using System;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KloudIdentity.Mapper.MapperCore.Outbound;
using Microsoft.SCIM;

namespace KN.KloudIdentity.Mapper.MapperCore.User;

public class UpdateUserV2 : ProvisioningBase, IUpdateResourceV2
{
    private readonly IList<IIntegrationBase> _integrations;

    public UpdateUserV2(
        IGetFullAppConfigQuery getFullAppConfigQuery,
        IList<IIntegrationBase> integrations) : base(getFullAppConfigQuery)
    {
        _integrations = integrations;
    }

    /// <summary>
    /// Updates a user asynchronously.
    /// </summary>
    /// <param name="patch">The user to update.</param>
    /// <param name="appId">The ID of the application.</param>
    /// <param name="correlationID">The correlation ID.</param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException">When integration method is not supported</exception>
    public async Task UpdateAsync(IPatch patch, string appId, string correlationID)
    {
        if (patch.PatchRequest is not PatchRequest2 patchRequest)
        {
            throw new ArgumentNullException(nameof(patchRequest));
        }

        Core2EnterpriseUser user = new Core2EnterpriseUser();
        user.Apply(patchRequest);
        user.Identifier = patch.ResourceIdentifier.Identifier;

        // Step 1: Get app config
        var appConfig = await GetAppConfigAsync(appId);

        var integrationOp = _integrations.FirstOrDefault(x => x.IntegrationMethod == appConfig.IntegrationMethodOutbound) ??
                            throw new NotSupportedException($"Integration method {appConfig.IntegrationMethodOutbound} is not supported.");

        var attributes = appConfig.UserAttributeSchemas.Where(x => x.HttpRequestType == HttpRequestTypes.PATCH &&
        x.SCIMDirection == SCIMDirections.Outbound).ToList();

        // Step 2: Map and prepare payload
        var payload = await integrationOp.MapAndPreparePayloadAsync(attributes, user);

        // Step 3: Update user
        await integrationOp.UpdateAsync(payload, user, appConfig, correlationID);

        // await CreateLogAsync(appConfig, user.Identifier, correlationID);
    }
}