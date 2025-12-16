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
}
