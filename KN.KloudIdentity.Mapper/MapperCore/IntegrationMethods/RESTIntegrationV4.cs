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
using Microsoft.Extensions.Options;
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
    private readonly IEnumerable<IAuthStrategy> _authStrategies;
    public IntegrationMethods IntegrationMethod { get; init; }
    private readonly IOptions<AppSettings> _appSettings;

    public RESTIntegrationV4(IAuthContext authContext, IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IKloudIdentityLogger logger, IOptions<AppSettings> appSettings, IEnumerable<IAuthStrategy> authStrategies)
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

    public virtual async Task<Core2EnterpriseUser> ReplaceAsync(dynamic payload, Core2EnterpriseUser resource, string appId, AppConfig appConfig, ActionStep actionStep, string correlationId, CancellationToken cancellationToken = default)
    {
        if (actionStep == null)
            throw new ArgumentNullException(nameof(actionStep));

        if (string.IsNullOrWhiteSpace(actionStep.EndPoint))
            throw new ArgumentException("ActionStep endpoint must be provided for REPLACE operation.");

        // Format endpoint with identifier if needed
        string endpoint = GenerateFormattedEndpoint(resource.Identifier, actionStep);

        var customConfig = _appSettings.Value.AppIntegrationConfigs?.FirstOrDefault(x => x.AppId == appId);
        var client = await CreateHttpClientAsync(appConfig, SCIMDirections.Outbound, cancellationToken);

        var content = PrepareHttpContent(payload as JObject ?? JObject.FromObject(payload), customConfig?.HttpSettings?.ContentType);

        HttpMethod httpMethod = actionStep.HttpVerb switch
        {
            HttpVerbs.PUT => HttpMethod.Put,
            HttpVerbs.PATCH => HttpMethod.Patch,
            _ => throw new NotSupportedException("Unsupported HTTP verb for Replace operation.")
        };

        using var request = new HttpRequestMessage(httpMethod, endpoint)
        {
            Content = content
        };

        var response = await client.SendAsync(request, cancellationToken);
        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            Log.Error(
                "[RESTIntegrationV4] ReplaceAsync failed. AppId: {AppId}, CorrelationID: {CorrelationID}, StatusCode: {StatusCode}, Response: {ResponseBody}",
                appConfig.AppId, correlationId, response.StatusCode, responseBody);
            throw new HttpRequestException($"Error replacing user: {response.StatusCode} - {responseBody}");
        }

        // Optionally log success
        _ = CreateLogAsync(appConfig.AppId,
            $"Replace User (Step {actionStep.StepOrder})",
            $"User replaced successfully for the id {resource.Identifier}",
            LogType.Edit,
            LogSeverities.Information,
            correlationId);

        return resource;
    }

    /// <summary>
    /// Retrieves a user by identifier using action-based configuration (multi-step, V4).
    /// </summary>
    /// <param name="identifier">Unique identifier of the user</param>
    /// <param name="appConfig">App configuration</param>
    /// <param name="actionStep">Action configuration containing steps</param>
    /// <param name="correlationId">Correlation ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Retrieved user</returns>
    public virtual async Task<Core2EnterpriseUser> GetAsync(string identifier, AppConfig appConfig, ActionStep actionStep, string correlationId, CancellationToken cancellationToken = default)
    {
        if (actionStep == null)
            throw new ArgumentNullException(nameof(actionStep));

        if (string.IsNullOrWhiteSpace(actionStep.EndPoint))
            throw new ArgumentException("ActionStep endpoint must be provided for GET operation.");

        // Format endpoint with identifier if needed
        string endpoint = GenerateFormattedEndpoint(identifier, actionStep);

        var customConfig = _appSettings.Value.AppIntegrationConfigs?.FirstOrDefault(x => x.AppId == appConfig.AppId);
        var client = await CreateHttpClientAsync(appConfig, SCIMDirections.Outbound, cancellationToken);

        var response = await client.GetAsync(endpoint, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            Log.Error(
                "GET API for users failed. Identifier: {Identifier}, AppId: {AppId}, CorrelationID: {CorrelationID}, StatusCode: {StatusCode}, Response: {ResponseBody}",
                identifier, appConfig.AppId, correlationId, response.StatusCode, errorContent);

            throw new HttpRequestException($"GET user failed: {response.StatusCode} - {errorContent}");
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var user = JsonConvert.DeserializeObject<JObject>(content);
        if (user == null)
            throw new NotFoundException($"User not found with identifier: {identifier}");

        var core2EntUsr = new Core2EnterpriseUser();

        var urnPrefix = _configuration["urnPrefix"];
        if (string.IsNullOrEmpty(urnPrefix))
            throw new InvalidOperationException("Configuration value 'urnPrefix' is missing or empty.");

        string usernameField = GetFieldMapperValue(actionStep, appConfig.AppId, "UserName", urnPrefix);

        core2EntUsr.Identifier = identifier;

        if (string.Equals(customConfig?.ClientType, "Navitaire", StringComparison.OrdinalIgnoreCase))
        {
            core2EntUsr.KIExtension.ExtensionAttribute1 = user["data"]?[0]?["userKey"]?.ToString() ?? string.Empty;
            core2EntUsr.UserName = user["data"]?[0]?["username"]?.ToString() ?? string.Empty;
        }
        else
        {
            core2EntUsr.UserName = GetValueCaseInsensitive(user, usernameField);
        }

        // Create log for the operation.
        _ = CreateLogAsync(appConfig.AppId,
            "Get User",
            $"User retrieved successfully for the id {identifier}",
            LogType.Read,
            LogSeverities.Information,
            correlationId);

        return core2EntUsr;
    }

    // Helper to format endpoint with identifier if needed
    private string GenerateFormattedEndpoint(string identifier, ActionStep actionStep)
    {
        string formattedEndpoint = actionStep.EndPoint;
        if (actionStep.EndPoint != null && actionStep.EndPoint.Contains('{'))
        {
            // For now, only support {0} -> identifier
            formattedEndpoint = string.Format(actionStep.EndPoint, identifier);
        }

        return formattedEndpoint;
    }

    // Helper to get value case-insensitive from JObject
    private string GetValueCaseInsensitive(JObject? jsonObject, string propertyName)
    {
        var property = jsonObject?.Properties()
            .FirstOrDefault(p => string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase));
        return property?.Value?.ToString() ?? string.Empty;
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

        var custom = _appSettings.Value.AppIntegrationConfigs?.FirstOrDefault(x => x.AppId == appConfig.AppId);
        var content = PrepareHttpContent(jPayload, custom?.HttpSettings?.ContentType);

        HttpMethod httpMethod = actionStep.HttpVerb switch
        {
            HttpVerbs.POST => HttpMethod.Post,
            HttpVerbs.PUT => HttpMethod.Put,
            _ => throw new NotSupportedException($"Action step with StepOrder {actionStep.StepOrder}, HttpVerb {actionStep.HttpVerb}, EndPoint '{actionStep.EndPoint}' is not supported for provisioning.")
        };

        string formattedEndpoint = actionStep.EndPoint.Contains('{')
                    ? GenerateFormattedEndpoint(GetIDValue(jPayload, actionStep, appConfig, correlationId, HttpRequestTypes.POST), actionStep)
                    : actionStep.EndPoint;

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

        var requestType = httpMethod == HttpMethod.Post ? HttpRequestTypes.POST
            : httpMethod == HttpMethod.Put ? HttpRequestTypes.PUT
            : httpMethod == HttpMethod.Patch ? HttpRequestTypes.PATCH
            : HttpRequestTypes.POST;

        var responseJson = JObject.Parse(responseBody);
        var idVal = GetIDValue(responseJson, actionStep, appConfig, correlationId, requestType);

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

    [Obsolete("Use ProvisionAsync with ActionStep parameter instead.")]
    public Task<Core2EnterpriseUser?> ProvisionAsync(dynamic payload, AppConfig appConfig, string correlationId, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("This method is obsolete and no longer supported. Use the overload that accepts an ActionStep parameter instead.");
    }

    [Obsolete("Use GetAsync with ActionStep parameter instead.")]
    public Task<Core2EnterpriseUser> GetAsync(string identifier, AppConfig appConfig, string correlationId, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("This method is obsolete and no longer supported. Use the overload that accepts an ActionStep parameter instead.");
    }

    [Obsolete("Use ReplaceAsync with ActionStep parameter instead.")]
    public virtual Task ReplaceAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig, string correlationId)
    {
        throw new NotSupportedException("This method is obsolete and no longer supported. Use the overload that accepts an ActionStep parameter instead.");
    }

    [Obsolete("Use UpdateAsync with ActionStep parameter instead.")]
    public virtual Task UpdateAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig, string correlationId)
    {
        throw new NotSupportedException("This method is obsolete and no longer supported. Use the overload that accepts an ActionStep parameter instead.");
    }

    public virtual Task UpdateAsync(dynamic payload, Core2EnterpriseUser resource, string appId, AppConfig appConfig, ActionStep actionStep, string correlationId, CancellationToken cancellationToken = default)
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

        var customHttpClient = _appSettings.Value.AppIntegrationConfigs?.FirstOrDefault(x => x.AppId == appConfig.AppId);
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

    protected virtual string GetFieldMapperValue(ActionStep actionStep, string appId, string fieldName, string urnPrefix)
    {
        var field = actionStep.UserAttributeSchemas?.FirstOrDefault(f => f.SourceValue == fieldName);
        if (field == null)
        {
            Log.Error("Field not found in the user schema. FieldName: {FieldName}, AppId: {AppId}", fieldName,
                appId);
            throw new NotFoundException(fieldName + " field not found in the user schema.");
        }

        return field.DestinationField.Remove(0, urnPrefix.Length);
    }

    protected virtual dynamic GetIDValue(JObject response, ActionStep actionStep, AppConfig appConfig, string correlationId, HttpRequestTypes? requestType = HttpRequestTypes.POST)
    {
        var idField = GetFieldMapperValue(actionStep, appConfig.AppId, "Identifier", _configuration["urnPrefix"]!);
        if (string.IsNullOrEmpty(idField))
        {
            // Try to find a property like "id", "identifier", "key", etc.
            var possibleKeys = new[] { "id", "identifier", "key", "userKey", "user_id", "userId" };
            foreach (var key in possibleKeys)
            {
                // Try to find the key at any depth in the payload (recursive search)
                var token = response.SelectToken($"$..{key}", false);
                if (token != null)
                {
                    Log.Warning(
                        "Identifier field not mapped, but found '{Key}' in payload. AppId: {AppId}, CorrelationID: {CorrelationID}",
                        key, appConfig.AppId, correlationId);

                    return token.ToString();
                }
            }

            Log.Error(
                "Identifier field not configured and no fallback found in payload. AppId: {AppId}, CorrelationID: {CorrelationID}",
                appConfig.AppId, correlationId);
            throw new InvalidOperationException("Identifier field not configured and no fallback found in payload.");
        }

        var idFieldPath = response.SelectToken(idField);
        if (idFieldPath == null)
        {
            Log.Error(
                "Identifier field not found in the response payload. AppId: {AppId}, CorrelationID: {CorrelationID}, Field: {Field}",
                appConfig.AppId, correlationId, idField);
            throw new InvalidOperationException("Identifier field not found in the response payload.");
        }

        var idVal = idFieldPath.ToString();
        return idVal;
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
