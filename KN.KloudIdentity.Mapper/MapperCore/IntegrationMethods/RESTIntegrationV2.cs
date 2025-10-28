using System.Security.Authentication;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.SCIM;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using static MassTransit.ValidationResultExtensions;

namespace KN.KloudIdentity.Mapper.MapperCore;

public class RESTIntegrationV2 : RESTIntegration
{
    private readonly IAuthContext _authContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IKloudIdentityLogger _logger;
    private readonly IEnumerable<IAuthStrategy> _authStrategies;
    private readonly IOptions<AppSettings> _appSettings;

    public RESTIntegrationV2(IAuthContext authContext, IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IKloudIdentityLogger logger, 
        IEnumerable<IAuthStrategy> authStrategies, 
        IOptions<AppSettings> appSettings
        ) :
        base(authContext, httpClientFactory, configuration, appSettings, logger)
    {
        _authContext = authContext;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _appSettings = appSettings;
        _authStrategies = authStrategies;
        IntegrationMethod = IntegrationMethods.REST;       
    }

    public override async Task<dynamic> GetAuthenticationAsync(AppConfig config,
        SCIMDirections direction = SCIMDirections.Outbound, CancellationToken cancellationToken = default,
        params dynamic[] args)
    {
        Log.Information("Getting authentication token for direction: {Direction} for app: {AppId}", direction,
            config.AppId);

        // If the AppId is in DotRezAppIds, use DotRezAuthStrategy
        var custom = _appSettings.Value.AppIntegrationConfigs?.FirstOrDefault(x => x.AppId == config.AppId);

        if (string.Equals(custom?.ClientType, "Navitaire", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length == 0 || args[0] is not HttpClient httpClient)
            {
                throw new ArgumentException("Authentication configuration is required for DotRez authentication.");
            }

            var dotRezAuthStrategy =
                _authStrategies.FirstOrDefault(s => s.AuthenticationMethod == AuthenticationMethods.DotRez);
            if (dotRezAuthStrategy == null)
            {
                throw new InvalidOperationException("DotRezAuthStrategy is not registered.");
            }

            // Get the token (response is expected to be a JSON string or object)
            var authDetails = JsonConvert.DeserializeObject<dynamic>(config.AuthenticationDetails.ToString());
            var token = await dotRezAuthStrategy.GetTokenAsync(authDetails, new[] { "WW2" });

            var tokenObj = token is string tokenStr
                ? JsonConvert.DeserializeObject<JObject>(tokenStr)
                : token as JObject;
            if (tokenObj == null)
            {
                throw new AuthenticationException("Failed to parse DotRez authentication token.");
            }

            httpClient.DefaultRequestHeaders.Add("X-Auth-Token", tokenObj["dotrezToken"]?.ToString());

            return tokenObj["apigeeToken"]?.ToString() ?? "";
        }

        // Call the base method to get the token
        return await base.GetAuthenticationAsync(config, direction, cancellationToken);
    }

    public override async Task<dynamic> MapAndPreparePayloadAsync(IList<AttributeSchema> schema,
        Core2EnterpriseUser resource,
        CancellationToken cancellationToken = default) 
    {
        var payload = JSONParserUtilV2<Resource>.Parse(schema, resource);
                
        return await Task.FromResult(payload);
    }

    public override async Task<Core2EnterpriseUser?> ProvisionAsync(dynamic payload, AppConfig appConfig,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var custom = _appSettings.Value.AppIntegrationConfigs?.FirstOrDefault(x => x.AppId == appConfig.AppId);

        if (string.Equals(custom?.ClientType, "Navitaire", StringComparison.OrdinalIgnoreCase))
        {
            payload = PreparePayloadOptional(payload, appConfig);
        }
        
        var result = await base.ProvisionAsync((object)payload, appConfig, correlationId, cancellationToken);                     

        return result;
    }

    public override async Task ReplaceAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig, string correlationId)
    {
        var custom = _appSettings.Value.AppIntegrationConfigs?.FirstOrDefault(x => x.AppId == appConfig.AppId);

        if (string.Equals(custom?.ClientType, "Navitaire", StringComparison.OrdinalIgnoreCase))
        {           
            payload = PreparePayloadOptional(payload, appConfig);

            // Get API call to get userKey
            var result2 = await base.GetAsync(resource.Identifier, appConfig, correlationId);

            var userkey = result2.KIExtension.ExtensionAttribute1;

            resource.Identifier = userkey;
        }  
                
        // call base method
        await base.ReplaceAsync((object)payload, resource, appConfig, correlationId);

        Log.Information("ReplaceAsync completed for username {Username}. CorrelationId: {CorrelationId}", resource.Identifier, correlationId);
    }

    public async override Task UpdateAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig, string correlationId)
    {
        var custom = _appSettings.Value.AppIntegrationConfigs?.FirstOrDefault(x => x.AppId == appConfig.AppId);

        if (string.Equals(custom?.ClientType, "Navitaire", StringComparison.OrdinalIgnoreCase))
        {
            payload = PreparePayloadOptional(payload, appConfig);

            // Get API call to get userKey
            var result2 = await base.GetAsync(resource.Identifier, appConfig, correlationId);

            var userkey = result2.KIExtension.ExtensionAttribute1;

            resource.Identifier = userkey;
        }   

        // call base method
        await base.UpdateAsync((object)payload, resource, appConfig, correlationId);

        Log.Information("UpdateAsync completed for username {Username}. CorrelationId: {CorrelationId}", resource.Identifier, correlationId);

    }

    public async override Task DeleteAsync(string identifier, AppConfig appConfig, string correlationID)
    {
        var custom = _appSettings.Value.AppIntegrationConfigs?.FirstOrDefault(x => x.AppId == appConfig.AppId);

        // get userKey from database using resource identifier       
        if (string.Equals(custom?.ClientType, "Navitaire", StringComparison.OrdinalIgnoreCase))
        {         
            // Get API call to get userKey
            var result2 = await base.GetAsync(identifier, appConfig, correlationID);

            var userkey = result2.KIExtension.ExtensionAttribute1;

            identifier = userkey;
        }

        // call base method
        await base.DeleteAsync(identifier, appConfig, correlationID);
    }


    private dynamic PreparePayloadOptional(dynamic payload , AppConfig appConfig)
    {
        var custom = _appSettings.Value.AppIntegrationConfigs?
        .FirstOrDefault(x => x.AppId == appConfig.AppId);

        // Only apply transformation if ClientType is Navitaire
        if (!string.Equals(custom?.ClientType, "Navitaire", StringComparison.OrdinalIgnoreCase))
            return payload;

        var payloadObj = payload is JObject jObj ? jObj : JObject.FromObject(payload);

        // Safely extract the existing "name" section
        var nameSection = (payloadObj["name"] as JObject) ?? new JObject();

        // Remove the original "name"
        payloadObj.Remove("name");

        // Build the "person" object
        var personObj = new JObject
        {
            ["name"] = new JObject
            {
                ["first"] = nameSection["first"] ?? JValue.CreateNull(),
                ["last"] = nameSection["last"] ?? JValue.CreateNull()
            }
        };

        // Insert into the payload
        payloadObj["person"] = personObj;

        Log.Information("Navitaire payload transformation applied for AppId: {AppId}", appConfig.AppId);

        return payloadObj;
    }
}