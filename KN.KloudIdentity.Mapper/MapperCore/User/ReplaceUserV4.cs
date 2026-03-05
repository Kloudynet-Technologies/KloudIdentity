using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Common;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Infrastructure.Persistence.Abstractions;
using KN.KloudIdentity.Mapper.MapperCore.Outbound;
using KN.KloudIdentity.Mapper.MapperCore.Outbound.CustomLogic;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.SCIM;
using Serilog;

namespace KN.KloudIdentity.Mapper.MapperCore.User;

public class ReplaceUserV4(
    IAppConfigSnapshotRepository snapshotRepository,
    IKloudIdentityLogger logger,
    IIntegrationBaseFactory integrationBaseFactory,
    IOutboundPayloadProcessor outboundPayloadProcessor)
    : ProvisioningBase(snapshotRepository, outboundPayloadProcessor), IReplaceResourceV2
{
    public virtual async Task ReplaceAsync(Core2EnterpriseUser resource, string appId, string correlationID)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationID);

        Log.Information($"[ReplaceUserV4] Execution started for user replacement. AppId: {appId}, CorrelationID: {correlationID}, Identifier: {resource.Identifier}");

        // Step 1: Get app config
        var appConfig = await GetAppConfigAsync(appId);

        // Step 2: Find the Replace actions (multi-step)
        var actionSteps = appConfig.Actions
            .Where(a => a.ActionName == ActionNames.EDIT && a.ActionTarget == ActionTargets.USER)
            .SelectMany(a => a.ActionSteps)
            .OrderBy(s => s.StepOrder)
            .ToList();

        if (actionSteps.Count == 0)
        {
            throw new InvalidOperationException($"No EDIT actions for USER target found for AppId: {appId}");
        }

        // Step 3: Get integration operator
        var integrationOp = integrationBaseFactory.GetIntegration(
            appConfig.IntegrationMethodOutbound ?? IntegrationMethods.REST,
            appId
        ) ?? throw new NotSupportedException($"Integration method {appConfig.IntegrationMethodOutbound} is not supported.");

        // Step 4: For each action step, map and send the payload
        foreach (var step in actionSteps)
        {
            Log.Information($"[ReplaceUserV4] Processing ActionStep {step.StepOrder} with HttpVerb {step.HttpVerb}");

            var attributes = step.UserAttributeSchemas?.ToList() ?? [];

            // Map the user resource to the outbound payload
            var payload = await integrationOp.MapAndPreparePayloadAsync(attributes, resource);
            Log.Information($"[ReplaceUserV4] Payload mapped and prepared successfully for ActionStep {step.StepOrder}, AppId: {appId}, CorrelationID: {correlationID}");

            var payloadValidationResult = await integrationOp.ValidatePayloadAsync(payload, appConfig, correlationID);
            if (!payloadValidationResult.Item1)
            {
                Log.Error($"[ReplaceUserV4] Payload validation failed for ActionStep {step.StepOrder}, AppId: {appId}, CorrelationID: {correlationID}. Errors: {string.Join(", ", payloadValidationResult.Item2)}");
                throw new InvalidOperationException($"Payload validation failed: {string.Join(", ", payloadValidationResult.Item2)}");
            }

            // Call the integration method's ReplaceAsync
            var result = await integrationOp.ReplaceAsync(payload, resource, appConfig.AppId, appConfig, step, correlationID, CancellationToken.None);
            resource.Identifier = result.Identifier; // Update identifier if changed

            Log.Information($"[ReplaceUserV4] Successfully processed ActionStep {step.StepOrder} for user {resource.Identifier}");
        }

        _ = CreateLogAsync(appConfig.AppId, resource.Identifier, correlationID);
    }

    private async Task CreateLogAsync(string appId, string identifier, string correlationID)
    {
        var logEntity = new CreateLogEntity(
            appId,
            nameof(LogType.Edit),
            LogSeverities.Information,
            "Replace user (V4)",
            $"User replaced successfully for the id {identifier}",
            correlationID,
            AppConstant.LoggerName,
            DateTime.UtcNow,
            AppConstant.User,
            null,
            null
        );

        await logger.CreateLogAsync(logEntity);
    }
}
