using System;
using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Common;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KloudIdentity.Mapper.MapperCore.Outbound;
using KN.KloudIdentity.Mapper.MapperCore.Outbound.CustomLogic;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.SCIM;

namespace KN.KloudIdentity.Mapper.MapperCore.User;

public class UpdateUserV2 : ProvisioningBase, IUpdateResourceV2
{
    private readonly IList<IIntegrationBase> _integrations;
    private readonly IKloudIdentityLogger _logger;

    public UpdateUserV2(
        IGetFullAppConfigQuery getFullAppConfigQuery,
        IList<IIntegrationBase> integrations,
        IOutboundPayloadProcessor outboundPayloadProcessor,
        IKloudIdentityLogger logger) : base(getFullAppConfigQuery, outboundPayloadProcessor)
    {
        _integrations = integrations;
        _logger = logger;
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

        _ = CreateLogAsync(appConfig.AppId, user.Identifier, correlationID);
    }

    private async Task CreateLogAsync(string appId, string identifier, string correlationID)
    {
        var logEntity = new CreateLogEntity(
            appId,
            LogType.Edit.ToString(),
            LogSeverities.Information,
            "Update user",
            $"User updated successfully for the id {identifier}",
            correlationID,
            AppConstant.LoggerName,
            DateTime.UtcNow,
            AppConstant.User,
            null,
            null
        );

        await _logger.CreateLogAsync(logEntity);
    }
}