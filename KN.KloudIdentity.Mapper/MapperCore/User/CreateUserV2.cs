using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Common;
using KN.KloudIdentity.Mapper.Common.Exceptions;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KloudIdentity.Mapper.MapperCore.Outbound;
using KN.KloudIdentity.Mapper.MapperCore.Outbound.CustomLogic;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.SCIM;
using Newtonsoft.Json;
using Serilog;

namespace KN.KloudIdentity.Mapper.MapperCore.User;

/// <summary>
/// Creates a user in the LOB app.
/// This class is the entry point for creating a user in the LOB app.
/// </summary>
public class CreateUserV2 : ProvisioningBase, ICreateResourceV2
{
    private readonly IList<IIntegrationBase> _integrations;
    private readonly IKloudIdentityLogger _logger;

    public CreateUserV2(
        IGetFullAppConfigQuery getFullAppConfigQuery,
        IList<IIntegrationBase> integrations,
        IOutboundPayloadProcessor outboundPayloadProcessor,
        IKloudIdentityLogger logger) : base(getFullAppConfigQuery, outboundPayloadProcessor)
    {
        _integrations = integrations;
        _logger = logger;
    }

    /// <summary>
    /// Executes the creation of a new user asynchronously.
    /// This method configures the integration method, maps the attributes, validates the payload, executes custom logic, and provisions the user.
    /// The provisioning pipeline is executed in this method.
    /// </summary>
    /// <param name="resource"></param>
    /// <param name="appId"></param>
    /// <param name="correlationID"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException">When the correct integration method implementation cannot be found</exception>
    /// <exception cref="PayloadValidationException">When payload validation fails</exception>
    public async Task<Core2EnterpriseUser> ExecuteAsync(Core2EnterpriseUser resource, string appId, string correlationID)
    {
        Log.Information("Execution started for user creation. AppId: {AppId}, CorrelationID: {CorrelationID}", appId, correlationID);

        // Step 1: Get app config
        var appConfig = await GetAppConfigAsync(appId);

        // Resolve integration method operations
        var integrationOp = _integrations.FirstOrDefault(x => x.IntegrationMethod == appConfig.IntegrationMethodOutbound) ??
                                throw new NotSupportedException($"Integration method {appConfig.IntegrationMethodOutbound} is not supported.");

        // Step 2: Attribute mapping
        var userAttributes = GetUserAttributes(appConfig.UserAttributeSchemas, appConfig.IntegrationMethodOutbound);
        var payload = await integrationOp.MapAndPreparePayloadAsync(userAttributes, resource);
        Log.Information(
            "Payload mapped and prepared successfully for AppId: {AppId}, CorrelationID: {CorrelationID}, Payload: {Payload}",
            appId, correlationID, JsonConvert.SerializeObject(payload));
        
        // Step 3: Payload validation
        var payloadValidationResult = await integrationOp.ValidatePayloadAsync(payload, appConfig, correlationID);
        if (!payloadValidationResult.Item1)
        {
            Log.Error("Payload validation failed. AppId: {AppId}, CorrelationID: {CorrelationID}, Error: {Error}", appId, correlationID, payloadValidationResult.Item2);
            throw new PayloadValidationException(appId, payloadValidationResult.Item2);
        }
        
        // Step 4: Execute custom logic
        payload = await ExecuteCustomLogicAsync(payload, appConfig, correlationID);

        // Step 5: Provisioning
        await integrationOp.ProvisionAsync(payload, appConfig, correlationID);

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
