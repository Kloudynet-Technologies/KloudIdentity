using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Common;
using KN.KloudIdentity.Mapper.Common.Exceptions;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Infrastructure.Persistence.Abstractions;
using KN.KloudIdentity.Mapper.MapperCore.Outbound;
using KN.KloudIdentity.Mapper.MapperCore.Outbound.CustomLogic;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.SCIM;
using Newtonsoft.Json;
using Serilog;

namespace KN.KloudIdentity.Mapper.MapperCore.User;

public class UpdateUserV4(
    IAppConfigSnapshotRepository snapshotRepository,
    IOutboundPayloadProcessor outboundPayloadProcessor,
    IKloudIdentityLogger logger,
    IIntegrationBaseFactory integrationBaseFactory,
    ITenantContext tenantContext
    )
    : ProvisioningBase(snapshotRepository, outboundPayloadProcessor), IUpdateResourceV2
{
    private AppConfig _appConfig = null!;

    public async Task UpdateAsync(IPatch patch, string appId, string correlationId)
    {
        ArgumentNullException.ThrowIfNull(patch);
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        Log.Information($"[UpdateUserV4] Execution started for user update. AppId: {appId}, CorrelationID: {correlationId}");

        // Step 1: Get app config
        _appConfig = await GetAppConfigForTenantAsync(tenantContext.TenantId, appId, CancellationToken.None);

        if (patch.PatchRequest is not PatchRequest2 patchRequest)
        {
            var actualType = patch.PatchRequest?.GetType().FullName ?? "null";
            Log.Error($"[UpdateUserV4] Invalid edit request type. Expected {nameof(PatchRequest2)} but received {actualType}. AppId: {appId}, CorrelationID: {correlationId}");
            throw new NotSupportedException(
                $"Unsupported patch request type '{actualType}' for argument '{nameof(patch)}'. Expected '{typeof(PatchRequest2).FullName}'.");
        }

        Core2EnterpriseUser user = new Core2EnterpriseUser();
        user.Apply(patchRequest);
        user.Identifier = patch.ResourceIdentifier.Identifier;

        if (_appConfig.IntegrationMethodOutbound == IntegrationMethods.REST || _appConfig.IntegrationMethodOutbound == IntegrationMethods.SOAP || _appConfig.IntegrationMethodOutbound == IntegrationMethods.SOAPEagle)
            await ExecuteMultistepForRESTAsync(user, appId, correlationId);
        else
            await ExecuteGenericUserUpdateLogicAsync(user, appId, correlationId);

        _ = CreateLogAsync(_appConfig.AppId, user.Identifier, correlationId);
    }

    protected virtual async Task ExecuteMultistepForRESTAsync(Core2EnterpriseUser user, string appId, string correlationId)
    {
        var integrationOp = integrationBaseFactory.GetIntegration(
            _appConfig.IntegrationMethodOutbound ?? IntegrationMethods.REST,
            appId) ?? throw new NotSupportedException($"Integration method {_appConfig.IntegrationMethodOutbound} is not supported.");

        // Step 2: Find the Update actions (multistep)
        var actionSteps = _appConfig.Actions?
            .Where(a => a is { ActionName: ActionNames.EDIT, ActionTarget: ActionTargets.USER })
            .SelectMany(a => a.ActionSteps)
            .OrderBy(s => s.StepOrder)
            .ToList()
            ?? [];

        if (!actionSteps.Any())
        {
            throw new InvalidOperationException($"No PATCH actions for USER target found for AppId: {appId}");
        }

        foreach (var step in actionSteps)
        {
            var attributes = step.UserAttributeSchemas?.ToList() ?? [];

            var payload = (_appConfig.IntegrationMethodOutbound == IntegrationMethods.SOAP
                           || _appConfig.IntegrationMethodOutbound == IntegrationMethods.SOAPEagle)
                ? await integrationOp.MapAndPreparePayloadAsync(attributes, user, _appConfig, step, CancellationToken.None)
                : await integrationOp.MapAndPreparePayloadAsync(attributes, user, _appConfig);
            Log.Information(
                "[UpdateUserV4] Payload mapped and prepared successfully for Identifier: {Identifier}, AppId: {AppId}, CorrelationID: {CorrelationID}",
                user.Identifier, appId, correlationId);

            var payloadValidationResult = await integrationOp.ValidatePayloadAsync(payload, _appConfig, correlationId);
            if (!payloadValidationResult.Item1)
            {
                Log.Error($"[UpdateUserV4] Payload validation failed for ActionStep {step.StepOrder}, AppId: {appId}, CorrelationID: {correlationId}. Errors: {string.Join(", ", payloadValidationResult.Item2)}");
                throw new PayloadValidationException(appId, payloadValidationResult.Item2);
            }

            // Execute custom logic
            payload = await ExecuteCustomLogicAsync(payload, _appConfig, correlationId);

            // Step 4: Update user
            await integrationOp.UpdateAsync(payload, user, appId, _appConfig, step, correlationId, CancellationToken.None);

            Log.Information(
                "[UpdateUserV4] User updated successfully for Identifier: {Identifier}, AppId: {AppId}, CorrelationID: {CorrelationID}",
                user.Identifier, appId, correlationId);
        }
    }

    protected virtual async Task ExecuteGenericUserUpdateLogicAsync(Core2EnterpriseUser user, string appId, string correlationId)
    {
        var integrationOp = integrationBaseFactory.GetIntegration(
            _appConfig.IntegrationMethodOutbound ?? IntegrationMethods.REST,
            appId) ?? throw new NotSupportedException($"Integration method {_appConfig.IntegrationMethodOutbound} is not supported.");

        var userAttributes = GetUserAttributes(_appConfig.UserAttributeSchemas, _appConfig.IntegrationMethodOutbound);

        var payload = await integrationOp.MapAndPreparePayloadAsync(userAttributes, user, _appConfig);
        
        Log.Information(
            "[UpdateUserV4] Payload mapped and prepared successfully for AppId: {AppId}, CorrelationID: {CorrelationID}, Payload: {Payload}",
            appId, correlationId, JsonConvert.SerializeObject(payload));

        var payloadValidationResult = await integrationOp.ValidatePayloadAsync(payload, _appConfig, correlationId);
        if (!payloadValidationResult.Item1)
        {
            Log.Error("[UpdateUserV4] Payload validation failed. AppId: {AppId}, CorrelationID: {CorrelationID}, Error: {Error}",
                appId, correlationId, payloadValidationResult.Item2);
            throw new PayloadValidationException(appId, payloadValidationResult.Item2);
        }

        payload = await ExecuteCustomLogicAsync(payload, _appConfig, correlationId);

        await integrationOp.UpdateAsync(payload, user, _appConfig, correlationId);

        Log.Information(
            "[UpdateUserV4] User updated successfully for Identifier: {Identifier}, AppId: {AppId}, CorrelationID: {CorrelationID}",
            user.Identifier, appId, correlationId);
    }

    private IList<AttributeSchema> GetUserAttributes(ICollection<AttributeSchema> userAttributeSchemas,
        IntegrationMethods? integrationMethodOutbound)
    {
        if (userAttributeSchemas == null)
            return new List<AttributeSchema>();

        switch (integrationMethodOutbound)
        {
            case IntegrationMethods.REST:
            case IntegrationMethods.SQL:
            case IntegrationMethods.SOAPEagle:
                var patchAttrs = userAttributeSchemas
                    .Where(x => x.HttpRequestType == HttpRequestTypes.PATCH)
                    .ToList();

                return patchAttrs.Count != 0
                    ? patchAttrs
                    : userAttributeSchemas.Where(x => x.HttpRequestType == HttpRequestTypes.PUT).ToList();

            case IntegrationMethods.ITSM:
                var providerUrlId = _appConfig.ItsmConfigurations.ServiceProviderUrls
                    .FirstOrDefault(x => x.ActionName == ActionNames.EDIT)?.Id;
                
                return userAttributeSchemas
                    .Where(x => x.ServiceProviderUrlId == providerUrlId)
                    .ToList();

            default:
                return userAttributeSchemas.ToList();
        }
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

        await logger.CreateLogAsync(logEntity);
    }
}
