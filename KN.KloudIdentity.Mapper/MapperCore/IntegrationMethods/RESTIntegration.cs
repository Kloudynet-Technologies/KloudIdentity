using System;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Http;
using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Common;
using KN.KloudIdentity.Mapper.Common.Exceptions;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
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
/// Integration logic implementation for REST.
/// </summary>
public class RESTIntegration : IIntegrationBase
{
    private readonly IAuthContext _authContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IKloudIdentityLogger _logger;
    private readonly AppSettings _appSettings;
    public IntegrationMethods IntegrationMethod { get; init; }

    public RESTIntegration(IAuthContext authContext,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IOptions<AppSettings> appSettings,
        IKloudIdentityLogger logger)
    {
        IntegrationMethod = IntegrationMethods.REST;

        _authContext = authContext;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _appSettings = appSettings.Value;
    }

    /// <summary>
    /// Gets the authentication token asynchronously.
    /// </summary>
    /// <param name="config"></param>
    /// <param name="direction"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public virtual async Task<dynamic> GetAuthenticationAsync(AppConfig config,
        SCIMDirections direction = SCIMDirections.Outbound, CancellationToken cancellationToken = default,
        params dynamic[] args)
    {
        return await _authContext.GetTokenAsync(config, direction);
    }

    /// <summary>
    /// Attribute mapping and prepares the payload asynchronously.
    /// </summary>
    /// <param name="schema"></param>
    /// <param name="resource"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public virtual async Task<dynamic> MapAndPreparePayloadAsync(IList<AttributeSchema> schema,
        Core2EnterpriseUser resource, CancellationToken cancellationToken = default)
    {
        var payload = JSONParserUtilV2<Resource>.Parse(schema, resource);

        return await Task.FromResult(payload);
    }

    /// <summary>
    /// Provisions the user asynchronously to LOB application.
    /// </summary>
    /// <param name="payload"></param>
    /// <param name="appConfig"></param>
    /// <param name="correlationId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="HttpRequestException">When an error occurred during provisioning</exception>
    public virtual async Task<Core2EnterpriseUser?> ProvisionAsync(
        dynamic payload,
        AppConfig appConfig,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        Log.Information("Provisioning started for user creation. AppId: {AppId}, CorrelationID: {CorrelationID}",
            appConfig.AppId, correlationId);

        var userUri = appConfig.UserURIs?.FirstOrDefault()?.Post
                      ?? throw new InvalidOperationException("User creation endpoint not configured.");

        // Ensure payload is JObject
        JObject jPayload = payload as JObject ?? JObject.FromObject(payload);

        // Get auth token if required
        var httpClient = await CreateHttpClientAsync(appConfig, SCIMDirections.Outbound, CancellationToken.None);

        var custom = _appSettings.AppIntegrationConfigs?.FirstOrDefault(x => x.AppId == appConfig.AppId);
        var content = PrepareHttpContent(jPayload, custom?.ClientType, custom?.HttpSettings?.ContentType);
        var response = await httpClient.PostAsync(userUri, content, cancellationToken);

        // Read full response
        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            Log.Error(
                "Provisioning failed. AppId: {AppId}, CorrelationID: {CorrelationID}, StatusCode: {StatusCode}, Response: {ResponseBody}",
                appConfig.AppId, correlationId, response.StatusCode, responseBody);

            throw new HttpRequestException($"Error creating user: {response.StatusCode} - {responseBody}");
        }

        string? idVal = null;
        if (custom?.IsIdentifierTakeFromCreateUser == true)
            idVal = GetIdentifier(responseBody, custom.ClientType);
        else
        {
            var idField = GetFieldMapperValue(appConfig, "Identifier", _configuration["urnPrefix"]!);
            idVal = payload[idField]!.ToString();
            if (string.Equals(custom?.ClientType, "Navitaire", StringComparison.OrdinalIgnoreCase))
            {
                idVal = payload["userName"]!.ToString();
            }
        }

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

    /// <summary>
    /// Validates the payload asynchronously before been sent to LOB app.
    /// </summary>
    /// <param name="payload"></param>
    /// <param name="correlationId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>Validation status and error messages</returns>
    public virtual Task<(bool, string[])> ValidatePayloadAsync(dynamic payload, AppConfig appConfig,
        string correlationId, CancellationToken cancellationToken = default)
    {
        // No payload validation required for REST integration. Always return true.
        return Task.FromResult((true, Array.Empty<string>()));
    }

    /// <summary>
    /// List all users from the LOB application asynchronously.
    /// </summary>
    /// <param name="identifier">Unique identifier of the user</param>
    /// <param name="appConfig">App configurations</param>
    /// <param name="correlationId">Correlation ID</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="NotFoundException">When user isn't found in the LOB app</exception>
    /// <exception cref="HttpResponseException">When user not found in the LOB app</exception>
    /// <exception cref="ApplicationException">When GET API is notCore2EnterpriseUser resource, string appId,</exception>
    public async Task<Core2EnterpriseUser> GetAsync(string identifier, AppConfig appConfig, string correlationId,
        CancellationToken cancellationToken = default)
    {
        var userUri = appConfig.UserURIs.FirstOrDefault()?.Get
                      ?? throw new ApplicationException("GET API not configured.");
        var customConfig = _appSettings.AppIntegrationConfigs?.FirstOrDefault(x => x.AppId == appConfig.AppId);
        var client = await CreateHttpClientAsync(appConfig, SCIMDirections.Outbound, cancellationToken);
        var url = DynamicApiUrlUtil.GetFullUrl(userUri.ToString(), identifier);
        var response = await client.GetAsync(url, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var user = JsonConvert.DeserializeObject<JObject>(content);
            if (user == null)
            {
                throw new NotFoundException($"User not found with identifier: {identifier}");
            }

            var core2EntUsr = new Core2EnterpriseUser();

            string urnPrefix = _configuration["urnPrefix"]!;

            string usernameField = GetFieldMapperValue(appConfig, "UserName", urnPrefix);

            core2EntUsr.Identifier = identifier;

            if (string.Equals(customConfig?.ClientType, "Navitaire", StringComparison.OrdinalIgnoreCase))
            {
                core2EntUsr.KIExtension.ExtensionAttribute1 = user["data"]?[0]?["userKey"]?.ToString() ?? string.Empty;
                core2EntUsr.UserName = user["data"]?[0]?["username"]?.ToString() ?? string.Empty;
            }
            else if (string.Equals(customConfig?.ClientType, "ManageEngine SDP", StringComparison.OrdinalIgnoreCase))
            {
                core2EntUsr.UserName = GetValueCaseInsensitive(user["user"] as JObject, usernameField);
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

        Log.Error(
            "GET API for users failed. Identifier: {Identifier}, AppId: {AppId}, CorrelationID: {CorrelationID}, Error: {Error}",
            identifier, appConfig.AppId, correlationId, response.ReasonPhrase);
        throw new HttpResponseException(System.Net.HttpStatusCode.NotFound);
    }

    public string GetFieldMapperValue(AppConfig appConfig, string fieldName, string urnPrefix)
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

    private string GetValueCaseInsensitive(JObject? jsonObject, string propertyName)
    {
        var property = jsonObject?.Properties()
            .FirstOrDefault(p => string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase));

        return property!.Value.ToString();
    }

    /// <summary>
    /// Replaces a user in the LOB application asynchronously.
    /// </summary>
    /// <param name="payload">Payload to send to the replace operation</param>
    /// <param name="resource">Resource to be replaced</param>
    /// <param name="appConfig">Application config</param>
    /// <param name="correlationId">Correlation ID</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="HttpRequestException"></exception>
    public virtual async Task ReplaceAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig,
        string correlationId)
    {
        var userUrIs = appConfig.UserURIs?.FirstOrDefault()
                       ?? throw new InvalidOperationException("User creation endpoint not configured.");

        // Ensure payload is JObject
        JObject jPayload = payload as JObject ?? JObject.FromObject(payload);

        // Get auth token if required
        var httpClient = await CreateHttpClientAsync(appConfig, SCIMDirections.Outbound, CancellationToken.None);

        var custom = _appSettings.AppIntegrationConfigs?.FirstOrDefault(x => x.AppId == appConfig.AppId);
        var content = PrepareHttpContent(jPayload, custom?.ClientType, custom?.HttpSettings?.ContentType);
        HttpResponseMessage? response = null;

        if (userUrIs!.Put != null)
        {
            var apiPath = DynamicApiUrlUtil.GetFullUrl(userUrIs.Put!.ToString(), resource.Identifier);
            response = await httpClient.PutAsync(apiPath, content); // x-www-form-urlencoded or other

            // Read full response
            string responseBody = await response.Content.ReadAsStringAsync(CancellationToken.None);

            if (!response.IsSuccessStatusCode)
            {
                Log.Error(
                    "Updating failed. AppId: {AppId}, CorrelationID: {CorrelationID}, StatusCode: {StatusCode}, Response: {ResponseBody}",
                    appConfig.AppId, correlationId, response.StatusCode, responseBody);

                throw new HttpRequestException($"Error creating user: {response.StatusCode} - {responseBody}");
            }

            // Log the operation.
            _ = Task.Run(() =>
            {
                var idField = GetFieldMapperValue(appConfig, "Identifier", _configuration["urnPrefix"]!);
                string? idVal = payload[idField]!.ToString();

                Log.Information(
                    "User replaced successfully for the id {IdVal}. AppId: {AppId}, CorrelationID: {CorrelationID}",
                    idVal, appConfig.AppId, correlationId);

                _ = CreateLogAsync(appConfig.AppId,
                    "Replace User",
                    $"User replaced successfully for the id {idVal}",
                    LogType.Provision,
                    LogSeverities.Information,
                    correlationId);
            });
        }
        else if (userUrIs.Patch != null)
        {
            string apiPath = DynamicApiUrlUtil.GetFullUrl(userUrIs.Patch.ToString(), resource.Identifier);

            response = await httpClient.PatchAsync(apiPath, content);
            var responseBody = await response.Content.ReadAsStringAsync(CancellationToken.None);
            if (!response.IsSuccessStatusCode)
            {
                Log.Error(
                    "Updatting failed. AppId: {AppId}, CorrelationID: {CorrelationID}, StatusCode: {StatusCode}, Response: {ResponseBody}",
                    appConfig.AppId, correlationId, response.StatusCode, responseBody);

                throw new HttpRequestException($"Error creating user: {response.StatusCode} - {responseBody}");
            }

            // Log the operation.
            _ = Task.Run(() =>
            {
                var idField = GetFieldMapperValue(appConfig, "Identifier", _configuration["urnPrefix"]!);
                string? idVal = payload[idField].ToString();

                Log.Information(
                    "User replaced successfully for the id {IdVal}. AppId: {AppId}, CorrelationID: {CorrelationID}",
                    idVal, appConfig.AppId, correlationId);

                _ = CreateLogAsync(appConfig.AppId,
                    "Replace User",
                    $"User replaced successfully for the id {idVal}",
                    LogType.Provision,
                    LogSeverities.Information,
                    correlationId);
            });
        }
        else
        {
            Log.Error(
                "PUTAPIForUsers and PATCHAPIForUsers cannot both be null or empty. AppId: {AppId}, CorrelationID: {CorrelationID}",
                appConfig.AppId, correlationId);
            throw new ArgumentNullException("PUTAPIForUsers and PATCHAPIForUsers cannot both be null or empty");
        }

        // Check if the request was successful; otherwise, throw an exception.
        if (!response.IsSuccessStatusCode)
        {
            Log.Error(
                "User replacement failed. AppId: {AppId}, CorrelationID: {CorrelationID}, Error: {Error}",
                appConfig.AppId, correlationId, response.ReasonPhrase);
            throw new HttpRequestException(
                $"Error updating user: {response.StatusCode} - {response.ReasonPhrase}"
            );
        }
    }

    /// <summary>
    /// Updates a user in the LOB application asynchronously.
    /// </summary>
    /// <param name="payload"></param>
    /// <param name="resource"></param>
    /// <param name="appConfig"></param>
    /// <param name="correlationId"></param>
    /// <returns></returns>
    /// <exception cref="HttpRequestException"></exception>
    public virtual async Task UpdateAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig,
        string correlationId)
    {
        var userUrIs = appConfig.UserURIs.FirstOrDefault();
        // Ensure payload is JObject
        JObject jPayload = payload as JObject ?? JObject.FromObject(payload);

        // Get auth token if required
        var httpClient = await CreateHttpClientAsync(appConfig, SCIMDirections.Outbound, CancellationToken.None);

        var custom = _appSettings.AppIntegrationConfigs?.FirstOrDefault(x => x.AppId == appConfig.AppId);
        var content = PrepareHttpContent(jPayload, custom?.ClientType, custom?.HttpSettings?.ContentType);
        HttpResponseMessage? response = null;
        if (userUrIs!.Patch is not null)
        {
            var apiPath = DynamicApiUrlUtil.GetFullUrl(userUrIs!.Patch!.ToString(), resource.Identifier);
            response = await httpClient.PatchAsync(apiPath, content);
        }
        else if (userUrIs.Put is not null)
        {
            var apiPath = DynamicApiUrlUtil.GetFullUrl(userUrIs.Put!.ToString(), resource.Identifier);
            response = await httpClient.PutAsync(apiPath, content);
        }

        if (response is { IsSuccessStatusCode: false })
        {
            Log.Error(
                "User update failed. AppId: {AppId}, CorrelationID: {CorrelationID}, Identifier: {Identifier}, Error: {Error}",
                appConfig.AppId, correlationId, resource.Identifier, response.ReasonPhrase);
            throw new HttpRequestException(
                $"Error updating user: {response.StatusCode} - {response.ReasonPhrase}"
            );
        }

        // Log the operation.
        _ = Task.Run(() =>
        {
            var idField = GetFieldMapperValue(appConfig, "Identifier", _configuration["urnPrefix"]!);
            string? idVal = payload[idField]!.ToString();

            Log.Information(
                "User updated successfully for the id {IdVal}. AppId: {AppId}, CorrelationID: {CorrelationID}",
                idVal, appConfig.AppId, correlationId);

            _ = CreateLogAsync(appConfig.AppId,
                "Update User",
                $"User updated successfully for the id {idVal}",
                LogType.Provision,
                LogSeverities.Information,
                correlationId);
        });
    }

    /// <summary>
    /// Deletes a resource asynchronously.
    /// </summary>
    /// <param name="identifier"></param>
    /// <param name="appConfig"></param>
    /// <param name="correlationID"></param>
    /// <returns></returns>
    /// <exception cref="HttpRequestException"></exception>
    public virtual async Task DeleteAsync(string identifier, AppConfig appConfig, string correlationID)
    {
        var userUri = appConfig.UserURIs.FirstOrDefault()?.Delete ??
                      throw new InvalidOperationException("User deletion endpoint not configured.");
        var httpClient = await CreateHttpClientAsync(appConfig, SCIMDirections.Outbound, CancellationToken.None);
        var apiUrl = DynamicApiUrlUtil.GetFullUrl(userUri.ToString(), identifier);

        using var response = await httpClient.DeleteAsync(apiUrl);
        if (!response.IsSuccessStatusCode)
        {
            Log.Error(
                "User deletion failed. AppId: {AppId}, CorrelationID: {CorrelationID}, Identifier: {Identifier}, Error: {Error}",
                appConfig.AppId, correlationID, identifier, response.ReasonPhrase);
            throw new HttpRequestException(
                $"HTTP request failed with error: {response.StatusCode} - {response.ReasonPhrase}"
            );
        }

        // Log the operation.
        _ = CreateLogAsync(appConfig.AppId,
            "Delete User",
            $"User deleted successfully for the id {identifier}",
            LogType.Provision,
            LogSeverities.Information,
            correlationID);
    }

    private static HttpContent PrepareHttpContent(JObject payload, string? clientType, string? contentType)
    {
        if (string.Equals(contentType, "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
        {
            if (clientType == "ManageEngine SDP")
            {
                var login_name = payload["login_name"]?.ToString();
                if (login_name != null)
                {
                    var atIdx = login_name.IndexOf('@');
                    payload["login_name"] = atIdx > 0 ? login_name.Substring(0, atIdx) : login_name;
                }
            }

            var wrappedPayload = new JObject { ["user"] = payload };
            var encodedJson = Uri.EscapeDataString(wrappedPayload.ToString(Formatting.None));
            var formData = clientType == "ManageEngine SDP" ? $"input_data={encodedJson}" : encodedJson;
            return new StringContent(formData, Encoding.UTF8, "application/x-www-form-urlencoded");
        }

        // Default JSON
        return new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");
    }

    private async Task<HttpClient> CreateHttpClientAsync(AppConfig appConfig, SCIMDirections direction,
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

    private string? GetIdentifier(string responseString, string clientType)
    {
        if (string.IsNullOrWhiteSpace(responseString) || string.IsNullOrWhiteSpace(clientType))
            return null;

        var json = JObject.Parse(responseString);
        return clientType.ToLowerInvariant() switch
        {
            "manageengine sdp" => json["user"]?["id"]?.ToString() ?? string.Empty,
            _ => json["id"]?.ToString() ?? string.Empty
        };
    }

    private async Task CreateLogAsync(string appId, string eventInfo, string logMessage, LogType logType,
        LogSeverities logSeverity, string correlationID)
    {
        var logEntity = new CreateLogEntity(
            appId,
            logType.ToString(),
            logSeverity,
            eventInfo,
            logMessage,
            correlationID,
            AppConstant.LoggerName,
            DateTime.UtcNow,
            AppConstant.User,
            null,
            null
        );

        await _logger.CreateLogAsync(logEntity);
    }
}