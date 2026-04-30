using System.Web.Http;
using KN.KloudIdentity.Mapper.Common.Exceptions;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Itsm;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Domain.Messaging;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using Microsoft.SCIM;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace KN.KloudIdentity.Mapper.MapperCore;

/// <summary>
/// The DisconnectedIntegration class is intended for use with applications that lack user provisioning functionality.
/// </summary>
public class ITSMIntegration(
    IMetaverseIntegrationClient metaverseIntegrationClient
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
        EnrichPayloadWithMetadata(payload, appConfig);

        var result = await metaverseIntegrationClient.SendAsync<ItsmOperationResponse>(
            payload.ToString(),
            correlationId,
            ActionType.CreateUser,
            cancellationToken);
        
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
        var payload = new JObject
        {
            ["Identifier"] = identifier
        };

        EnrichPayloadWithMetadata(payload, appConfig);

        var response = await metaverseIntegrationClient.SendAsync<ItsmOperationResponse>(
            payload.ToString(),
            correlationId,
            ActionType.GetUser,
            cancellationToken);

        if (response.ExternalKey == null)
        {
            throw new HttpResponseException(System.Net.HttpStatusCode.NotFound);
        }
        
        return new Core2EnterpriseUser { Identifier = response.ExternalKey };
    }

    public async Task ReplaceAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig,
        string correlationId)
    {
        EnrichPayloadWithMetadata(payload, appConfig);

        var response = await metaverseIntegrationClient.SendAsync<ItsmOperationResponse>(
            payload.ToString(),
            correlationId,
            ActionType.EditUser,
            CancellationToken.None);
        // [To-DO] Develop later
    }

    public async Task UpdateAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig,
        string correlationId)
    {
        EnrichPayloadWithMetadata(payload, appConfig);
        
        if(resource.UserName == null) return;
        
        var response = await metaverseIntegrationClient.SendAsync<ItsmOperationResponse>(
            payload.ToString(),
            correlationId,
            ActionType.EditUser,
            CancellationToken.None);
        // [To-DO] Develop later
    }

    public async Task DeleteAsync(string identifier, AppConfig appConfig, string correlationId)
    {
        var payload = new JObject
        {
            ["Identifier"] = identifier
        };
        EnrichPayloadWithMetadata(payload, appConfig);
        var response = await metaverseIntegrationClient.SendAsync<ItsmOperationResponse>(
            payload.ToString(),
            correlationId,
            ActionType.DisableUser,
            CancellationToken.None);
        // [To-DO] Develop later
    }

    // Helper: safely read IntegrationDetails and merge AdditionalProperties into the payload

    private static void EnrichPayloadWithMetadata(dynamic payload, AppConfig appConfig)
    {
        var inDetails = appConfig.IntegrationDetails?.ToString();
        if (string.IsNullOrWhiteSpace(inDetails)) return;

        try
        {
            var details = JsonConvert.DeserializeObject<ItsmIntegrationMethod>(inDetails);

            if (payload is not JObject jObj) return;

            var extProps = details?.AdditionalProperties != null
                ? JObject.FromObject(details.AdditionalProperties)
                : new JObject();

            extProps["Identifier"] = jObj["Identifier"]?.ToString();
            jObj["ExtendedProperties"] = extProps;
            jObj["AppId"] = appConfig.AppId;
            jObj["TenantId"] = appConfig.TenantId;
        }
        catch
        {
            Log.Error(
                "Failed to parse IntegrationDetails for appId {AppId}. Ensure it is a valid JSON string matching ItsmIntegrationMethod structure.",
                appConfig.AppId);
            throw new ArgumentException(
                "Invalid IntegrationDetails format. Expected a JSON string matching ItsmIntegrationMethod structure.");
        }
    }

    #region Not Implemented: Action-based methods (not required for DisconnectedIntegration)

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