using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Common;
using KN.KloudIdentity.Mapper.Common.Exceptions;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Messaging;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Queries;
using KN.KloudIdentity.Mapper.MapperCore.Outbound;
using KN.KloudIdentity.Mapper.MapperCore.Outbound.CustomLogic;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.SCIM;
using Serilog;

namespace KN.KloudIdentity.Mapper.MapperCore.User;

public class GetUserV4 : ProvisioningBase, IGetResourceV2
{
    private readonly IIntegrationBaseFactory _integrationBaseFactory;
    private readonly IKloudIdentityLogger _logger;

    public GetUserV4(
        IGetFullAppConfigQuery getFullAppConfigQuery,
        IIntegrationBaseFactory integrationBaseFactory,
        IOutboundPayloadProcessor outboundPayloadProcessor,
        IKloudIdentityLogger logger) : base(getFullAppConfigQuery, outboundPayloadProcessor)
    {
        _integrationBaseFactory = integrationBaseFactory;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a user by identifier and application ID asynchronously using V4 action-based config.
    /// </summary>
    public async Task<Core2EnterpriseUser> GetAsync(string identifier, string appId, string correlationID)
    {
        Log.Information("Starting GetUserV4 for identifier: {Identifier}, appId: {AppId}", identifier, appId);

        // Retrieve full application configuration
        var appConfig = await GetAppConfigAsync(appId);

        if (appConfig.Actions == null || !appConfig.Actions.Any())
            throw new InvalidOperationException("No action steps defined in application configuration.");

        // Initialize an empty user object
        Core2EnterpriseUser? retrievedUser = null;

        // Process each action step sequentially
        var actionSteps = appConfig.Actions
            .Where(action => action.ActionTarget == ActionTargets.USER && action.ActionName == ActionNames.GET)
            .SelectMany(action => action.ActionSteps)
            .OrderBy(step => step.StepOrder);

        if (!actionSteps.Any())
            throw new InvalidOperationException("No action steps found for user GET operation. AppId: " + appId);

        foreach (var actionStep in actionSteps)
        {
            Log.Information("Processing ActionStep {StepOrder} with HttpVerb {HttpVerb}", actionStep.StepOrder, actionStep.HttpVerb);

            var integrationMethod = _integrationBaseFactory.GetIntegration(appConfig.IntegrationMethodOutbound ?? IntegrationMethods.REST, appId)
                ?? throw new NotSupportedException($"Integration method {appConfig.IntegrationMethodOutbound} is not supported.");

            retrievedUser = await integrationMethod.GetAsync(identifier, appConfig, actionStep, correlationID, CancellationToken.None);

            if (retrievedUser == null)
            {
                Log.Warning("No user retrieved in ActionStep {StepOrder}. Ending process.", actionStep.StepOrder);
                continue;
            }
        }

        if (retrievedUser == null)
        {
            Log.Error("User with identifier {Identifier} not found in any action step for appId {AppId}", identifier, appId);
            throw new NotFoundException($"User with identifier {identifier} not found.");
        }

        Log.Information("Successfully retrieved user with identifier {Identifier} for appId {AppId}", identifier, appId);

        // Create log entry for successful retrieval
        await CreateLogAsync(appId, identifier, correlationID);

        return retrievedUser;
    }

    private async Task CreateLogAsync(string appId, string identifier, string correlationID)
    {
        var logEntity = new CreateLogEntity(
            appId,
            LogType.Read.ToString(),
            LogSeverities.Information,
            "Retrieve user (V4)",
            $"User retrieved successfully for the id {identifier}",
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
