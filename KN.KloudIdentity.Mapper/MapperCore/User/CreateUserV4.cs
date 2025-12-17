using Serilog;
using System;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KloudIdentity.Mapper.MapperCore.Outbound.CustomLogic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SCIM;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Common.Exceptions;
using Newtonsoft.Json;
using KN.KloudIdentity.Mapper.MapperCore.Outbound;
using KN.KI.LogAggregator.Library;
using KN.KloudIdentity.Mapper.Common;
using KN.KloudIdentity.Mapper.Utils;
using KN.KloudIdentity.Mapper.Domain.Mapping;

namespace KN.KloudIdentity.Mapper.MapperCore.User;

public class CreateUserV4 : ProvisioningBase, ICreateResourceV2
{
    private readonly IIntegrationBaseFactory _integrationFactory;
    private readonly IKloudIdentityLogger _logger;
    private readonly IOptions<AppSettings> _options;
    private readonly IReplaceResourceV2 _replaceResourceV2;
    private readonly IAzureStorageManager? _azureStorageManager;
    private readonly IGetFullAppConfigQuery _getFullAppConfigQuery;
    private readonly IOutboundPayloadProcessor _outboundPayloadProcessor;
    private AppConfig _appConfig = null!;

    public CreateUserV4(
        IGetFullAppConfigQuery getFullAppConfigQuery,
        IIntegrationBaseFactory integrationBaseFactory,
        IOutboundPayloadProcessor outboundPayloadProcessor,
        IKloudIdentityLogger logger,
        IOptions<AppSettings> options,
        IReplaceResourceV2 replaceResourceV2,
        IServiceProvider serviceProvider) : base(getFullAppConfigQuery, outboundPayloadProcessor)
    {
        _getFullAppConfigQuery = getFullAppConfigQuery;
        _outboundPayloadProcessor = outboundPayloadProcessor;
        _integrationFactory = integrationBaseFactory;
        _logger = logger;
        _options = options;
        _replaceResourceV2 = replaceResourceV2;
        _azureStorageManager = serviceProvider.GetService<IAzureStorageManager>();
    }

    public async Task<Core2EnterpriseUser> ExecuteAsync(Core2EnterpriseUser resource, string appId, string correlationID)
    {
        // Logging start
        Log.Information("Execution started for user creation. AppId: {AppId}, CorrelationID: {CorrelationID}", appId, correlationID);

        // Step 1: Get app config
        _appConfig = await GetAppConfigAsync(appId);

        // Step 2: If user migration is enabled, handle user migration logic.
        if (IsUserMigrationEnabled(appId))
        {
            resource = await HandleUserMigrationAsync(resource, appId, correlationID);
            return resource;
        }

        // Step 3: Handle multistep API calls if applicable
        resource = _appConfig.IntegrationMethodOutbound == IntegrationMethods.REST
            ? await ExecuteMultistepForRESTAsync(resource, appId, correlationID)
            : await ExecuteGenericUserCreationLogicAsync(resource, appId, correlationID);

        // Step 4: Return the updated user.
        return resource;
    }

    protected virtual async Task<Core2EnterpriseUser> ExecuteMultistepForRESTAsync(Core2EnterpriseUser resource, string appId, string correlationID)
    {
        // Resolve integration method operations
        var integrationOp = _integrationFactory.GetIntegration(_appConfig.IntegrationMethodOutbound ?? IntegrationMethods.REST, appId) ??
                                throw new NotSupportedException($"Integration method {_appConfig.IntegrationMethodOutbound} is not supported.");

        var apiSteps = _appConfig.Actions?.Where(a => a.ActionTarget == ActionTargets.USER && a.ActionName == ActionNames.CREATE)
                        .SelectMany(a => a.ActionSteps)
                        .OrderBy(s => s.StepOrder)
                        .ToList()
                        ?? [];

        foreach (var step in apiSteps)
        {
            // Attribute mapping
            var userAttributes = step.UserAttributeSchemas?.ToList() ?? [];
            var payload = await integrationOp.MapAndPreparePayloadAsync(userAttributes, resource);
            Log.Information(
                "Payload mapped and prepared successfully for AppId: {AppId}, CorrelationID: {CorrelationID}, Step: {Step}, Payload: {Payload}",
                appId, correlationID, step.StepOrder, JsonConvert.SerializeObject(payload));

            // Payload validation
            var payloadValidationResult = await integrationOp.ValidatePayloadAsync(payload, _appConfig, correlationID);
            if (!payloadValidationResult.Item1)
            {
                Log.Error("Payload validation failed. AppId: {AppId}, CorrelationID: {CorrelationID}, Step: {Step}, Error: {Error}", appId, correlationID, step.StepOrder, payloadValidationResult.Item2);
                throw new PayloadValidationException(appId, payloadValidationResult.Item2);
            }

            // Execute custom logic
            payload = await ExecuteCustomLogicAsync(payload, _appConfig, correlationID);

            // Provisioning
            var result = await integrationOp.ProvisionAsync(payload, appId, _appConfig, step, correlationID);
            if (result is not null)
                resource.Identifier = result.Identifier;
        }

        // Step 6: Logging
        await CreateLogAsync(appId, correlationID);
        Log.Information("User provisioned successfully. AppId: {AppId}, CorrelationID: {CorrelationID}, Identifier: {Identifier}", appId, correlationID, resource.Identifier);

        return resource;
    }

    protected virtual async Task<Core2EnterpriseUser> HandleUserMigrationAsync(Core2EnterpriseUser resource, string appId, string correlationID)
    {
        // Step 1: Extract the correlation property name and the value from the configuration.
        var correlationPropertyName = GetCorrelationPropertyName(appId);
        var correlationPropertyValue = Utils.PropertyAccessorCacheUtil.GetPropertyValue(resource, correlationPropertyName);
        if (string.IsNullOrWhiteSpace(correlationPropertyValue))
        {
            throw new ArgumentException($"Value for property '{correlationPropertyName}' cannot be null or empty.");
        }

        // Step 2: Retrieve the user migration data from Azure Storage.
        if (_azureStorageManager == null)
        {
            Log.Warning("AzureStorageManager is not configured. Skipping user migration logic. AppId: {AppId}, CorrelationID: {CorrelationID}", appId, correlationID);
            return await ExecuteGenericUserCreationLogicAsync(resource, appId, correlationID);
        }

        var userMigrationData = await _azureStorageManager.GetUserMigrationDataAsync(appId, correlationPropertyValue);
        if (userMigrationData == null)
        {
            Log.Information("No user migration data found for AppId: {AppId}, CorrelationID: {CorrelationID}. Proceeding with user creation.", appId, correlationID);
            return await ExecuteGenericUserCreationLogicAsync(resource, appId, correlationID);
        }

        Log.Information("User migration data found for AppId: {AppId}, CorrelationID: {CorrelationID}. Proceeding with user migration.", appId, correlationID);

        // Step 3: Update the user resource with the existing user identifier.
        resource.Identifier = userMigrationData.RowKey;

        // Step 4: Replace the user using the ReplaceUserV2 logic.
        await _replaceResourceV2.ReplaceAsync(resource, appId, correlationID);

        return resource;
    }

    private bool IsUserMigrationEnabled(string appId)
    {
        return _options.Value.UserMigration?.AppFeatureEnabledMap is { } map
               && map.TryGetValue(appId, out var enabled)
               && enabled;
    }

    private string GetCorrelationPropertyName(string appId)
    {
        if (_options.Value.UserMigration?.AppCorrelationPropertyMap is { } map
            && map.TryGetValue(appId, out var correlationPropertyName))
        {
            return correlationPropertyName;
        }

        throw new InvalidOperationException($"UserMigration: No correlation property configured for AppId: {appId}.");
    }

    protected virtual async Task<Core2EnterpriseUser> ExecuteGenericUserCreationLogicAsync(Core2EnterpriseUser resource, string appId, string correlationID)
    {
        // Resolve integration method operations
        var integrationOp = _integrationFactory.GetIntegration(_appConfig.IntegrationMethodOutbound ?? IntegrationMethods.REST, appId) ??
                                throw new NotSupportedException($"Integration method {_appConfig.IntegrationMethodOutbound} is not supported.");

        // Step 2: Attribute mapping
        var userAttributes = GetUserAttributes(_appConfig.UserAttributeSchemas, _appConfig.IntegrationMethodOutbound);
        var payload = await integrationOp.MapAndPreparePayloadAsync(userAttributes, resource);
        Log.Information(
            "Payload mapped and prepared successfully for AppId: {AppId}, CorrelationID: {CorrelationID}, Payload: {Payload}",
            appId, correlationID, JsonConvert.SerializeObject(payload));

        // Step 3: Payload validation
        var payloadValidationResult = await integrationOp.ValidatePayloadAsync(payload, _appConfig, correlationID);
        if (!payloadValidationResult.Item1)
        {
            Log.Error("Payload validation failed. AppId: {AppId}, CorrelationID: {CorrelationID}, Error: {Error}", appId, correlationID, payloadValidationResult.Item2);
            throw new PayloadValidationException(appId, payloadValidationResult.Item2);
        }

        // Step 4: Execute custom logic
        payload = await ExecuteCustomLogicAsync(payload, _appConfig, correlationID);

        // Step 5: Provisioning
        var result = await integrationOp.ProvisionAsync(payload, _appConfig, correlationID);
        if (result is not null)
            resource.Identifier = result.Identifier;

        // Step 6: Logging
        await CreateLogAsync(appId, correlationID);

        Log.Information("User provisioned successfully. AppId: {AppId}, CorrelationID: {CorrelationID}, Identifier: {Identifier}", appId, correlationID, resource.Identifier);

        return resource;
    }

    private IList<AttributeSchema> GetUserAttributes(ICollection<AttributeSchema> userAttributeSchemas, IntegrationMethods? integrationMethodOutbound)
    {
        switch (integrationMethodOutbound)
        {
            case IntegrationMethods.REST:
            case IntegrationMethods.SQL:
                return userAttributeSchemas.Where(x => x.HttpRequestType == HttpRequestTypes.POST).ToList();
            default:
                return userAttributeSchemas.ToList();
        }
    }

    private async Task CreateLogAsync(string appId, string correlationID)
    {
        var logEntity = new CreateLogEntity(
            appId,
            LogType.Provision.ToString(),
            LogSeverities.Information,
            "User Provision",
            "User provisioned successfully",
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
