//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Authentication;
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

namespace KN.KloudIdentity.Mapper.MapperCore;

public class TungstenAPEssentialsIntegration : RESTIntegration
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppSettings _appSettings;
    private readonly IKloudIdentityLogger _logger;
    private readonly IConfiguration _config;

    private static readonly ConcurrentDictionary<string, (string Token, DateTime ExpiresAt)> _tokenCache = new();

    public TungstenAPEssentialsIntegration(
        IAuthContext authContext,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IOptions<AppSettings> appSettings,
        IKloudIdentityLogger logger)
        : base(authContext, httpClientFactory, configuration, appSettings, logger)
    {
        _httpClientFactory = httpClientFactory;
        _appSettings = appSettings.Value;
        _logger = logger;
        _config = configuration;
        IntegrationMethod = IntegrationMethods.REST;
    }

    public override async Task<dynamic> GetAuthenticationAsync(
        AppConfig config,
        SCIMDirections direction = SCIMDirections.Outbound,
        CancellationToken cancellationToken = default,
        params dynamic[] args)
    {
        // Return cached token if still valid
        if (_tokenCache.TryGetValue(config.AppId, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
        {
            Log.Debug("Returning cached ApsToken for appId {AppId}.", config.AppId);
            return cached.Token;
        }

        return await FetchAndCacheTokenAsync(config, cancellationToken);
    }

    protected new async Task<HttpClient> CreateHttpClientAsync(
        AppConfig appConfig,
        SCIMDirections direction,
        CancellationToken cancellationToken = default)
    {
        var token = await GetAuthenticationAsync(appConfig, direction, cancellationToken);
        var client = _httpClientFactory.CreateClient();

        // Tungsten requires: Authorization: ApsToken Value="<token>"
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("ApsToken", $"Value=\"{token}\"");

        // Apply fixed headers (x-rs-version, x-rs-culture, x-rs-uiculture) from AppIntegrationConfigs
        var customConfig = _appSettings.AppIntegrationConfigs?.FirstOrDefault(x => x.AppId == appConfig.AppId);
        if (customConfig?.HttpSettings?.Headers is { Count: > 0 })
        {
            client.SetCustomHeaders(customConfig.HttpSettings.Headers);
        }

        // x-rs-key may already be present via HttpSettings.Headers; only fall back to KI:Tungsten:ApiKey when absent
        if (!client.DefaultRequestHeaders.Contains("x-rs-key"))
            client.DefaultRequestHeaders.Add("x-rs-key", GetApiKey());

        return client;
    }

    public override async Task<Core2EnterpriseUser?> ProvisionAsync(
        dynamic payload,
        AppConfig appConfig,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        Log.Information("Tungsten user creation started. AppId: {AppId}, CorrelationID: {CorrelationID}",
            appConfig.AppId, correlationId);

        var userUri = appConfig.UserURIs?.FirstOrDefault()?.Post
            ?? throw new InvalidOperationException("User creation endpoint not configured.");

        var organizationId = GetOrganizationId(appConfig.AppId);

        JObject jPayload = payload as JObject ?? JObject.FromObject(payload);
        jPayload["OrganizationId"] = organizationId;

        // Extract UserName from payload — needed for GET-first upsert check
        var userName = jPayload["UserName"]?.ToString();
        if (string.IsNullOrWhiteSpace(userName))
            throw new InvalidOperationException("Payload must contain UserName for Tungsten provisioning.");

        // Upsert: check if user already exists in Tungsten before creating
        try
        {
            var existing = await GetAsync(userName, appConfig, correlationId, cancellationToken);
            if (existing != null)
            {
                Log.Information("Tungsten user already exists, updating instead. UserName: {UserName}, AppId: {AppId}, CorrelationID: {CorrelationID}",
                    userName, appConfig.AppId, correlationId);
                await ReplaceAsync(jPayload, new Core2EnterpriseUser { Identifier = userName }, appConfig, correlationId);
                return new Core2EnterpriseUser { Identifier = userName };
            }
        }
        catch (NotFoundException)
        {
            // User does not exist in Tungsten — proceed with create below
        }

        var httpClient = await CreateHttpClientAsync(appConfig, SCIMDirections.Outbound, cancellationToken);
        var content = new StringContent(jPayload.ToString(Formatting.None), System.Text.Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(userUri, content, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                InvalidateCache(appConfig.AppId);
            }

            Log.Error("Tungsten user creation failed. AppId: {AppId}, Status: {Status}, Response: {Response}",
                appConfig.AppId, response.StatusCode, responseBody);
            throw new System.Net.Http.HttpRequestException($"Error creating Tungsten user: {response.StatusCode} - {responseBody}");
        }

        var json = JObject.Parse(responseBody);
        userName = json["UserName"]?.ToString();

        if (string.IsNullOrWhiteSpace(userName))
        {
            Log.Error("Tungsten create response missing UserName. AppId: {AppId}, Response: {Response}",
                appConfig.AppId, responseBody);
            throw new InvalidOperationException("Tungsten user creation response did not contain a UserName.");
        }

        _ = Task.Run(async () =>
        {
            try
            {
                Log.Information("Tungsten user created successfully. UserName: {UserName}, AppId: {AppId}, CorrelationID: {CorrelationID}",
                    userName, appConfig.AppId, correlationId);

                await CreateLogAsync(appConfig.AppId, "Create User",
                    $"Tungsten user created successfully for UserName {userName}",
                    LogType.Provision, LogSeverities.Information, correlationId);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to write audit log after Tungsten user creation. AppId: {AppId}", appConfig.AppId);
            }
        }, cancellationToken);

        return new Core2EnterpriseUser { Identifier = userName };
    }

    public override async Task<Core2EnterpriseUser> GetAsync(
        string identifier,
        AppConfig appConfig,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var userUri = appConfig.UserURIs?.FirstOrDefault()?.Get
            ?? throw new InvalidOperationException("GET endpoint not configured.");

        var organizationId = GetOrganizationId(appConfig.AppId);

        var url = DynamicApiUrlUtil.GetFullUrl(userUri.ToString(), organizationId, identifier);
        var client = await CreateHttpClientAsync(appConfig, SCIMDirections.Outbound, cancellationToken);
        var response = await client.GetAsync(url, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new NotFoundException($"Tungsten user not found: {identifier}");
        }

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                InvalidateCache(appConfig.AppId);

            Log.Error("Tungsten GET failed. Identifier: {Identifier}, AppId: {AppId}, Status: {Status}",
                identifier, appConfig.AppId, response.StatusCode);
            throw new System.Net.Http.HttpRequestException($"Tungsten GET failed: {response.StatusCode}");
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var json = JObject.Parse(responseBody);

        _ = CreateLogAsync(appConfig.AppId, "Get User",
            $"Tungsten user retrieved successfully for UserName {identifier}",
            LogType.Read, LogSeverities.Information, correlationId);

        return new Core2EnterpriseUser
        {
            Identifier = identifier,
            // Store Tungsten GUID Id in ExternalId for use by Replace/Delete
            ExternalIdentifier = json["Id"]?.ToString()
        };
    }

    public override async Task ReplaceAsync(
        dynamic payload,
        Core2EnterpriseUser resource,
        AppConfig appConfig,
        string correlationId)
    {
        Log.Information("Tungsten user replace started. AppId: {AppId}, CorrelationID: {CorrelationID}",
            appConfig.AppId, correlationId);

        var userUri = appConfig.UserURIs?.FirstOrDefault()?.Put
            ?? throw new InvalidOperationException("PUT endpoint not configured.");

        var organizationId = GetOrganizationId(appConfig.AppId);

        // Resolve Tungsten GUID Id via GET (needed in PUT body)
        var existing = await GetAsync(resource.Identifier, appConfig, correlationId);
        var tungstenId = existing.ExternalIdentifier
            ?? throw new InvalidOperationException($"Could not resolve Tungsten Id for user: {resource.Identifier}");

        JObject jPayload = payload as JObject ?? JObject.FromObject(payload);
        jPayload["Id"] = tungstenId;
        jPayload["OrganizationId"] = organizationId;

        var httpClient = await CreateHttpClientAsync(appConfig, SCIMDirections.Outbound, CancellationToken.None);
        var content = new StringContent(jPayload.ToString(Formatting.None), System.Text.Encoding.UTF8, "application/json");
        var response = await httpClient.PutAsync(userUri, content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                InvalidateCache(appConfig.AppId);

            Log.Error("Tungsten user replace failed. AppId: {AppId}, Status: {Status}, Response: {Response}",
                appConfig.AppId, response.StatusCode, responseBody);
            throw new System.Net.Http.HttpRequestException($"Error replacing Tungsten user: {response.StatusCode} - {responseBody}");
        }

        _ = Task.Run(async () =>
        {
            try
            {
                Log.Information("Tungsten user replaced successfully. UserName: {UserName}, AppId: {AppId}, CorrelationID: {CorrelationID}",
                    resource.Identifier, appConfig.AppId, correlationId);

                await CreateLogAsync(appConfig.AppId, "Replace User",
                    $"Tungsten user replaced successfully for UserName {resource.Identifier}",
                    LogType.Provision, LogSeverities.Information, correlationId);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to write audit log after Tungsten user replace. AppId: {AppId}", appConfig.AppId);
            }
        });
    }

    public override async Task UpdateAsync(
        dynamic payload,
        Core2EnterpriseUser resource,
        AppConfig appConfig,
        string correlationId)
    {
        if (resource.UserName == null)
        {
            Log.Warning("UpdateAsync skipped for appId {AppId}: resource.UserName is null.", appConfig.AppId);
            return;
        }
        
        await ReplaceAsync(payload, resource, appConfig, correlationId);
    }

    public override async Task DeleteAsync(
        string identifier,
        AppConfig appConfig,
        string correlationId)
    {
        Log.Information("Tungsten user delete started. AppId: {AppId}, CorrelationID: {CorrelationID}",
            appConfig.AppId, correlationId);

        var userUri = appConfig.UserURIs?.FirstOrDefault()?.Delete
            ?? throw new InvalidOperationException("DELETE endpoint not configured.");

        // Resolve Tungsten GUID Id via GET
        var existing = await GetAsync(identifier, appConfig, correlationId);
        var tungstenId = existing.ExternalIdentifier
            ?? throw new InvalidOperationException($"Could not resolve Tungsten Id for user: {identifier}");

        var url = DynamicApiUrlUtil.GetFullUrl(userUri.ToString(), tungstenId);
        var httpClient = await CreateHttpClientAsync(appConfig, SCIMDirections.Outbound, CancellationToken.None);
        var response = await httpClient.DeleteAsync(url);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                InvalidateCache(appConfig.AppId);

            Log.Error("Tungsten user delete failed. AppId: {AppId}, Identifier: {Identifier}, Status: {Status}",
                appConfig.AppId, identifier, response.StatusCode);
            throw new System.Net.Http.HttpRequestException($"Error deleting Tungsten user: {response.StatusCode} - {responseBody}");
        }

        // DELETE returns 200 with the deleted entity body — log for audit
        Log.Information("Tungsten user deleted successfully. UserName: {UserName}, AppId: {AppId}, CorrelationID: {CorrelationID}",
            identifier, appConfig.AppId, correlationId);

        _ = CreateLogAsync(appConfig.AppId, "Delete User",
            $"Tungsten user deleted successfully for UserName {identifier}",
            LogType.Provision, LogSeverities.Information, correlationId);
    }

    private async Task<string> FetchAndCacheTokenAsync(AppConfig config, CancellationToken cancellationToken)
    {
        var basicAuth = JsonConvert.DeserializeObject<BasicAuthentication>(config.AuthenticationDetails.ToString());

        if (basicAuth is null || string.IsNullOrWhiteSpace(basicAuth.Username) || string.IsNullOrWhiteSpace(basicAuth.Password))
            throw new InvalidOperationException(
                $"Tungsten auth requires Username and Password in AuthenticationDetails (appId: {config.AppId}).");

        var baseUrl = config.UserURIs?.FirstOrDefault()?.BaseUrl?.TrimEnd('/')
            ?? throw new InvalidOperationException($"BaseUrl not configured in UserURIs for appId: {config.AppId}.");

        var authUrl = $"{baseUrl}/authentication/rest/authenticate";

        var requestBody = JsonConvert.SerializeObject(new
        {
            UserName = basicAuth.Username,
            Password = basicAuth.Password,
            AuthenticationType = 4
        });

        Log.Debug("Requesting Tungsten ApsToken for appId {AppId}.", config.AppId);

        var client = _httpClientFactory.CreateClient();

        // Apply fixed headers (x-rs-version, x-rs-culture, x-rs-uiculture) to the auth call too
        var customConfig = _appSettings.AppIntegrationConfigs?.FirstOrDefault(x => x.AppId == config.AppId);
        if (customConfig?.HttpSettings?.Headers is { Count: > 0 })
        {
            client.SetCustomHeaders(customConfig.HttpSettings.Headers);
        }

        // x-rs-key may already be present via HttpSettings.Headers; only fall back to KI:Tungsten:ApiKey when absent
        if (!client.DefaultRequestHeaders.Contains("x-rs-key"))
            client.DefaultRequestHeaders.Add("x-rs-key", GetApiKey());

        var response = await client.PostAsync(
            authUrl,
            new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json"),
            cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            Log.Error("Tungsten authentication failed. AppId: {AppId}, Status: {Status}, Response: {Response}",
                config.AppId, response.StatusCode, responseBody);
            throw new AuthenticationException(
                $"Tungsten authentication failed (appId: {config.AppId}). HTTP {(int)response.StatusCode}: {responseBody}");
        }

        var json = JObject.Parse(responseBody);
        var token = json["Token"]?.ToString();

        if (string.IsNullOrWhiteSpace(token))
            throw new AuthenticationException($"Tungsten returned no Token for appId: {config.AppId}.");

        _tokenCache[config.AppId] = (token, DateTime.UtcNow.AddMinutes(20));

        Log.Debug("Tungsten ApsToken cached for appId {AppId}, expires in 20 minutes.", config.AppId);
        return token;
    }

    private string GetOrganizationId(string appId)
    {
        return _appSettings.AppIntegrationConfigs?.FirstOrDefault(x => x.AppId == appId)?.OrganizationId
            ?? throw new InvalidOperationException($"OrganizationId not configured in AppIntegrationConfigs for appId: {appId}.");
    }

    private static void InvalidateCache(string appId)
    {
        _tokenCache.TryRemove(appId, out _);
        Log.Warning("Tungsten ApsToken cache invalidated for appId {AppId} due to 401.", appId);
    }

    private string GetApiKey()
    {
        return _config["KI:Tungsten:ApiKey"]
            ?? throw new InvalidOperationException("Tungsten API key (KI:Tungsten:ApiKey) not configured in App Configuration.");
    }
}
