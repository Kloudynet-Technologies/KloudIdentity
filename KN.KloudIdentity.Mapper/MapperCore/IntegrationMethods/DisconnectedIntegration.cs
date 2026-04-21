using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using Microsoft.SCIM;

namespace KN.KloudIdentity.Mapper.MapperCore;

/// <summary>
/// The DisconnectedIntegration class is intended for use with applications that lack user provisioning functionality.
/// </summary>
public class DisconnectedIntegration(
    IMetaverseIntegrationClient metaverseIntegrationClient
) : IIntegrationBaseV2
{
    public IntegrationMethods IntegrationMethod { get; init; } = IntegrationMethods.ITSM;

    public virtual async Task<dynamic> MapAndPreparePayloadAsync(IList<AttributeSchema> schema,
        Core2EnterpriseUser resource,
        CancellationToken cancellationToken = default)
    {
        var payload = JSONParserUtilV2<Resource>.Parse(schema, resource);
        if (!payload.ContainsKey("identifier"))
        {
            payload["identifier"] = resource.Identifier;
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
        var result = await metaverseIntegrationClient.CreateAsync<object>(
            appConfig.TenantId,
            appConfig.AppId,
            payload,
            correlationId,
            cancellationToken);

        // [To-DO] Develop later
        return new Core2EnterpriseUser { Identifier = payload.identifier };
    }

    public Task<(bool, string[])> ValidatePayloadAsync(dynamic payload, AppConfig appConfig, string correlationId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult((true, Array.Empty<string>()));
    }

    public async Task<Core2EnterpriseUser> GetAsync(string identifier, AppConfig appConfig, string correlationId,
        CancellationToken cancellationToken = default)
    {
        var response = await metaverseIntegrationClient.GetAsync<object>(appConfig.TenantId,
            appConfig.AppId,
            identifier,
            correlationId,
            cancellationToken);

        // [To-DO] Develop later
        return new Core2EnterpriseUser { Identifier = identifier };
    }

    public async Task ReplaceAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig,
        string correlationId)
    {
        var response = await metaverseIntegrationClient.ReplaceAsync<object>(appConfig.TenantId,
            appConfig.AppId,
            resource.Identifier,
            payload,
            correlationId);
        // [To-DO] Develop later
    }

    public async Task UpdateAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig,
        string correlationId)
    {
        var response = await metaverseIntegrationClient.UpdateAsync<object>(appConfig.TenantId,
            appConfig.AppId,
            resource.Identifier,
            payload,
            correlationId);

        // [To-DO] Develop later
    }

    public async Task DeleteAsync(string identifier, AppConfig appConfig, string correlationId)
    {
        var response = await metaverseIntegrationClient.DeleteAsync<object>(appConfig.TenantId,
            appConfig.AppId,
            identifier,
            correlationId);
        //[To-DO] Develop later
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