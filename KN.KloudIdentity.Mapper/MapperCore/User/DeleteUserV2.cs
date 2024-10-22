using System;
using System.Configuration.Provider;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KloudIdentity.Mapper.MapperCore.Outbound;
using KN.KloudIdentity.Mapper.MapperCore.Outbound.CustomLogic;
using Microsoft.SCIM;

namespace KN.KloudIdentity.Mapper.MapperCore.User;

public class DeleteUserV2 : ProvisioningBase, IDeleteResourceV2
{
    private readonly IList<IIntegrationBase> _integrations;

    public DeleteUserV2(
        IGetFullAppConfigQuery getFullAppConfigQuery,
        IOutboundPayloadProcessor outboundPayloadProcessor,
        IList<IIntegrationBase> integrations) : base(getFullAppConfigQuery, outboundPayloadProcessor)
    {
        _integrations = integrations;
    }

    public async Task DeleteAsync(IResourceIdentifier resourceIdentifier, string appId, string correlationID)
    {
        // Step 1: Get app config
        var appConfig = await GetAppConfigAsync(appId);

        // Resolve integration method operations
        var integrationOp = _integrations.FirstOrDefault(x => x.IntegrationMethod == appConfig.IntegrationMethodOutbound) ??
                                throw new NotSupportedException($"Integration method {appConfig.IntegrationMethodOutbound} is not supported.");

        // Validate the request.
        ValidatedRequest(resourceIdentifier.Identifier, appConfig);

        // Step 2: Delete user
        await integrationOp.DeleteAsync(resourceIdentifier.Identifier, appConfig, correlationID);

        // Log the operation.
        // await CreateLogAsync(_appConfig, resourceIdentifier.Identifier, correlationID);
    }

    /// <summary>
    /// Validates the request by checking if the identifier and DELETEAPIForUsers are null or empty.
    /// </summary>
    /// <param name="identifier">The identifier to be validated.</param>
    /// <param name="appConfig">The mapper configuration containing DELETEAPIForUsers.</param>
    /// <exception cref="ArgumentNullException">Thrown when the identifier or DELETEAPIForUsers is null or empty.</exception>
    private void ValidatedRequest(string identifier, AppConfig appConfig)
    {
        var userURIs = appConfig.UserURIs.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentNullException(nameof(identifier), "Identifier cannot be null or empty");
        }
        if (userURIs == null || userURIs.Delete == null)
        {
            throw new ArgumentNullException(nameof(userURIs.Delete), "Delete endpoint cannot be null or empty");
        }
    }
}
