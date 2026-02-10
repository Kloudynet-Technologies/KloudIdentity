using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Common;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KloudIdentity.Mapper.MapperCore.Outbound;
using KN.KloudIdentity.Mapper.MapperCore.Outbound.CustomLogic;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.SCIM;
using Serilog;

namespace KN.KloudIdentity.Mapper.MapperCore.User;

public class UpdateUserV4(
    IGetFullAppConfigQuery getFullAppConfigQuery,
    IOutboundPayloadProcessor outboundPayloadProcessor,
    IKloudIdentityLogger logger,
    IHttpClientFactory httpClientFactory,
    IIntegrationBaseFactory integrationBaseFactory)
    : ProvisioningBase(getFullAppConfigQuery, outboundPayloadProcessor), IUpdateResourceV2
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IKloudIdentityLogger _logger = logger;

    public async Task UpdateAsync(IPatch patch, string appId, string correlationId)
    {
        ArgumentNullException.ThrowIfNull(patch);
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        
        Log.Information($"[UpdateUserV4] Execution started for user update. AppId: {appId}, CorrelationID: {correlationId}");
        
        // Step 1: Get app config
        var appConfig = await GetAppConfigAsync(appId);
        var integrationOp = integrationBaseFactory.GetIntegration(
            appConfig.IntegrationMethodOutbound ?? IntegrationMethods.REST,
            appId) ?? throw new NotSupportedException($"Integration method {appConfig.IntegrationMethodOutbound} is not supported.");
        
        if (patch.PatchRequest is not PatchRequest2 patchRequest)        {
            Log.Error($"[UpdateUserV4] Invalid patch request type. Expected PatchRequest2. AppId: {appId}, CorrelationID: {correlationId}");
            throw new ArgumentNullException(nameof(patchRequest));
        }   
        
        Core2EnterpriseUser user = new Core2EnterpriseUser();
        user.Apply(patchRequest);
        user.Identifier = patch.ResourceIdentifier.Identifier;
        
        // Step 2: Find the Update actions (multistep)
        var actionSteps = appConfig.Actions
            .Where(a => a is { ActionName: ActionNames.PATCH, ActionTarget: ActionTargets.USER })
            .SelectMany(a => a.ActionSteps)
            .OrderBy(s => s.StepOrder)
            .ToList();

        if (!actionSteps.Any())
        {
            throw new InvalidOperationException($"No PATCH actions for USER target found for AppId: {appId}");
        }

        foreach (var step in actionSteps)
        {
            var attributes = step.UserAttributeSchemas?.ToList() ?? [];
            var payload = await integrationOp.MapAndPreparePayloadAsync(attributes, user);
            Log.Information(
                "[UpdateUserV4] Payload mapped and prepared successfully for Identifier: {Identifier}, AppId: {AppId}, CorrelationID: {CorrelationID}",
                user.Identifier, appId, correlationId);
            
            var payloadValidationResult = await integrationOp.ValidatePayloadAsync(payload, appConfig, correlationId);
            if (!payloadValidationResult.Item1)
            {
                Log.Error($"[UpdateUserV4] Payload validation failed for ActionStep {step.StepOrder}, AppId: {appId}, CorrelationID: {correlationId}. Errors: {string.Join(", ", payloadValidationResult.Item2)}");
                throw new InvalidOperationException($"Payload validation failed: {string.Join(", ", payloadValidationResult.Item2)}");
            }
            
            // Execute custom logic
            payload = await ExecuteCustomLogicAsync(payload, appConfig, correlationId);

            // Step 4: Update user
            await integrationOp.UpdateAsync(payload, user, appId, appConfig, step, correlationId, CancellationToken.None);
            
            Log.Information(
                "[UpdateUserV4] User updated successfully for Identifier: {Identifier}, AppId: {AppId}, CorrelationID: {CorrelationID}",
                user.Identifier, appId, correlationId);
            
        }
        _ = CreateLogAsync(appConfig.AppId, user.Identifier, correlationId);
    }
    
    private async Task CreateLogAsync(string appId, string identifier, string correlationId)
    {
        var logEntity = new CreateLogEntity(
            appId,
            nameof(LogType.Edit),
            LogSeverities.Information,
            "Update user (V4)",
            $"User update successfully for the id {identifier}",
            correlationId,
            AppConstant.LoggerName,
            DateTime.UtcNow,
            AppConstant.User,
            null,
            null
        );

        await _logger.CreateLogAsync(logEntity);
    }
}