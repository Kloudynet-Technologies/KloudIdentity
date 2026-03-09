using System;
using KN.KloudIdentity.Mapper.Common.Exceptions;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KloudIdentity.Mapper.MapperCore.Outbound.CustomLogic;
using Microsoft.SCIM;
using Serilog;
using System.Linq;

namespace KN.KloudIdentity.Mapper.MapperCore.Outbound;

/// <summary>
/// Represents the base class for provisioning.
/// This contains all the common methods and properties for provisioning.
/// </summary>
public class ProvisioningBase : IProvisioningBase
{
    private readonly IGetFullAppConfigQuery _getFullAppConfigQuery;
    private readonly IOutboundPayloadProcessor _outboundPayloadProcessor;

    public ProvisioningBase(IGetFullAppConfigQuery getFullAppConfigQuery,
        IOutboundPayloadProcessor outboundPayloadProcessor)
    {
        _getFullAppConfigQuery = getFullAppConfigQuery;
        _outboundPayloadProcessor = outboundPayloadProcessor;
    }

    /// <summary>
    /// Executes custom logic asynchronously.
    /// </summary>
    /// <param name="payload">Payload to be sent to custom logic endpoint</param>
    /// <param name="appConfig">App configuration</param>
    /// <param name="correlationID">Correlation ID</param>
    /// <returns></returns>
    public virtual async Task<dynamic> ExecuteCustomLogicAsync(dynamic payload, AppConfig appConfig,
        string correlationID)
    {
        if (appConfig is { IsExternalAPIEnabled: false or null })
        {
            return payload;
        }

        payload = await _outboundPayloadProcessor.ProcessAsync(
            payload,
            appConfig.ExternalEndpointInfo,
            correlationID,
            CancellationToken.None);

        return payload;
    }

    /// <summary>
    /// Gets the application configuration asynchronously.
    /// </summary>
    /// <param name="appId">Application ID</param>
    /// <returns></returns>
    /// <exception cref="NotFoundException"></exception>
    public virtual async Task<AppConfig> GetAppConfigAsync(string appId)
    {
        var config = await _getFullAppConfigQuery.GetAsync(appId);

        if (config == null)
        {
            Log.Error("App configuration not found for app ID {AppId}", appId);
            throw new NotFoundException($"App configuration not found for app ID {appId}.");
        }

        return config;
    }

    /// <summary>
    /// Returns an app config scoped to a single SOAP action template for payload mapping.
    /// For non-SOAP integrations, returns the original configuration instance.
    /// </summary>
    protected AppConfig GetMappingConfigForSoapAction(AppConfig appConfig, SOAPActions action)
    {
        if (appConfig.IntegrationMethodOutbound != IntegrationMethods.SOAP)
        {
            return appConfig;
        }

        var scopedTemplates = appConfig.SOAPTemplates?
            .Where(template => template.Action == action)
            .ToList();

        return appConfig with
        {
            SOAPTemplates = scopedTemplates
        };
    }
}