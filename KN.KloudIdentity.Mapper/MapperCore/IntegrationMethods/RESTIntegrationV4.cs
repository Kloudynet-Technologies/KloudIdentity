using System;
using System.Security.Authentication;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.SCIM;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using HttpClientExtensions = KN.KloudIdentity.Mapper.Utils.HttpClientExtensions;

namespace KN.KloudIdentity.Mapper.MapperCore;

/// <summary>
/// REST Integration Method V4.
/// This integration method override implments the multi step outbound user provisioning flow.
/// Code refactored and re-engineered to consolidate common logic in previous versions and new features.
/// </summary>
public class RESTIntegrationV4 : IIntegrationBase
{
    private readonly IAuthContext _authContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IKloudIdentityLogger _logger;
    private readonly AppSettings _appSettings;
    private readonly IEnumerable<IAuthStrategy> _authStrategies;
    public IntegrationMethods IntegrationMethod { get; init; }

    public RESTIntegrationV4(IAuthContext authContext, IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IKloudIdentityLogger logger, AppSettings appSettings, IEnumerable<IAuthStrategy> authStrategies)
    {
        _authContext = authContext;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _appSettings = appSettings;
        _authStrategies = authStrategies;
        IntegrationMethod = IntegrationMethods.REST;
    }

    public Task DeleteAsync(string identifier, AppConfig appConfig, string correlationId)
    {
        throw new NotImplementedException();
    }

    public Task<Core2EnterpriseUser> GetAsync(string identifier, AppConfig appConfig, string correlationId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public virtual async Task<dynamic> GetAuthenticationAsync(AppConfig config, SCIMDirections direction = SCIMDirections.Outbound, CancellationToken cancellationToken = default, params dynamic[] args)
    {
        Log.Information($"Getting authentication token for direction: {direction} for app: {config.AppId}");

        return await _authContext.GetTokenAsync(config, direction);
    }

    public virtual async Task<dynamic> MapAndPreparePayloadAsync(IList<AttributeSchema> schema, Core2EnterpriseUser resource, CancellationToken cancellationToken = default)
    {
        Log.Information("Mapping and preparing payload for resource: {ResourceId}", resource.Identifier);

        var payload = JSONParserUtilV2<Resource>.Parse(schema, resource);

        return await Task.FromResult(payload);
    }

    public virtual async Task<Core2EnterpriseUser?> ProvisionAsync(dynamic payload, AppConfig appConfig, string correlationId, CancellationToken cancellationToken = default)
    {
        Log.Information("Provisioning user for app: {AppId}, CorrelationId: {CorrelationId}", appConfig.AppId, correlationId);

        var userUrisList = appConfig.Actions?
            .Where(a => a.ActionName == ActionNames.CREATE && a.ActionTarget == ActionTargets.USER)
            .Select(a => a.ActionSteps)
            .OrderBy(s => s.Min(step => step.StepOrder))
            .ToList();
        if (userUrisList == null || !userUrisList.Any())
        {
            Log.Error("No CREATE APIs configured for app: {AppId}. Please add at least one CREATE API.", appConfig.AppId);

            throw new InvalidOperationException("No CREATE APIs configured. Please add at least one CREATE API.");
        }

        // Ensure the payload is JObject
        JObject jPayload = payload as JObject ?? JObject.FromObject(payload);

        // Get an auth token if required
        var httpClient = await CreateHttpClientAsync(appConfig, SCIMDirections.Outbound, CancellationToken.None);

        throw new NotImplementedException();
    }

    public Task ReplaceAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig, string correlationId)
    {
        throw new NotImplementedException();
    }

    public Task UpdateAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig, string correlationId)
    {
        throw new NotImplementedException();
    }

    public Task<(bool, string[])> ValidatePayloadAsync(dynamic payload, AppConfig appConfig, string correlationId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    protected virtual async Task<HttpClient> CreateHttpClientAsync(AppConfig appConfig, SCIMDirections direction,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient();
        var token = await GetAuthenticationAsync(appConfig, direction, cancellationToken, client);

        HttpClientExtensions.SetAuthenticationHeaders(client, appConfig.AuthenticationMethodOutbound,
            appConfig.AuthenticationDetails, token);

        var customHttpClient = _appSettings.AppIntegrationConfigs?.FirstOrDefault(x => x.AppId == appConfig.AppId);
        if (customHttpClient?.HttpSettings?.Headers is { Count: > 0 })
        {
            client.SetCustomHeaders(customHttpClient.HttpSettings.Headers);
        }

        return client;
    }
}
