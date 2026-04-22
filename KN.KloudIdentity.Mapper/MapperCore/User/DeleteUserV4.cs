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
    private AppConfig _appConfig = null!;

    public async Task DeleteAsync(IResourceIdentifier resourceIdentifier, string appId, string correlationId)
    {
        ArgumentNullException.ThrowIfNull(resourceIdentifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        Log.Information(
            $"[DeleteUserV4] Execution started for user deletion. Identifier: {resourceIdentifier.Identifier}, AppId: {appId}, CorrelationID: {correlationId}");

        _appConfig = await GetAppConfigAsync(appId);

        if (_appConfig.IntegrationMethodOutbound == IntegrationMethods.REST)
            await ExecuteMultistepForRESTAsync(resourceIdentifier.Identifier, appId, correlationId);
        else
            await ExecuteGenericUserDeletionLogicAsync(resourceIdentifier.Identifier, appId, correlationId);

        Log.Information(
            $"User deleted successfully for the id {resourceIdentifier.Identifier}. AppId: {appId}, CorrelationID: {correlationId}");
        _ = CreateLogAsync(appId, resourceIdentifier.Identifier, correlationId);
    }

    protected virtual async Task ExecuteMultistepForRESTAsync(string identifier, string appId, string correlationId)
    {
        if (_appConfig.Actions == null || !_appConfig.Actions.Any())
            throw new InvalidOperationException("No action steps defined in application configuration.");

        var integrationOp = integrationBaseFactory.GetIntegration(_appConfig.IntegrationMethodOutbound ??
                                                                  IntegrationMethods.REST, appId) ??
                            throw new NotSupportedException(
                                $"Integration method {_appConfig.IntegrationMethodOutbound} is not supported.");

        var actionSteps = _appConfig.Actions
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
            await integrationOp.DeleteAsync(identifier, appId, _appConfig, actionStep, correlationId);
        }
    }

    protected virtual async Task ExecuteGenericUserDeletionLogicAsync(string identifier, string appId,
        string correlationId)
    {
        var integrationOp = integrationBaseFactory.GetIntegration(_appConfig.IntegrationMethodOutbound ??
                                                                  IntegrationMethods.REST, appId) ??
                            throw new NotSupportedException(
                                $"Integration method {_appConfig.IntegrationMethodOutbound} is not supported.");

        ValidateRequest(identifier, _appConfig);

        await integrationOp.DeleteAsync(identifier, _appConfig, correlationId);
    }

    private static void ValidateRequest(string identifier, AppConfig appConfig)
    {
        if (appConfig.IntegrationMethodOutbound == IntegrationMethods.AS400 ||
            appConfig.IntegrationMethodOutbound == IntegrationMethods.SQL)
            return;

        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentNullException(nameof(identifier), "Identifier cannot be null or empty");
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
