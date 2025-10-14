using System;
using System.Security.Authentication;
using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Common;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Infrastructure.CEB.Abstractions;
using KN.KloudIdentity.Mapper.Infrastructure.CEB.Commands;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using KN.KloudIdentity.Mapper.Utils;
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
    private readonly ICreateUserDetailsToStorageCommand _createUserDetailsToStorageCommand;
    private readonly IGetUserDetailsFromStorageQuery _getUserDetailsFromStorageQuery;


    public RESTIntegrationV2(IAuthContext authContext, IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IKloudIdentityLogger logger, 
        IEnumerable<IAuthStrategy> authStrategies, 
        IOptions<AppSettings> appSettings,
        ICreateUserDetailsToStorageCommand createUserDetailsToStorageCommand,
        IGetUserDetailsFromStorageQuery getUserDetailsFromStorageQuery
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
        _createUserDetailsToStorageCommand = createUserDetailsToStorageCommand;
        _getUserDetailsFromStorageQuery = getUserDetailsFromStorageQuery;
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

        // need to mapping payload to Navitaire format       
                

        return await Task.FromResult(payload);
    }

    public override async Task<Core2EnterpriseUser?> ProvisionAsync(dynamic payload, AppConfig appConfig,
        string correlationId,
        CancellationToken cancellationToken = default)
    {

        //var result = await base.ProvisionAsync((object)payload, appConfig, correlationId, cancellationToken);
        var result = new Core2EnterpriseUser { Identifier = "TESTInfosecCRUDRowel", UserName = payload["username"] };
        // Get API call to get userKey
        var result2 = await base.GetAsync(result!.Identifier, appConfig, correlationId, cancellationToken);

        var userkey = result2.KIExtension.ExtensionAttribute1;
        // store username, userkey and rolecode to database

        var saveUserKey = await _createUserDetailsToStorageCommand.CreateUserKeyDataAsync(userkey, result!.UserName);      

        return result;
    }

    public override async Task ReplaceAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig, string correlationId)
    {

        string username = payload["username"];
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be null or empty in the payload.", nameof(payload));
    

        // get userKey from database using resource identifier
        var userDetails = await _getUserDetailsFromStorageQuery.GetUserKeyDataAsync(payload["username"]);

        // set userKey to resource identifier
        if (userDetails != null)
        {            
            resource.Identifier = userDetails.UserKey;       
        }
        else
        {
            // Handle the case where user not found in storage
            Log.Warning("No user mapping found in storage for username {Username}. CorrelationId: {CorrelationId}", username, correlationId);
           
            throw new InvalidOperationException($"User not found in storage: {username}");
        }

        
        // call base method
        await base.ReplaceAsync((object)payload, resource, appConfig, correlationId);

        Log.Information("ReplaceAsync completed for username {Username}. CorrelationId: {CorrelationId}", username, correlationId);

    }

    public async override Task UpdateAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig, string correlationId)
    {
        // get userKey from database using resource identifier
        // set userKey to resource identifier

        string username = resource.Identifier;
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be null or empty in the payload.", nameof(payload));


        // get userKey from database using resource identifier
        var userDetails = await _getUserDetailsFromStorageQuery.GetUserKeyDataAsync(payload["username"]);

        // set userKey to resource identifier
        if (userDetails != null)
        {
            resource.Identifier = userDetails.UserKey;
        }
        else
        {
            // Handle the case where user not found in storage
            Log.Warning("No user mapping found in storage for username {Username}. CorrelationId: {CorrelationId}", username, correlationId);

            throw new InvalidOperationException($"User not found in storage: {username}");
        }


        // call base method
        await base.UpdateAsync((object)payload, resource, appConfig, correlationId);
    }

    public override Task DeleteAsync(string identifier, AppConfig appConfig, string correlationID)
    {
        // get userKey from database using resource identifier
        // set userKey to resource identifier
        // call base method
        return base.DeleteAsync(identifier, appConfig, correlationID);
    }
}