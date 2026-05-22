using KN.KloudIdentity.Mapper.Common.Exceptions;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Infrastructure.Persistence.Abstractions;
using KN.KloudIdentity.Mapper.MapperCore.Outbound.CustomLogic;
using Serilog;

namespace KN.KloudIdentity.Mapper.MapperCore.Outbound;

/// <summary>
/// Represents the base class for provisioning.
/// This contains all the common methods and properties for provisioning.
/// </summary>
public class ProvisioningBase(
    IAppConfigSnapshotRepository appConfigSnapshotRepository,
    IOutboundPayloadProcessor outboundPayloadProcessor
    )
    : IProvisioningBase
{
    /// <summary>
    /// Executes custom logic asynchronously.
    /// </summary>
    /// <param name="payload">Payload to be sent to custom logic endpoint</param>
    /// <param name="appConfig">App configuration</param>
    /// <param name="correlationID">Correlation ID</param>
    /// <returns></returns>
    public virtual async Task<dynamic> ExecuteCustomLogicAsync(dynamic payload, AppConfig appConfig,
        string correlationID)
    {
        if (appConfig is { IsExternalAPIEnabled: false or null })
        {
            return payload;
        }

        payload = await outboundPayloadProcessor.ProcessAsync(
            payload,
            appConfig.ExternalEndpointInfo,
            correlationID,
            CancellationToken.None);

        return payload;
    }

    /// <summary>
    /// Gets the application configuration for a specific tenant and application ID asynchronously.
    /// </summary>
    /// <param name="tenantId"></param>
    /// <param name="appId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="NotFoundException">Thrown when no application configuration exists for the specified tenant and application ID.</exception>
    public virtual async Task<AppConfig> GetAppConfigForTenantAsync(string tenantId, string appId, CancellationToken cancellationToken)
    {
        var config = await appConfigSnapshotRepository.GetAppConfigByAppIdAsync(tenantId, appId, cancellationToken);
        if (config == null)
            throw new NotFoundException($"ProvisioningBase: App configuration not found for tenant ID {tenantId} and app ID {appId}.");
        
        return config;
    }

    /// <summary>
    /// Gets the application configuration asynchronously.
    /// </summary>
    /// <param name="appId">Application ID</param>
    /// <returns></returns>
    /// <exception cref="NotFoundException"></exception>
    [Obsolete("Please use GetAppConfigForTenantAsync instead")]
    public virtual async Task<AppConfig> GetAppConfigAsync(string appId)
    {
        var config = await appConfigSnapshotRepository.GetAppConfigByAppIdAsync(appId);

        if (config == null)
        {
            Log.Error("App configuration not found for app ID {AppId}", appId);
            throw new NotFoundException($"App configuration not found for app ID {appId}.");
        }

        return config;
    }

}