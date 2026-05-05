using System.Web.Http;
using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Common;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Itsm;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Domain.Messaging;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.SCIM;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace KN.KloudIdentity.Mapper.MapperCore;

/// <summary>
/// The ITSMIntegration class is intended for use with applications that lack user provisioning functionality.
/// </summary>
public class ITSMIntegration(
    IMetaverseIntegrationClient metaverseIntegrationClient,
    IKloudIdentityLogger logger
) : IIntegrationBaseV2
{
    public IntegrationMethods IntegrationMethod { get; init; } = IntegrationMethods.ITSM;

    public virtual async Task<dynamic> MapAndPreparePayloadAsync(IList<AttributeSchema> schema,
        Core2EnterpriseUser resource,
        CancellationToken cancellationToken = default)
    {
        var payload = JSONParserUtilV2<Resource>.Parse(schema, resource);
        if (!payload.ContainsKey("Identifier"))
        {
            payload["Identifier"] = resource.Identifier;
        }
        payload["ExtendedProperties"] ??= new JObject();
        payload["ExtendedProperties"]!["DisplayName"] = resource.DisplayName;
        payload["ExtendedProperties"]!["UserName"] = resource.UserName;

        return await Task.FromResult(payload);
    }

    /// <summary>
    /// Handles user provisioning through the pipeline when the application interacts with the internal metaverse service.
    /// Authentication is not required in this scenario.
    /// </summary>
    public async Task<dynamic> GetAuthenticationAsync(AppConfig config,
        SCIMDirections direction = SCIMDirections.Outbound,
        CancellationToken cancellationToken = default, params dynamic[] args)
    {
        return await Task.FromResult<object>(null!);
    }

    public async Task<Core2EnterpriseUser?> ProvisionAsync(dynamic payload, AppConfig appConfig, string correlationId,
        CancellationToken cancellationToken = default)
    {
        Log.Information("Creating ITSM ticket for tenantId {TenantId}, appId {AppId}, correlationId {CorrelationId}.", appConfig.TenantId, appConfig.AppId, correlationId);
        EnrichPayloadWithMetadata(payload, appConfig);

        var result = await metaverseIntegrationClient.SendAsync<ItsmOperationResponse>(
            payload.ToString(Formatting.None),
            correlationId,
            ActionType.CreateUser,
            cancellationToken);

        if(result.ExternalKey == null)
        {
            Log.Error("ITSM ticket creation failed for tenantId {TenantId}, appId {AppId}, correlationId {CorrelationId}. No ExternalKey returned.", appConfig.TenantId, appConfig.AppId, correlationId);
            throw new HttpResponseException(System.Net.HttpStatusCode.InternalServerError);
        }

        Log.Information("ITSM ticket created successfully for tenantId {TenantId}, appId {AppId}, correlationId {CorrelationId}. ExternalKey: {ExternalKey}.", appConfig.TenantId, appConfig.AppId, correlationId, result.ExternalKey);
        _ = CreateLogAsync(appConfig.AppId, "Create ITSM Ticket", $"ITSM ticket created successfully. ExternalKey: {result.ExternalKey}", LogType.Provision, LogSeverities.Information, correlationId);
        return new Core2EnterpriseUser { Identifier = result.ExternalKey };
    }

    public Task<(bool, string[])> ValidatePayloadAsync(dynamic payload, AppConfig appConfig, string correlationId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult((true, Array.Empty<string>()));
    }

    public async Task<Core2EnterpriseUser> GetAsync(string identifier, AppConfig appConfig, string correlationId,
        CancellationToken cancellationToken = default)
    {
        Log.Information("Fetching ITSM user for tenantId {TenantId}, appId {AppId}, identifier {Identifier}, correlationId {CorrelationId}.", appConfig.TenantId, appConfig.AppId, identifier, correlationId);

        var payload = new JObject
        {
            ["Identifier"] = identifier
        };

        EnrichPayloadWithMetadata(payload, appConfig);

        var response = await metaverseIntegrationClient.SendAsync<ItsmOperationResponse>(
            payload.ToString(Formatting.None),
            correlationId,
            ActionType.GetUser,
            cancellationToken);

        if (response.ExternalKey == null)
        {
            Log.Warning("ITSM user not found for tenantId {TenantId}, appId {AppId}, identifier {Identifier}, correlationId {CorrelationId}.", appConfig.TenantId, appConfig.AppId, identifier, correlationId);
            throw new HttpResponseException(System.Net.HttpStatusCode.NotFound);
        }

        Log.Information("ITSM user fetched successfully for tenantId {TenantId}, appId {AppId}, identifier {Identifier}.", appConfig.TenantId, appConfig.AppId, identifier);
        _ = CreateLogAsync(appConfig.AppId, "Get ITSM User", $"User retrieved successfully for identifier {identifier}", LogType.Read, LogSeverities.Information, correlationId);
        return new Core2EnterpriseUser { Identifier = response.ExternalKey };
    }

    public async Task ReplaceAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig,
        string correlationId)
    {
        Log.Information("Replacing ITSM user for tenantId {TenantId}, appId {AppId}, identifier {Identifier}, correlationId {CorrelationId}.", appConfig.TenantId, appConfig.AppId, resource.Identifier, correlationId);
        EnrichPayloadWithMetadata(payload, appConfig);

        var response = await metaverseIntegrationClient.SendAsync<ItsmOperationResponse>(
            payload.ToString(Formatting.None),
            correlationId,
            ActionType.EditUser,
            CancellationToken.None);

        if (response.ExternalKey == null)
        {
            Log.Error("ITSM user replace failed for tenantId {TenantId}, appId {AppId}, identifier {Identifier}, correlationId {CorrelationId}. No ExternalKey returned.", appConfig.TenantId, appConfig.AppId, resource.Identifier, correlationId);
            throw new HttpResponseException(System.Net.HttpStatusCode.InternalServerError);
        }

        Log.Information("ITSM user replaced successfully for tenantId {TenantId}, appId {AppId}, identifier {Identifier}.", appConfig.TenantId, appConfig.AppId, resource.Identifier);
        _ = CreateLogAsync(appConfig.AppId, "Replace ITSM User", $"User replaced successfully for identifier {resource.Identifier}", LogType.Edit, LogSeverities.Information, correlationId);
    }

    public async Task UpdateAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig,
        string correlationId)
    {
        Log.Information("Updating ITSM user for tenantId {TenantId}, appId {AppId}, identifier {Identifier}, correlationId {CorrelationId}.", appConfig.TenantId, appConfig.AppId, resource.Identifier, correlationId);
        EnrichPayloadWithMetadata(payload, appConfig);
        if (resource.UserName == null)
        {
            Log.Warning("UpdateAsync skipped for tenantId {TenantId}, appId {AppId}: resource.UserName is null.", appConfig.TenantId, appConfig.AppId);
            return;
        }

        var response = await metaverseIntegrationClient.SendAsync<ItsmOperationResponse>(
            payload.ToString(Formatting.None),
            correlationId,
            ActionType.EditUser,
            CancellationToken.None);

        if (response.ExternalKey == null)
        {
            Log.Error("ITSM user update failed for tenantId {TenantId}, appId {AppId}, identifier {Identifier}, correlationId {CorrelationId}. No ExternalKey returned.", appConfig.TenantId, appConfig.AppId, resource.Identifier, correlationId);
            throw new HttpResponseException(System.Net.HttpStatusCode.InternalServerError);
        }

        Log.Information("ITSM user updated successfully for tenantId {TenantId}, appId {AppId}, identifier {Identifier}.", appConfig.TenantId, appConfig.AppId, resource.Identifier);
        _ = CreateLogAsync(appConfig.AppId, "Update ITSM User", $"User updated successfully for identifier {resource.Identifier}", LogType.Edit, LogSeverities.Information, correlationId);
    }

    public async Task DeleteAsync(string identifier, AppConfig appConfig, string correlationId)
    {
        Log.Information("Disabling ITSM user for tenantId {TenantId}, appId {AppId}, identifier {Identifier}, correlationId {CorrelationId}.", appConfig.TenantId, appConfig.AppId, identifier, correlationId);

        var payload = new JObject
        {
            ["Identifier"] = identifier
        };
        EnrichPayloadWithMetadata(payload, appConfig);

        await metaverseIntegrationClient.SendAsync<ItsmOperationResponse>(
            payload.ToString(Formatting.None),
            correlationId,
            ActionType.DisableUser,
            CancellationToken.None);

        Log.Information("ITSM user disabled successfully for tenantId {TenantId}, appId {AppId}, identifier {Identifier}.", appConfig.TenantId, appConfig.AppId, identifier);
        _ = CreateLogAsync(appConfig.AppId, "Disable ITSM User", $"User disabled successfully for identifier {identifier}", LogType.Deprovision, LogSeverities.Information, correlationId);
    }

    private async Task CreateLogAsync(string appId, string eventInfo, string logMessage, LogType logType,
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

        await logger.CreateLogAsync(logEntity);
    }

    // Helper: safely read IntegrationDetails and merge AdditionalProperties into the payload

    private static void EnrichPayloadWithMetadata(dynamic payload, AppConfig appConfig)
    {
        if (payload is not JObject jObj) return;

        // Always stamp core fields regardless of IntegrationDetails presence
        jObj["AppId"] = appConfig.AppId;
        jObj["TenantId"] = appConfig.TenantId;
        var extProps = jObj["ExtendedProperties"] as JObject ?? new JObject();
        extProps["Identifier"] = jObj["Identifier"]?.ToString();
        jObj["ExtendedProperties"] = extProps;

        Log.Debug(
            "Payload stamped with AppId {AppId}, TenantId {TenantId}, Identifier {Identifier}.",
            appConfig.AppId, appConfig.TenantId, extProps["Identifier"]?.ToString());

        var inDetails = appConfig.IntegrationDetails?.ToString();
        if (string.IsNullOrWhiteSpace(inDetails))
        {
            Log.Warning(
                "IntegrationDetails is missing for appId {AppId}. AdditionalProperties will not be merged.",
                appConfig.AppId);
            return;
        }

        try
        {
            var details = JsonConvert.DeserializeObject<ItsmIntegrationMethod>(inDetails);

            var existing = jObj["ExtendedProperties"] as JObject ?? new JObject();
            var additionalProps = details?.AdditionalProperties != null
                ? JObject.FromObject(details.AdditionalProperties)
                : new JObject();
            additionalProps.Merge(existing, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Union });
            jObj["ExtendedProperties"] = additionalProps;
        }
        catch (Exception ex)
        {
            Log.Error(
                ex,
                "Failed to parse IntegrationDetails for appId {AppId}. Ensure it is a valid JSON string matching ItsmIntegrationMethod structure.",
                appConfig.AppId);
            throw new ArgumentException(
                "Invalid IntegrationDetails format. Expected a JSON string matching ItsmIntegrationMethod structure.", ex);
        }
    }

    #region Not Implemented: Action-based methods (not required for ITSM integration)

    public Task<Core2EnterpriseUser?> ProvisionAsync(dynamic payload, string appId, AppConfig appConfig,
        ActionStep actionStep, string correlationId,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Action-based provisioning is not supported for disconnected applications.");
    }

    public Task<Core2EnterpriseUser> GetAsync(string identifier, AppConfig appConfig, ActionStep actionStep,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Action-based retrieval is not supported for disconnected applications.");
    }

    public Task<Core2EnterpriseUser> ReplaceAsync(dynamic payload, Core2EnterpriseUser resource, string appId,
        AppConfig appConfig,
        ActionStep actionStep, string correlationId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Action-based replacement is not supported for disconnected applications.");
    }

    public Task UpdateAsync(dynamic payload, Core2EnterpriseUser resource, string appId, AppConfig appConfig,
        ActionStep actionStep, string correlationId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Action-based update is not supported for disconnected applications.");
    }

    public Task DeleteAsync(string identifier, string appId, AppConfig appConfig, ActionStep actionStep,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Action-based deletion is not supported for disconnected applications.");
    }

    #endregion
}