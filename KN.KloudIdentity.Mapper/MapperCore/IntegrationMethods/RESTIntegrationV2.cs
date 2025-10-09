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

    public RESTIntegrationV2(IAuthContext authContext, IHttpClientFactory httpClientFactory, IConfiguration configuration,
        IKloudIdentityLogger logger, IEnumerable<IAuthStrategy> authStrategies, IOptions<AppSettings> appSettings) : base(authContext, httpClientFactory, configuration, logger)
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
        SCIMDirections direction = SCIMDirections.Outbound, CancellationToken cancellationToken = default, params dynamic[] args)
    {
        Log.Information("Getting authentication token for direction: {Direction} for app: {AppId}", direction, config.AppId);

        // If the AppId is in DotRezAppIds, use DotRezAuthStrategy
        var dotRezAppIdsList = _appSettings.Value.DotRezAppIds ?? new List<string>();
        var dotRezAppIds = new HashSet<string>(dotRezAppIdsList, StringComparer.OrdinalIgnoreCase);

        if (dotRezAppIds.Contains(config.AppId))
        {
            if (args.Length == 0 || args[0] is not HttpClient httpClient)
            {
                throw new ArgumentException("Authentication configuration is required for DotRez authentication.");
            }

            var dotRezAuthStrategy = _authStrategies.FirstOrDefault(s => s.AuthenticationMethod == AuthenticationMethods.DotRez);
            if (dotRezAuthStrategy == null)
            {
                throw new InvalidOperationException("DotRezAuthStrategy is not registered.");
            }

            // Get the token (response is expected to be a JSON string or object)
            var authDetails = JsonConvert.DeserializeObject<dynamic>(config.AuthenticationDetails.ToString());
            var token = await dotRezAuthStrategy.GetTokenAsync(authDetails, new[] { "WW2" });

            var tokenObj = token is string tokenStr ? JsonConvert.DeserializeObject<JObject>(tokenStr) : token as JObject;
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
}
