using System;
using KN.KloudIdentity.Mapper.Common.Exceptions;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using Microsoft.SCIM;

namespace KN.KloudIdentity.Mapper.MapperCore.Outbound;

/// <summary>
/// Represents the base class for provisioning.
/// This contains all the common methods and properties for provisioning.
/// </summary>
public class ProvisioningBase : IProvisioningBase
{
    private readonly IGetFullAppConfigQuery _getFullAppConfigQuery;

    public ProvisioningBase(IGetFullAppConfigQuery getFullAppConfigQuery)
    {
        _getFullAppConfigQuery = getFullAppConfigQuery;
    }

    /// <summary>
    /// Executes custom logic asynchronously.
    /// </summary>
    /// <param name="payload">Payload to be sent to custom logic endpoint</param>
    /// <param name="appConfig">App configuration</param>
    /// <param name="correlationID">Correlation ID</param>
    /// <returns></returns>
    public virtual Task<dynamic> ExecuteCustomLogicAsync(dynamic payload, AppConfig appConfig, string correlationID)
    {
        if (appConfig.IsExternalAPIEnabled == false)
        {
            return Task.FromResult((object)payload);
        }

        throw new NotImplementedException();
    }

    /// <summary>
    /// Gets the application configuration asynchronously.
    /// </summary>
    /// <param name="appId">Application ID</param>
    /// <returns></returns>
    /// <exception cref="NotFoundException"></exception>
    public virtual async Task<AppConfig> GetAppConfigAsync(string appId)
    {
        var config = await _getFullAppConfigQuery.GetAsync(appId);

        return config ?? throw new NotFoundException($"App configuration not found for app ID {appId}.");
    }
}
