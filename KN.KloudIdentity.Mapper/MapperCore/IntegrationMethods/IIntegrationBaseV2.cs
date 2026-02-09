using System;
using KN.KloudIdentity.Mapper.Domain.Application;
using Microsoft.SCIM;

namespace KN.KloudIdentity.Mapper.MapperCore;

public interface IIntegrationBaseV2 : IIntegrationBase
{
    /// <summary>
    /// Provisions the user asynchronously to LOB application.
    /// </summary>
    /// <param name="payload">Payload to be provisioned to LOB app</param>
    /// <param name="appId">Application ID</param>
    /// <param name="appConfig">App configuration</param>
    /// <param name="actionStep">Action step</param>
    /// <param name="correlationId">Correlation ID</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Core2EnterpriseUser?> ProvisionAsync(dynamic payload, string appId, AppConfig appConfig, ActionStep actionStep, string correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a user by identifier using action-based configuration (multi-step, V4).
    /// </summary>
    /// <param name="identifier">Unique identifier of the user</param>
    /// <param name="appConfig">App configuration</param>
    /// <param name="actionStep">Action configuration containing steps</param>
    /// <param name="correlationId">Correlation ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Retrieved user</returns>
    Task<Core2EnterpriseUser> GetAsync(string identifier, AppConfig appConfig, ActionStep actionStep, string correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the user asynchronously in LOB application.
    /// </summary>
    /// <param name="payload">Payload to be sent for replacement</param>
    /// <param name="resource">User resource to be replaced</param>
    /// <param name="appId">Application ID</param>
    /// <param name="appConfig">App configuration</param>
    /// <param name="actionStep">Action step</param>
    /// <param name="correlationId">Correlation ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Replaced user</returns>
    Task<Core2EnterpriseUser> ReplaceAsync(dynamic payload, Core2EnterpriseUser resource, string appId, AppConfig appConfig, ActionStep actionStep, string correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the user asynchronously in LOB application.
    /// </summary>
    /// <param name="payload">Payload to be sent for update</param>
    /// <param name="resource">User resource to be updated</param>
    /// <param name="appId">Application ID</param>
    /// <param name="appConfig">App configuration</param>
    /// <param name="actionStep">Action step</param>
    /// <param name="correlationId">Correlation ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns></returns>
    Task UpdateAsync(dynamic payload, Core2EnterpriseUser resource, string appId, AppConfig appConfig, ActionStep actionStep, string correlationId, CancellationToken cancellationToken = default);
}
