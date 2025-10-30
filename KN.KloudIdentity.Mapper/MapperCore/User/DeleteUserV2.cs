using System;
using System.Configuration.Provider;
using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Common;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KloudIdentity.Mapper.MapperCore.Outbound;
using KN.KloudIdentity.Mapper.MapperCore.Outbound.CustomLogic;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.SCIM;
using Serilog;

namespace KN.KloudIdentity.Mapper.MapperCore.User;

public class DeleteUserV2 : ProvisioningBase, IDeleteResourceV2
{
    private readonly IIntegrationBaseFactory _integrationBaseFactory;
    private readonly IKloudIdentityLogger _logger;

    public DeleteUserV2(
        IGetFullAppConfigQuery getFullAppConfigQuery,
        IIntegrationBaseFactory integrationBaseFactory,
        IOutboundPayloadProcessor outboundPayloadProcessor,
        IKloudIdentityLogger logger) : base(getFullAppConfigQuery, outboundPayloadProcessor)
    {
        _integrationBaseFactory = integrationBaseFactory;
        _logger = logger;
    }

    public async Task DeleteAsync(IResourceIdentifier resourceIdentifier, string appId, string correlationID)
    {
        Log.Information("Execution started for user deletion. Identifier: {Identifier}, AppId: {AppId}, CorrelationID: {CorrelationID}", resourceIdentifier.Identifier, appId, correlationID);
       
        // Step 1: Get app config
        var appConfig = await GetAppConfigAsync(appId);
        
        // Resolve integration method operations
        var integrationOp =
            _integrationBaseFactory.GetIntegration(appConfig.IntegrationMethodOutbound ?? IntegrationMethods.REST,
                appId) ??
            throw new NotSupportedException(
                $"Integration method {appConfig.IntegrationMethodOutbound} is not supported.");
        
        // Validate the request.
        ValidatedRequest(resourceIdentifier.Identifier, appConfig);
        
        // Step 2: Delete user
        await integrationOp.DeleteAsync(resourceIdentifier.Identifier, appConfig, correlationID);
        
        // Log the operation.
        await CreateLogAsync(appConfig.AppId, resourceIdentifier.Identifier, correlationID);
        
        Log.Information(
            "User deleted successfully for the id {Identifier}. AppId: {AppId}, CorrelationID: {CorrelationID}",
            resourceIdentifier.Identifier, appConfig.AppId, correlationID);
    }

    /// <summary>
    /// Validates the request by checking if the identifier and DELETEAPIForUsers are null or empty.
    /// </summary>
    /// <param name="identifier">The identifier to be validated.</param>
    /// <param name="appConfig">The mapper configuration containing DELETEAPIForUsers.</param>
    /// <exception cref="ArgumentNullException">Thrown when the identifier or DELETEAPIForUsers is null or empty.</exception>
    private void ValidatedRequest(string identifier, AppConfig appConfig)
    {
        if (appConfig.IntegrationMethodOutbound == IntegrationMethods.AS400 || appConfig.IntegrationMethodOutbound == IntegrationMethods.SQL) return;

        var userURIs = appConfig.UserURIs.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(identifier))
        {
            Log.Error("Identifier cannot be null or empty");
            throw new ArgumentNullException(nameof(identifier), "Identifier cannot be null or empty");
        }
        if (userURIs == null || userURIs.Delete == null)
        {
            Log.Error("Delete endpoint cannot be null or empty");
            throw new ArgumentNullException(nameof(userURIs.Delete), "Delete endpoint cannot be null or empty");
        }
    }

    private async Task CreateLogAsync(string appId, string identifier, string correlationID)
    {
        var logEntity = new CreateLogEntity(
            appId,
            LogType.Deprovision.ToString(),
            LogSeverities.Information,
            "User Deprovision",
            $"User deleted successfully for the id {identifier}",
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
