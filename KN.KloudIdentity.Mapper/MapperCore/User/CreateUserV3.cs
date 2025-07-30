using System;
using System.Reflection;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KloudIdentity.Mapper.MapperCore.Outbound;
using KN.KloudIdentity.Mapper.MapperCore.Outbound.CustomLogic;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.Extensions.Options;
using Microsoft.SCIM;
using Serilog;

namespace KN.KloudIdentity.Mapper.MapperCore.User;

public class CreateUserV3 : CreateUserV2, ICreateResourceV2
{
    private readonly IList<IIntegrationBase> _integrations;
    private readonly IKloudIdentityLogger _logger;
    private readonly IOptions<AppSettings> _options;
    private readonly IReplaceResourceV2 _replaceResourceV2;
    private readonly IAzureStorageManager _azureStorageManager;

    public CreateUserV3(
        IGetFullAppConfigQuery getFullAppConfigQuery,
        IList<IIntegrationBase> integrations,
        IOutboundPayloadProcessor outboundPayloadProcessor,
        IKloudIdentityLogger logger,
        IOptions<AppSettings> options,
        IReplaceResourceV2 replaceResourceV2,
        IAzureStorageManager azureStorageManager) : base(getFullAppConfigQuery, integrations, outboundPayloadProcessor, logger)
    {
        _integrations = integrations;
        _logger = logger;
        _options = options;
        _replaceResourceV2 = replaceResourceV2;

        _azureStorageManager = azureStorageManager;
    }

    public override async Task<Core2EnterpriseUser> ExecuteAsync(Core2EnterpriseUser resource, string appId, string correlationID)
    {
        Log.Information("Execution started for user creation. AppId: {AppId}, CorrelationID: {CorrelationID}", appId, correlationID);

        // Step 1: If user migration is not enabled, skip the migration logic.
        if (!_options.Value.UserMigration.IsEnabled)
        {
            // If user migration is not enabled, skip the migration logic.
            return await base.ExecuteAsync(resource, appId, correlationID);
        }

        // Step 2: Extract the correlation property name and the value from the configuration.
        var correlationPropertyName = _options.Value.UserMigration.CorrelationPropertyName;
        if (string.IsNullOrWhiteSpace(correlationPropertyName))
        {
            throw new ArgumentNullException(nameof(correlationPropertyName), "Correlation property is not configured in AppSettings.");
        }

        // Use a cached compiled delegate for property access to improve performance
        var correlationPropertyValue = PropertyAccessorCacheUtil.GetPropertyValue(resource, correlationPropertyName);
        if (string.IsNullOrWhiteSpace(correlationPropertyValue))
        {
            throw new ArgumentNullException(nameof(correlationPropertyValue), $"Value for property '{correlationPropertyName}' cannot be null or empty.");
        }

        // Step 3: Retrieve the user migration data from Azure Storage.
        var userMigrationData = await _azureStorageManager.GetUserMigrationDataAsync(appId, correlationPropertyValue);
        if (userMigrationData == null)
        {
            Log.Information("No user migration data found for AppId: {AppId}, CorrelationID: {CorrelationID}. Proceeding with user creation.", appId, correlationID);
            return await base.ExecuteAsync(resource, appId, correlationID);
        }

        Log.Information("User migration data found for AppId: {AppId}, CorrelationID: {CorrelationID}. Proceeding with user migration.", appId, correlationID);

        // Step 4: Update the user resource with the existing user identifier.
        resource.Identifier = userMigrationData.RowKey;

        // Step 5: Replace the user using the ReplaceUserV2 logic.
        await _replaceResourceV2.ReplaceAsync(resource, appId, correlationID);

        // Step 6: Return the updated user.
        return resource;
    }
}