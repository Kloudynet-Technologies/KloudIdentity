using System;
using System.Security.Authentication;
using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Common;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.SCIM;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

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
        IKloudIdentityLogger logger, IEnumerable<IAuthStrategy> authStrategies, IOptions<AppSettings> appSettings) :
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

        // need to mapping payload to Navitaire format

        return await Task.FromResult(payload);
    }

    public override async Task<Core2EnterpriseUser?> ProvisionAsync(dynamic payload, AppConfig appConfig,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var result = await base.ProvisionAsync((object)payload, appConfig, correlationId, cancellationToken);

        // Get API call to get userKey
        var result2 = await base.GetAsync(result!.Identifier, appConfig, correlationId, cancellationToken);

        // store username, userkey and rolecode to database

        return result;
    }

    public override Task ReplaceAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig, string correlationId)
    {
        // get userKey from database using resource identifier
        // set userKey to resource identifier
        // call base method
        return base.ReplaceAsync((object)payload, resource, appConfig, correlationId);
    }

    public override Task UpdateAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig, string correlationId)
    {
        // get userKey from database using resource identifier
        // set userKey to resource identifier
        // call base method
        return base.UpdateAsync((object)payload, resource, appConfig, correlationId);
    }

    public override Task DeleteAsync(string identifier, AppConfig appConfig, string correlationID)
    {
        // get userKey from database using resource identifier
        // set userKey to resource identifier
        // call base method
        return base.DeleteAsync(identifier, appConfig, correlationID);
    }
}