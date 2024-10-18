using System;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KloudIdentity.Mapper.MapperCore.Outbound;
using KN.KloudIdentity.Mapper.MapperCore.Outbound.CustomLogic;
using Microsoft.SCIM;

namespace KN.KloudIdentity.Mapper.MapperCore.User;

public class GetUserV2 : ProvisioningBase, IGetResourceV2
{
    private readonly IList<IIntegrationBase> _integrations;

    public GetUserV2(
        IGetFullAppConfigQuery getFullAppConfigQuery,
        IOutboundPayloadProcessor outboundPayloadProcessor,
        IList<IIntegrationBase> integrations) : base(getFullAppConfigQuery, outboundPayloadProcessor)
    {
        _integrations = integrations;
    }

    /// <summary>
    /// Retrieves a user by identifier and application ID asynchronously.
    /// </summary>
    /// <param name="identifier">Unique identifier of the user</param>
    /// <param name="appId">Application ID</param>
    /// <param name="correlationID">Correlation ID</param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException">When integration method is not supported</exception>
    public async Task<Core2EnterpriseUser> GetAsync(string identifier, string appId, string correlationID)
    {
        // Step 1: Get app config
        var appConfig = await GetAppConfigAsync(appId);

        // Resolve integration method operations
        var integrationOp = _integrations.FirstOrDefault(x => x.IntegrationMethod == appConfig.IntegrationMethodOutbound) ??
                                throw new NotSupportedException($"Integration method {appConfig.IntegrationMethodOutbound} is not supported.");

        // Step 2: Retrieve user
        var user = await integrationOp.GetAsync(identifier, appConfig, correlationID);

        return user;
    }
}
