using KN.KloudIdentity.Mapper.Common.Exceptions;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KloudIdentity.Mapper.MapperCore.Outbound;
using KN.KloudIdentity.Mapper.MapperCore.Outbound.CustomLogic;
using Microsoft.SCIM;

namespace KN.KloudIdentity.Mapper.MapperCore.User;

/// <summary>
/// Creates a user in the LOB app.
/// This class is the entry point for creating a user in the LOB app.
/// </summary>
public class CreateUserV2 : ProvisioningBase, ICreateResourceV2
{
    private readonly IList<IIntegrationBase> _integrations;

    public CreateUserV2(
        IGetFullAppConfigQuery getFullAppConfigQuery,
        IOutboundPayloadProcessor outboundPayloadProcessor,
        IList<IIntegrationBase> integrations) : base(getFullAppConfigQuery, outboundPayloadProcessor)
    {
        _integrations = integrations;
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
        // Step 1: Get app config
        var appConfig = await GetAppConfigAsync(appId);

        // Resolve integration method operations
        var integrationOp = _integrations.FirstOrDefault(x => x.IntegrationMethod == appConfig.IntegrationMethodOutbound) ??
                                throw new NotSupportedException($"Integration method {appConfig.IntegrationMethodOutbound} is not supported.");

        // Step 2: Attribute mapping
        var userAttributes = appConfig.UserAttributeSchemas.Where(x => x.HttpRequestType == HttpRequestTypes.POST).ToList();
        var payload = await integrationOp.MapAndPreparePayloadAsync(userAttributes, resource);

        // Step 3: Payload validation
        var payloadValidationResult = await integrationOp.ValidatePayloadAsync(payload, correlationID);
        if (!payloadValidationResult.Item1)
            throw new PayloadValidationException(appId, payloadValidationResult.Item2);

        // Step 4: Execute custom logic
        payload = await ExecuteCustomLogicAsync(payload, appConfig, correlationID);

        // Step 5: Provisioning
        await integrationOp.ProvisionAsync(payload, appConfig, correlationID);

        return resource;
    }
}
