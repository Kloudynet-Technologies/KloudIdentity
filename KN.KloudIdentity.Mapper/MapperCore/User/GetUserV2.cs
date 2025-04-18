using System;
using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Common;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KloudIdentity.Mapper.MapperCore.Outbound;
using KN.KloudIdentity.Mapper.MapperCore.Outbound.CustomLogic;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.SCIM;
using Serilog;

namespace KN.KloudIdentity.Mapper.MapperCore.User;

public class GetUserV2 : ProvisioningBase, IGetResourceV2
{
    private readonly IList<IIntegrationBase> _integrations;
    private readonly IKloudIdentityLogger _logger;

    public GetUserV2(
        IGetFullAppConfigQuery getFullAppConfigQuery,
        IList<IIntegrationBase> integrations,
        IOutboundPayloadProcessor outboundPayloadProcessor,
        IKloudIdentityLogger logger) : base(getFullAppConfigQuery, outboundPayloadProcessor)
    {
        _integrations = integrations;
        _logger = logger;
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
        Log.Information(
            "Execution started for user retrieval. Identifier: {Identifier}, AppId: {AppId}, CorrelationID: {CorrelationID}",
            identifier, appId, correlationID);

        // Step 1: Get app config
        var appConfig = await GetAppConfigAsync(appId);
        
        // Resolve integration method operations
        var integrationOp =
            _integrations.FirstOrDefault(x => x.IntegrationMethod == appConfig.IntegrationMethodOutbound) ??
            throw new NotSupportedException(
                $"Integration method {appConfig.IntegrationMethodOutbound} is not supported.");

        // Step 2: Retrieve user
        var user = await integrationOp.GetAsync(identifier, appConfig, correlationID);
        Log.Information(
            "User retrieved successfully for Identifier: {Identifier}, AppId: {AppId}, CorrelationID: {CorrelationID}",
            identifier, appId, correlationID);

        // Log the operation.
        _ = CreateLogAsync(appConfig.AppId, identifier, correlationID);
        
        return user;
    }

    private async Task CreateLogAsync(string appId, string identifier, string correlationID)
    {
        var logEntity = new CreateLogEntity(
            appId,
            LogType.Read.ToString(),
            LogSeverities.Information,
            "Retrieve user",
            $"User retrieved successfully for the id {identifier}",
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