using System;
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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace KN.KloudIdentity.Mapper.MapperCore.User;

public class ReplaceUserV2 : ProvisioningBase, IReplaceResourceV2
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IKloudIdentityLogger _logger;
    private readonly IList<IIntegrationBase> _integrations;

    public ReplaceUserV2(IAuthContext authContext,
        IHttpClientFactory httpClientFactory,
        IGetFullAppConfigQuery getFullAppConfigQuery,
        IKloudIdentityLogger logger,
        IList<IIntegrationBase> integrations,
        IOutboundPayloadProcessor outboundPayloadProcessor
    )
        : base(getFullAppConfigQuery, outboundPayloadProcessor)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _integrations = integrations;
    }

    public async Task ReplaceAsync(
        Core2EnterpriseUser resource,
        string appId,
        string correlationID
    )
    {
        Log.Information(
            "Execution started for user replacement. AppId: {AppId}, CorrelationID: {CorrelationID}, Identifier: {Identifier}",
            appId,
            correlationID, resource.Identifier);
        // Step 1: Get app config
        var appConfig = await GetAppConfigAsync(appId);

        // Resolve integration method operations
        var integrationOp =
            _integrations.FirstOrDefault(x => x.IntegrationMethod == appConfig.IntegrationMethodOutbound) ??
            throw new NotSupportedException(
                $"Integration method {appConfig.IntegrationMethodOutbound} is not supported.");

        var attributes = GetUserAttributes(appConfig.UserAttributeSchemas, appConfig.IntegrationMethodOutbound);

        // Step 2: Map and prepare payload
        var payload = await integrationOp.MapAndPreparePayloadAsync(attributes, resource);
        Log.Information(
            "Payload mapped and prepared successfully for AppId: {AppId}, CorrelationID: {CorrelationID}, Payload: {Payload}",
            appId, correlationID, JsonConvert.SerializeObject(payload));
        // Step 3: Replace user
        await integrationOp.ReplaceAsync(payload, resource, appConfig, correlationID);
        Log.Information(
            "User replaced successfully for Identifier: {Identifier}, AppId: {AppId}, CorrelationID: {CorrelationID}",
            resource.Identifier, appId, correlationID);

        _ = CreateLogAsync(appConfig.AppId, resource.Identifier, correlationID);
    }

    private IList<AttributeSchema> GetUserAttributes(ICollection<AttributeSchema> userAttributeSchemas,
        IntegrationMethods? integrationMethodOutbound)
    {
        switch (integrationMethodOutbound)
        {
            case IntegrationMethods.REST:
                return userAttributeSchemas.Where(x => x.HttpRequestType == HttpRequestTypes.PUT).ToList();
            default:
                return userAttributeSchemas.ToList();
        }
    }

    private async Task CreateLogAsync(string appId, string identifier, string correlationID)
    {
        var logEntity = new CreateLogEntity(
            appId,
            LogType.Edit.ToString(),
            LogSeverities.Information,
            "Replace user",
            $"User replaced successfully for the id {identifier}",
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