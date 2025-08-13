using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Domain.Inbound;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Domain.Mapping.Inbound;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KloudIdentity.Mapper.MapperCore.Inbound.Utils;
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;
using Serilog;

namespace KN.KloudIdentity.Mapper.MapperCore.Inbound;

public abstract class OperationsBaseInbound : IAPIMapperBaseInbound
{
    private readonly IAuthContext _authContext;
    private readonly IGetInboundAppConfigQuery _getInboundAppConfigQuery;
    private readonly IInboundMapper _inboundMapper;
    private readonly IKloudIdentityLogger _logger;

    public OperationsBaseInbound(
        IAuthContext authContext,
        IInboundMapper inboundMapper,
        IGetInboundAppConfigQuery getInboundAppConfigQuery,
        IKloudIdentityLogger logger)
    {
        _authContext = authContext;
        _inboundMapper = inboundMapper;
        _getInboundAppConfigQuery = getInboundAppConfigQuery;
        _logger = logger;

        CorrelationID = Guid.NewGuid().ToString();
    }

    /// <inheritdoc/>
    public string CorrelationID { get; init; }

    /// <inheritdoc/>
    public async Task<InboundConfig> GetAppConfigAsync(string appId)
    {
        var config = await _getInboundAppConfigQuery.GetAsync(appId);
        if (config == null)
        {
            Log.Error("No configuration found for the application with ID: {AppId}, CorrelationID: {CorrelationID}",
                appId, CorrelationID);
            _ = CreateLogAsync(appId, LogSeverities.Error, "GetInboundAppConfig",
                $"No configuration found for the application with ID: {appId}", CorrelationID);

            throw new ApplicationException($"No configuration found for the application with ID: {appId}");
        }

        Log.Information("Configuration found for the application with ID: {AppId}, CorrelationID: {CorrelationID}",
            appId, CorrelationID);
        _ = CreateLogAsync(appId, LogSeverities.Information, "GetInboundAppConfig",
            $"Configuration found for the application with ID: {appId}", CorrelationID);

        return config;
    }

    /// <inheritdoc/>
    public async Task<string> GetAuthenticationAsync(InboundConfig config, SCIMDirections direction)
    {
        string token = await _authContext.GetTokenAsync(config, direction);
        if (string.IsNullOrEmpty(token))
        {
            Log.Error("No access token generated. AppId: {AppId}, CorrelationID: {CorrelationID}", config.AppId,
                CorrelationID);
            _ = CreateLogAsync(config.AppId, LogSeverities.Error, "GetAuthentication", "No access token generated",
                CorrelationID);

            throw new ApplicationException("No token found");
        }

        Log.Information("Access token generated successfully. AppId: {AppId}, CorrelationID: {CorrelationID}",
            config.AppId, CorrelationID);
        _ = CreateLogAsync(config.AppId, LogSeverities.Information, "GetAuthentication", "Access token generated",
            CorrelationID);

        return token;
    }

    /// <inheritdoc/>
    public async Task<JObject> MapAndPreparePayloadAsync(InboundMappingConfig config, JObject users, string appId)
    {
        try
        {
            var configValidationResults = await _inboundMapper.ValidateMappingConfigAsync(config);
            if (configValidationResults.Item1)
            {
                var mappedPayload = await _inboundMapper.MapAsync(config, users, CorrelationID);

                var payloadValidationResults = await _inboundMapper.ValidateMappedPayloadAsync(mappedPayload);
                if (payloadValidationResults.Item1)
                {
                    Log.Information(
                        "Payload mapping completed successfully. AppId: {AppId}, CorrelationID: {CorrelationID}", appId,
                        CorrelationID);
                    _ = CreateLogAsync(appId, LogSeverities.Information, "MapAndPreparePayload",
                        "Payload mapping completed", CorrelationID);

                    return mappedPayload;
                }
                else
                {
                    Log.Error(
                        "Mapped payload is invalid. AppId: {AppId}, CorrelationID: {CorrelationID}, Error: {Error}",
                        appId, CorrelationID, payloadValidationResults.Item2);
                    _ = CreateLogAsync(appId, LogSeverities.Error, "MapAndPreparePayload",
                        $"Mapped payload is invalid.\n{payloadValidationResults.Item2}", CorrelationID);

                    throw new ApplicationException($"Mapped payload is invalid.\n{payloadValidationResults.Item2}");
                }
            }
            else
            {
                Log.Error(
                    "Mapping configuration is invalid. AppId: {AppId}, CorrelationID: {CorrelationID}, Error: {Error}",
                    appId, CorrelationID, configValidationResults.Item2);
                _ = CreateLogAsync(appId, LogSeverities.Error, "MapAndPreparePayload",
                    $"Mapping configuration is invalid.\n{configValidationResults.Item2}", CorrelationID);

                throw new ApplicationException($"Mapping configuration is invalid.\n{configValidationResults.Item2}");
            }
        }
        catch (Exception ex)
        {
            Log.Error(
                "Error occurred while mapping and preparing payload. AppId: {AppId}, CorrelationID: {CorrelationID}, Error: {Error}",
                appId, CorrelationID, ex.Message);
            _ = CreateLogAsync(appId, LogSeverities.Error, "MapAndPreparePayload", ex.Message, CorrelationID);

            throw;
        }
    }

    private async Task CreateLogAsync(string appId, LogSeverities severity, string eventInfo, string message,
        string correlationId)
    {
        await _logger.CreateLogAsync(new CreateLogEntity
        (
            appId,
            "Inbound",
            severity,
            eventInfo,
            message,
            correlationId,
            "KN.KloudIdentity",
            DateTime.UtcNow,
            "SYSTEM",
            null,
            null
        ));
    }
}