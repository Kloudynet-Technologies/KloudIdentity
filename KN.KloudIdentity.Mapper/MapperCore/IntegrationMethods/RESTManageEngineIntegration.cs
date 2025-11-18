using System.Text;
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

namespace KN.KloudIdentity.Mapper.MapperCore;

public class RestIntegrationManageEngine : RESTIntegration
{
    private readonly IConfiguration _configuration;
    private readonly AppSettings _appSettings;
    private const string RoleField = "associated_roles";

    public RestIntegrationManageEngine(IAuthContext authContext, IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IKloudIdentityLogger logger,
        IOptions<AppSettings> appSettings) :
        base(authContext, httpClientFactory, configuration, appSettings, logger)
    {
        _configuration = configuration;
        IntegrationMethod = IntegrationMethods.REST;
        _appSettings = appSettings.Value;
    }

    public override async Task<dynamic> MapAndPreparePayloadAsync(IList<AttributeSchema> schema,
        Core2EnterpriseUser resource,
        CancellationToken cancellationToken = default)
    {
        var payload = await base.MapAndPreparePayloadAsync(schema, resource, cancellationToken);

        var loginName = payload["login_name"]?.ToString();
        if (loginName != null)
        {
            var atIdx = loginName.IndexOf('@');
            payload["login_name"] = atIdx > 0 ? loginName.Substring(0, atIdx) : loginName;
        }

        payload["associated_roles"] = resource.Roles != null
            ? JArray.FromObject(
                resource.Roles
                    .Where(x => x.Display != "User")
                    .Select(y => new { id = y.Value })
            )
            : new JArray(); // empty array if roles are null

        return await Task.FromResult(payload);
    }

    public override async Task<Core2EnterpriseUser?> ProvisionAsync(dynamic payload, AppConfig appConfig,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        Log.Information("Provisioning started for user creation. AppId: {AppId}, CorrelationID: {CorrelationID}",
            appConfig.AppId, correlationId);

        var userUri = appConfig.UserURIs?.FirstOrDefault()?.Post
                      ?? throw new InvalidOperationException("User creation endpoint not configured.");

        // Ensure the payload is JObject
        JObject jPayload = payload as JObject ?? JObject.FromObject(payload);

        // Get an auth token if required
        var httpClient = await CreateHttpClientAsync(appConfig, SCIMDirections.Outbound, CancellationToken.None);

        var content = PrepareHttpContent(jPayload);
        var response = await httpClient.PostAsync(userUri, content, cancellationToken);

        // Read the full response
        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            Log.Error(
                "Provisioning failed. AppId: {AppId}, CorrelationID: {CorrelationID}, StatusCode: {StatusCode}, Response: {ResponseBody}",
                appConfig.AppId, correlationId, response.StatusCode, responseBody);

            throw new HttpRequestException($"Error creating user: {response.StatusCode} - {responseBody}");
        }

        var json = JObject.Parse(responseBody);
        var idVal = json["user"]?["id"]?.ToString();
        if (idVal == null)
        {
            Log.Error(
                "User creation response did not contain a valid identifier. AppId: {AppId}, CorrelationID: {CorrelationID}, Response: {ResponseBody}",
                appConfig.AppId, correlationId, responseBody);
            throw new InvalidOperationException("User creation response did not contain a valid identifier.");
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

    public override async Task<Core2EnterpriseUser> GetAsync(string identifier, AppConfig appConfig,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var userUri = appConfig.UserURIs.FirstOrDefault()?.Get
                      ?? throw new ApplicationException("GET API not configured.");
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

            string urnPrefix = _configuration["urnPrefix"] ??
                               throw new InvalidOperationException("urnPrefix configuration is missing");

            string usernameField = GetFieldMapperValue(appConfig, "UserName", urnPrefix);

            core2EntUsr.Identifier = identifier;
            core2EntUsr.UserName = GetValueCaseInsensitive(user["user"] as JObject, usernameField);
            core2EntUsr.KIExtension.ExtensionAttribute3 =
                GetValueCaseInsensitive(user["user"] as JObject, "is_technician");

            // Create a log for the operation.
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

    public override async Task ReplaceAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig,
        string correlationId)
    {
        var userUrIs = appConfig.UserURIs?.FirstOrDefault()
                       ?? throw new InvalidOperationException("User update endpoint not configured.");
        var user = await GetAsync(resource.Identifier, appConfig, correlationId);
        var isTechnician = string.Equals(
            user.KIExtension.ExtensionAttribute3,
            "true",
            StringComparison.OrdinalIgnoreCase
        );
        // Ensure the payload is JObject
        JObject jPayload = payload as JObject ?? JObject.FromObject(payload);

        // Parse roles safely (if it’s a JSON array or already a JArray)
        var roles = payload[RoleField] switch
        {
            JArray arr => arr,
            string json when !string.IsNullOrEmpty(json) => JArray.Parse(json),
            _ => new JArray()
        };

        if (roles.Count > 0 && !isTechnician)
        {
            await ChangeAsTechnicianAsync(jPayload, resource, appConfig, correlationId);
        }

        // Get an auth token if required
        var httpClient = await CreateHttpClientAsync(appConfig, SCIMDirections.Outbound, CancellationToken.None);
        var content = PrepareHttpContent(jPayload);
        var apiPath = userUrIs.Put != null
            ? DynamicApiUrlUtil.GetFullUrl(userUrIs.Put.ToString(), resource.Identifier)
            : throw new InvalidOperationException("User update endpoint (PUT) not configured.");
        var response = await httpClient.PutAsync(apiPath, content); // x-www-form-urlencoded or other

        // Read the full response
        string responseBody = await response.Content.ReadAsStringAsync(CancellationToken.None);

        if (!response.IsSuccessStatusCode)
        {
            Log.Error(
                "Updating failed. AppId: {AppId}, CorrelationID: {CorrelationID}, StatusCode: {StatusCode}, Response: {ResponseBody}",
                appConfig.AppId, correlationId, response.StatusCode, responseBody);

            throw new HttpRequestException($"Error updating user: {response.StatusCode} - {responseBody}");
        }

        // Log the operation.
        _ = Task.Run(() =>
        {
            var urnPrefix = _configuration["urnPrefix"] ??
                            throw new InvalidOperationException("urnPrefix configuration is missing");
            var idField = GetFieldMapperValue(appConfig, "Identifier", urnPrefix);
            string? idVal = payload[idField]?.ToString() ?? resource.Identifier;

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

    public override async Task UpdateAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig,
        string correlationId)
    {
        await ReplaceAsync(payload, resource, appConfig, correlationId);
    }

    private async Task ChangeAsTechnicianAsync(JObject payload, Core2EnterpriseUser resource, AppConfig appConfig,
        string correlationId)
    {
        var appSetting = _appSettings.AppIntegrationConfigs.FirstOrDefault(x => x.AppId == appConfig.AppId);
        if (string.IsNullOrWhiteSpace(appSetting?.TechnicianUrl))
            throw new ApplicationException("Technician URL not configured.");

        // Get an auth token if required
        var httpClient = await CreateHttpClientAsync(appConfig, SCIMDirections.Outbound, CancellationToken.None);
        var content = PrepareHttpContent(payload, isTechnician: true);
        var apiPath = DynamicApiUrlUtil.GetFullUrl(appSetting.TechnicianUrl, resource.Identifier);
        var response = await httpClient.PutAsync(apiPath, content); // x-www-form-urlencoded or other

        // Read the full response
        var responseBody = await response.Content.ReadAsStringAsync(CancellationToken.None);

        if (!response.IsSuccessStatusCode)
        {
            Log.Error(
                "Change as Technician failed. AppId: {AppId}, CorrelationID: {CorrelationID}, StatusCode: {StatusCode}, Response: {ResponseBody}",
                appConfig.AppId, correlationId, response.StatusCode, responseBody);

            throw new HttpRequestException($"Error updating user: {response.StatusCode} - {responseBody}");
        }

        // Log the operation.
        _ = Task.Run(() =>
        {
            var idField = GetFieldMapperValue(appConfig, "Identifier", _configuration["urnPrefix"]!);
            string? idVal = payload[idField]!.ToString();

            Log.Information(
                "Change as Technician successful for the id {IdVal}. AppId: {AppId}, CorrelationID: {CorrelationID}",
                idVal, appConfig.AppId, correlationId);

            _ = CreateLogAsync(appConfig.AppId,
                "Change as Technician",
                $"Change as Technician successful for the id {idVal}",
                LogType.Edit,
                LogSeverities.Information,
                correlationId);
        });
    }

    private HttpContent PrepareHttpContent(JObject payload, bool isTechnician = false)
    {
        if (!isTechnician && payload is { } jObj &&
            jObj.TryGetValue(RoleField, out JToken? value) &&
            value is JArray rolesArray && !rolesArray.Any())
        {
            jObj.Remove(RoleField);
        }

        var wrappedPayload =
            isTechnician ? new JObject { ["technician"] = payload } : new JObject { ["user"] = payload };

        Log.Debug("Wrapped payload: {Payload}", wrappedPayload);
        var encodedJson = Uri.EscapeDataString(wrappedPayload.ToString(Formatting.None));
        var formData = $"input_data={encodedJson}";
        return new StringContent(formData, Encoding.UTF8, "application/x-www-form-urlencoded");
    }
}