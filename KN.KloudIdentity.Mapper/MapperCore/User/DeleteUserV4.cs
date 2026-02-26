using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Common;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KloudIdentity.Mapper.Infrastructure.Persistence.Abstractions;
using KN.KloudIdentity.Mapper.MapperCore.Outbound;
using KN.KloudIdentity.Mapper.MapperCore.Outbound.CustomLogic;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.SCIM;
using Serilog;

namespace KN.KloudIdentity.Mapper.MapperCore.User;

public class DeleteUserV4(
    IAppConfigSnapshotRepository snapshotRepository,
    IOutboundPayloadProcessor outboundPayloadProcessor,
    IIntegrationBaseFactory integrationBaseFactory,
    IKloudIdentityLogger logger
)
    : ProvisioningBase(snapshotRepository, outboundPayloadProcessor), IDeleteResourceV2
{
    public async Task DeleteAsync(IResourceIdentifier resourceIdentifier, string appId, string correlationId)
    {
        ArgumentNullException.ThrowIfNull(resourceIdentifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        Log.Information(
            $"[DeleteUserV4] Execution started for user deletion. Identifier: {resourceIdentifier.Identifier}, AppId: {appId}, CorrelationID: {correlationId}");

        var appConfig = await GetAppConfigAsync(appId);
        if (appConfig.Actions == null || !appConfig.Actions.Any())
            throw new InvalidOperationException("No action steps defined in application configuration.");
        var integrationOp = integrationBaseFactory.GetIntegration(appConfig.IntegrationMethodOutbound ??
                                                                  IntegrationMethods.REST, appId) ??
                            throw new NotSupportedException(
                                $"Integration method {appConfig.IntegrationMethodOutbound} is not supported.");

        var actionSteps = appConfig.Actions
            .Where(a => a is { ActionName: ActionNames.DELETE, ActionTarget: ActionTargets.USER })
            .SelectMany(a => a.ActionSteps)
            .OrderBy(s => s.StepOrder)
            .ToList();

        if (actionSteps.Count == 0)
            throw new InvalidOperationException("No action steps found for user DELETE operation. AppId: " + appId);

        foreach (var actionStep in actionSteps)
        {
            Log.Information("Processing ActionStep {StepOrder} with HttpVerb {HttpVerb}", actionStep.StepOrder,
                actionStep.HttpVerb);
            await integrationOp.DeleteAsync(resourceIdentifier.Identifier, appId, appConfig, actionStep, correlationId);
        }

        Log.Information(
            $"User deleted successfully for the id {resourceIdentifier.Identifier}. AppId: {appId}, CorrelationID: {correlationId}");
        _ = CreateLogAsync(appId, resourceIdentifier.Identifier, correlationId);
    }

    private async Task CreateLogAsync(string appId, string identifier, string correlationId)
    {
        var logEntity = new CreateLogEntity(
            appId,
            nameof(LogType.Delete),
            LogSeverities.Information,
            "Delete user (V4)",
            $"User deleted successfully for the id {identifier}",
            correlationId,
            AppConstant.LoggerName,
            DateTime.UtcNow,
            AppConstant.User,
            null,
            null
        );

        await logger.CreateLogAsync(logEntity);
    }
}