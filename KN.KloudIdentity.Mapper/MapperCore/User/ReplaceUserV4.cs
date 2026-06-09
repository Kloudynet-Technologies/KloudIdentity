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

public class ReplaceUserV4(
    IAppConfigSnapshotRepository snapshotRepository,
    IKloudIdentityLogger logger,
    IIntegrationBaseFactory integrationBaseFactory,
    IOutboundPayloadProcessor outboundPayloadProcessor,
    ITenantContext tenantContext
    )
    : ProvisioningBase(snapshotRepository, outboundPayloadProcessor), IReplaceResourceV2
{
    private AppConfig _appConfig = null!;

    public virtual async Task ReplaceAsync(Core2EnterpriseUser resource, string appId, string correlationID)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationID);

        Log.Information($"[ReplaceUserV4] Execution started for user replacement. AppId: {appId}, CorrelationID: {correlationID}, Identifier: {resource.Identifier}");

        // Step 1: Get app config
        _appConfig = await GetAppConfigForTenantAsync(tenantContext.TenantId, appId, CancellationToken.None);

        if (_appConfig.IntegrationMethodOutbound == IntegrationMethods.REST || _appConfig.IntegrationMethodOutbound == IntegrationMethods.SOAP || _appConfig.IntegrationMethodOutbound == IntegrationMethods.SOAPEagle)
            await ExecuteMultistepForRESTAsync(resource, appId, correlationID);
        else
            await ExecuteGenericUserReplaceLogicAsync(resource, appId, correlationID);

        _ = CreateLogAsync(_appConfig.AppId, resource.Identifier, correlationID);
    }

    protected virtual async Task ExecuteMultistepForRESTAsync(Core2EnterpriseUser resource, string appId, string correlationID)
    {
        // Resolve integration method operations
        var integrationOp = integrationBaseFactory.GetIntegration(
            _appConfig.IntegrationMethodOutbound ?? IntegrationMethods.REST,
            appId
        ) ?? throw new NotSupportedException($"Integration method {_appConfig.IntegrationMethodOutbound} is not supported.");

        // Step 2: Find the Replace actions (multi-step)
        var actionSteps = _appConfig.Actions?
            .Where(a => a is { ActionName: ActionNames.EDIT, ActionTarget: ActionTargets.USER })
            .SelectMany(a => a.ActionSteps)
            .OrderBy(s => s.StepOrder)
            .ToList()
            ?? [];

        if (actionSteps.Count == 0)
        {
            throw new InvalidOperationException($"No EDIT actions for USER target found for AppId: {appId}");
        }

        foreach (var step in actionSteps)
        {
            Log.Information($"[ReplaceUserV4] Processing ActionStep {step.StepOrder} with HttpVerb {step.HttpVerb}");

            var attributes = step.UserAttributeSchemas?.ToList() ?? [];

            var isSoap = _appConfig.IntegrationMethodOutbound == IntegrationMethods.SOAP
                         || _appConfig.IntegrationMethodOutbound == IntegrationMethods.SOAPEagle;
            var payload = isSoap
                ? await integrationOp.MapAndPreparePayloadAsync(attributes, resource, _appConfig, step, CancellationToken.None)
                : await integrationOp.MapAndPreparePayloadAsync(attributes, resource, _appConfig);
            
            Log.Information($"[ReplaceUserV4] Payload mapped and prepared successfully for ActionStep {step.StepOrder}, AppId: {appId}, CorrelationID: {correlationID}");

            var payloadValidationResult = await integrationOp.ValidatePayloadAsync(payload, _appConfig, correlationID);
            if (!payloadValidationResult.Item1)
            {
                Log.Error($"[ReplaceUserV4] Payload validation failed for ActionStep {step.StepOrder}, AppId: {appId}, CorrelationID: {correlationID}. Errors: {string.Join(", ", payloadValidationResult.Item2)}");
                throw new PayloadValidationException(appId, payloadValidationResult.Item2);
            }

            payload = await ExecuteCustomLogicAsync(payload, _appConfig, correlationID);

            // Call the integration method's ReplaceAsync
            var result = await integrationOp.ReplaceAsync(payload, resource, _appConfig.AppId, _appConfig, step, correlationID, CancellationToken.None);
            resource.Identifier = result.Identifier;

            Log.Information($"[ReplaceUserV4] Successfully processed ActionStep {step.StepOrder} for user {resource.Identifier}");
        }
    }

    protected virtual async Task ExecuteGenericUserReplaceLogicAsync(Core2EnterpriseUser resource, string appId, string correlationID)
    {
        var integrationOp = integrationBaseFactory.GetIntegration(
            _appConfig.IntegrationMethodOutbound ?? IntegrationMethods.REST,
            appId
        ) ?? throw new NotSupportedException($"Integration method {_appConfig.IntegrationMethodOutbound} is not supported.");

        var userAttributes = GetUserAttributes(_appConfig.UserAttributeSchemas, _appConfig.IntegrationMethodOutbound);

        var payload = await integrationOp.MapAndPreparePayloadAsync(userAttributes, resource, _appConfig);
        Log.Information(
            "[ReplaceUserV4] Payload mapped and prepared successfully for AppId: {AppId}, CorrelationID: {CorrelationID}, Payload: {Payload}",
            appId, correlationID, JsonConvert.SerializeObject(payload));

        var payloadValidationResult = await integrationOp.ValidatePayloadAsync(payload, _appConfig, correlationID);
        if (!payloadValidationResult.Item1)
        {
            Log.Error("[ReplaceUserV4] Payload validation failed. AppId: {AppId}, CorrelationID: {CorrelationID}, Error: {Error}",
                appId, correlationID, payloadValidationResult.Item2);
            throw new PayloadValidationException(appId, payloadValidationResult.Item2);
        }

        payload = await ExecuteCustomLogicAsync(payload, _appConfig, correlationID);

        await integrationOp.ReplaceAsync(payload, resource, _appConfig, correlationID);

        Log.Information(
            "[ReplaceUserV4] User replaced successfully for Identifier: {Identifier}, AppId: {AppId}, CorrelationID: {CorrelationID}",
            resource.Identifier, appId, correlationID);
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
                return userAttributeSchemas
                    .Where(x => x.HttpRequestType == HttpRequestTypes.PUT)
                    .ToList();

            case IntegrationMethods.ITSM:
                var providerUrlId = _appConfig.ItsmConfigurations.ServiceProviderUrls
                    .FirstOrDefault(x => x.ActionName == ActionNames.EDIT)?.Id;

                return [.. userAttributeSchemas.Where(x => x.ServiceProviderUrlId == providerUrlId)];

            default:
                return userAttributeSchemas.ToList();
        }
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
