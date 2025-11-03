using System;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KloudIdentity.Mapper.MapperCore.Outbound.CustomLogic;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SCIM;
using Serilog;

namespace KN.KloudIdentity.Mapper.MapperCore.User;

public class CreateUserV3 : CreateUserV2, ICreateResourceV2
{
    private readonly IIntegrationBaseFactory _integrationFactory;
    private readonly IKloudIdentityLogger _logger;
    private readonly IOptions<AppSettings> _options;
    private readonly IReplaceResourceV2 _replaceResourceV2;
    private readonly IAzureStorageManager? _azureStorageManager;

    public CreateUserV3(
        IGetFullAppConfigQuery getFullAppConfigQuery,
        IIntegrationBaseFactory integrationFactory,
        IOutboundPayloadProcessor outboundPayloadProcessor,
        IKloudIdentityLogger logger,
        IOptions<AppSettings> options,
        IReplaceResourceV2 replaceResourceV2,
        IServiceProvider serviceProvider) : base(getFullAppConfigQuery, integrations, outboundPayloadProcessor,        
        logger)
    {
        _integrationFactory = integrationFactory;
        _logger = logger;
        _options = options;
        _replaceResourceV2 = replaceResourceV2;
        _azureStorageManager = serviceProvider.GetService<IAzureStorageManager>();
    }

    public override async Task<Core2EnterpriseUser> ExecuteAsync(Core2EnterpriseUser resource, string appId,
        string correlationID)
    {
        Log.Information("Execution started for user creation. AppId: {AppId}, CorrelationID: {CorrelationID}", appId,
            correlationID);

        // Step 1: If user migration is not enabled, skip the migration logic.
        if (!IsUserMigrationEnabled(appId))
        {
            // If user migration is not enabled, skip the migration logic.
            return await base.ExecuteAsync(resource, appId, correlationID);
        }

        // Step 2: Extract the correlation property name and the value from the configuration.
        var correlationPropertyName = GetCorrelationPropertyName(appId);

        // Use a cached compiled delegate for property access to improve performance
        var correlationPropertyValue = PropertyAccessorCacheUtil.GetPropertyValue(resource, correlationPropertyName);
        if (string.IsNullOrWhiteSpace(correlationPropertyValue))
        {
            throw new ArgumentException(
                $"Value for property '{correlationPropertyName}' cannot be null or empty. Parameter: {nameof(correlationPropertyValue)}"
            );
        }

        // Step 3: Retrieve the user migration data from Azure Storage.

        if (_azureStorageManager == null)
        {
            Log.Warning("AzureStorageManager is not configured. Skipping user migration logic. AppId: {AppId}, CorrelationID: {CorrelationID}", appId, correlationID);
            return await base.ExecuteAsync(resource, appId, correlationID);
        }

        var userMigrationData = await _azureStorageManager.GetUserMigrationDataAsync(appId, correlationPropertyValue);
        if (userMigrationData == null)
        {
            Log.Information(
                "No user migration data found for AppId: {AppId}, CorrelationID: {CorrelationID}. Proceeding with user creation.",
                appId, correlationID);
            return await base.ExecuteAsync(resource, appId, correlationID);
        }

        Log.Information(
            "User migration data found for AppId: {AppId}, CorrelationID: {CorrelationID}. Proceeding with user migration.",
            appId, correlationID);

        // Step 4: Update the user resource with the existing user identifier.
        resource.Identifier = userMigrationData.RowKey;

        // Step 5: Replace the user using the ReplaceUserV2 logic.
        await _replaceResourceV2.ReplaceAsync(resource, appId, correlationID);

        // Step 6: Return the updated user.
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

        throw new InvalidOperationException(
            $"UserMigration: No correlation property configured for AppId: {appId}. Parameter: {nameof(appId)}");
    }
}