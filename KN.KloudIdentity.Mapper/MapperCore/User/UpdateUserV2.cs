using System;
using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Common;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KloudIdentity.Mapper.MapperCore.Outbound;
using KN.KloudIdentity.Mapper.MapperCore.Outbound.CustomLogic;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.SCIM;
using Serilog;

namespace KN.KloudIdentity.Mapper.MapperCore.User;

public class UpdateUserV2 : ProvisioningBase, IUpdateResourceV2
{
    private readonly IIntegrationBaseFactory _integrationBaseFactory;
    private readonly IKloudIdentityLogger _logger;

    public UpdateUserV2(
        IGetFullAppConfigQuery getFullAppConfigQuery,
        IIntegrationBaseFactory integrationBaseFactory,
        IOutboundPayloadProcessor outboundPayloadProcessor,
        IKloudIdentityLogger logger) : base(getFullAppConfigQuery, outboundPayloadProcessor)
    {
        _integrationBaseFactory = integrationBaseFactory;
        _logger = logger;
    }

    /// <summary>
    /// Updates a user asynchronously.
    /// </summary>
    /// <param name="patch">The user to update.</param>
    /// <param name="appId">The ID of the application.</param>
    /// <param name="correlationId">The correlation ID.</param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException">When integration method is not supported</exception>
    public async Task UpdateAsync(IPatch patch, string appId, string correlationId)
    {
        Log.Information("Execution started for user update. AppId: {AppId}, CorrelationID: {CorrelationID}", appId,
            correlationId);
        if (patch.PatchRequest is not PatchRequest2 patchRequest)
        {
            Log.Error(
                "Invalid patch request type. Expected PatchRequest2. AppId: {AppId}, CorrelationID: {CorrelationID}",
                appId, correlationId);
            throw new ArgumentNullException(nameof(patchRequest));
        }

        Core2EnterpriseUser user = new Core2EnterpriseUser();
        user.Apply(patchRequest);
        user.Identifier = patch.ResourceIdentifier.Identifier;

        // Step 1: Get app config
        var appConfig = await GetAppConfigAsync(appId);

        var integrationOp =
            _integrationBaseFactory.GetIntegration(appConfig.IntegrationMethodOutbound ?? IntegrationMethods.REST,
                appId) ??
            throw new NotSupportedException(
                $"Integration method {appConfig.IntegrationMethodOutbound} is not supported.");

        var attributes = GetUserAttributes(appConfig.UserAttributeSchemas, appConfig.IntegrationMethodOutbound);

        // Step 2: Map and prepare payload
        var payload = await integrationOp.MapAndPreparePayloadAsync(attributes, user);
        Log.Information(
            "Payload mapped and prepared successfully for Identifier: {Identifier}, AppId: {AppId}, CorrelationID: {CorrelationID}",
            user.Identifier, appId, correlationId);

        // Step 3: Update user
        await integrationOp.UpdateAsync(payload, user, appConfig, correlationId);
        Log.Information(
            "User updated successfully for Identifier: {Identifier}, AppId: {AppId}, CorrelationID: {CorrelationID}",
            user.Identifier, appId, correlationId);

        _ = CreateLogAsync(appConfig.AppId, user.Identifier, correlationId);
    }

    private IList<AttributeSchema> GetUserAttributes(ICollection<AttributeSchema> userAttributeSchemas,
        IntegrationMethods? integrationMethodOutbound)
    {
        switch (integrationMethodOutbound)
        {
            case IntegrationMethods.REST:
                var patchAttrs = userAttributeSchemas.Where(x => x.HttpRequestType == HttpRequestTypes.PATCH).ToList();
                return patchAttrs.Count != 0 ? patchAttrs : userAttributeSchemas.Where(x => x.HttpRequestType == HttpRequestTypes.PUT).ToList();

            case IntegrationMethods.SQL:
                return userAttributeSchemas.Where(x => x.HttpRequestType == HttpRequestTypes.PATCH).ToList();
            default:
            case IntegrationMethods.Linux:
                return userAttributeSchemas.ToList();
        }
    }

    private async Task CreateLogAsync(string appId, string identifier, string correlationID)
    {
        var logEntity = new CreateLogEntity(
            appId,
            nameof(LogType.Edit),
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