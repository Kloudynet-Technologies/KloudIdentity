using System;
using System.Security.Authentication;
using System.Text;
using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Common;
using KN.KloudIdentity.Mapper.Common.Exceptions;
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
public class RESTIntegrationV4 : IIntegrationBaseV2
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

    public virtual Task DeleteAsync(string identifier, AppConfig appConfig, string correlationId)
    {
        throw new NotImplementedException();
    }

    public virtual Task<Core2EnterpriseUser> GetAsync(string identifier, AppConfig appConfig, string correlationId, CancellationToken cancellationToken = default)
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

    public virtual async Task<Core2EnterpriseUser?> ProvisionAsync(dynamic payload, string appId, AppConfig appConfig, ActionStep actionStep, string correlationId, CancellationToken cancellationToken = default)
    {
        Log.Information("Provisioning user for app: {AppId}, CorrelationId: {CorrelationId}", appId, correlationId);

        // Ensure the payload is JObject
        JObject jPayload = payload as JObject ?? JObject.FromObject(payload);

        // Get an auth token if required
        var httpClient = await CreateHttpClientAsync(appConfig, SCIMDirections.Outbound, cancellationToken);

        var custom = _appSettings.AppIntegrationConfigs?.FirstOrDefault(x => x.AppId == appConfig.AppId);
        var content = PrepareHttpContent(jPayload, custom?.HttpSettings?.ContentType);

        HttpMethod httpMethod = actionStep.HttpVerb switch
        {
            HttpVerbs.POST => HttpMethod.Post,
            HttpVerbs.PUT => HttpMethod.Put,
            _ => throw new NotSupportedException($"Action step with StepOrder {actionStep.StepOrder}, HttpVerb {actionStep.HttpVerb}, EndPoint '{actionStep.EndPoint}' is not supported for provisioning.")
        };
        string formattedEndpoint = GenerateFormattedEndpoint(payload, appConfig, actionStep);

        using var request = new HttpRequestMessage(httpMethod, formattedEndpoint)
        {
            Content = content
        };

        var response = await httpClient.SendAsync(request, cancellationToken);

        // Read the full response
        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            Log.Error(
                "Provisioning failed. AppId: {AppId}, CorrelationID: {CorrelationID}, StatusCode: {StatusCode}, Response: {ResponseBody}",
                appConfig.AppId, correlationId, response.StatusCode, responseBody);

            throw new HttpRequestException($"Error creating user: {response.StatusCode} - {responseBody}");
        }

        var idField = GetFieldMapperValue(appConfig, "Identifier", _configuration["urnPrefix"]!);
        var idVal = payload[idField]!.ToString();

        // Fire-and-forget success logging
        _ = Task.Run(async () =>
        {
            try
            {
                Log.Information(
                    "User created successfully. Id: {IdVal}. AppId: {AppId}, CorrelationID: {CorrelationID}",
                    idVal, appConfig.AppId, correlationId);

                await CreateLogAsync(
                    appConfig.AppId,
                    "Create User",
                    $"User created successfully for ID {idVal}",
                    LogType.Provision,
                    LogSeverities.Information,
                    correlationId);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to log user creation. AppId: {AppId}, CorrelationID: {CorrelationID}",
                    appConfig.AppId, correlationId);
            }
        }, cancellationToken);

        return new Core2EnterpriseUser()
        {
            Identifier = idVal
        };
    }

    protected virtual string GenerateFormattedEndpoint(dynamic payload, AppConfig appConfig, ActionStep actionStep)
    {
        string formattedEndpoint = actionStep.EndPoint;
        if (actionStep.EndPoint != null && actionStep.EndPoint.Contains('{'))
        {
            // Attempt to extract values for placeholders from the payload
            // For example, if endpoint is "/users/{0}/confirm", use the "Identifier" field
            var idField = GetFieldMapperValue(appConfig, "Identifier", _configuration["urnPrefix"]!);
            var idVal = payload[idField]?.ToString();
            // Add more fields as needed for additional placeholders
            // For now, only support {0} -> idVal
            formattedEndpoint = string.Format(actionStep.EndPoint, idVal);
        }

        return formattedEndpoint;
    }

    [Obsolete("Use ProvisionAsync with ActionStep parameter instead.")]
    public Task<Core2EnterpriseUser?> ProvisionAsync(dynamic payload, AppConfig appConfig, string correlationId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public virtual Task ReplaceAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig, string correlationId)
    {
        throw new NotImplementedException();
    }

    public virtual Task UpdateAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig, string correlationId)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Validates the payload before provisioning.
    /// Able to override in derived classes for custom validation logic.
    /// </summary>
    /// <param name="payload"></param>
    /// <param name="appConfig"></param>
    /// <param name="correlationId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public virtual Task<(bool, string[])> ValidatePayloadAsync(dynamic payload, AppConfig appConfig, string correlationId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult((true, Array.Empty<string>()));
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

    protected virtual HttpContent PrepareHttpContent(JObject payload, string? contentType)
    {
        if (string.Equals(contentType, "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
        {
            var encodedJson = Uri.EscapeDataString(payload.ToString(Formatting.None));
            return new StringContent(encodedJson, Encoding.UTF8, "application/x-www-form-urlencoded");
        }

        // Default JSON
        return new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");
    }

    protected virtual string GetFieldMapperValue(AppConfig appConfig, string fieldName, string urnPrefix)
    {
        var field = appConfig.UserAttributeSchemas.FirstOrDefault(f => f.SourceValue == fieldName);
        if (field != null)
        {
            return field.DestinationField.Remove(0, urnPrefix.Length);
        }
        else
        {
            Log.Error("Field not found in the user schema. FieldName: {FieldName}, AppId: {AppId}", fieldName,
                appConfig.AppId);
            throw new NotFoundException(fieldName + " field not found in the user schema.");
        }
    }

    protected async Task CreateLogAsync(string appId, string eventInfo, string logMessage, LogType logType,
        LogSeverities logSeverity, string correlationId)
    {
        var logEntity = new CreateLogEntity(
            appId,
            logType.ToString(),
            logSeverity,
            eventInfo,
            logMessage,
            correlationId,
            AppConstant.LoggerName,
            DateTime.UtcNow,
            AppConstant.User,
            null,
            null
        );

        await _logger.CreateLogAsync(logEntity);
    }
}
