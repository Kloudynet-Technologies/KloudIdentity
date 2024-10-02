using KN.KloudIdentity.Mapper.Domain.Inbound;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Domain.Mapping.Inbound;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KloudIdentity.Mapper.MapperCore.Inbound.Utils;
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper.MapperCore.Inbound;

public abstract class OperationsBaseInbound : IAPIMapperBaseInbound
{
    private readonly IAuthContext _authContext;
    private readonly IGetInboundAppConfigQuery _getInboundAppConfigQuery;
    private readonly IInboundMapper _inboundMapper;

    public OperationsBaseInbound(
        IAuthContext authContext,
        IInboundMapper inboundMapper,
        IGetInboundAppConfigQuery getInboundAppConfigQuery)
    {
        _authContext = authContext;
        _inboundMapper = inboundMapper;
        _getInboundAppConfigQuery = getInboundAppConfigQuery;

        CorrelationID = Guid.NewGuid().ToString();
    }

    /// <inheritdoc/>
    public string CorrelationID { get; init; }

    /// <inheritdoc/>
    public async Task<InboundConfig> GetAppConfigAsync(string appId)
    {
        return await _getInboundAppConfigQuery.GetAsync(appId);
    }

    /// <inheritdoc/>
    public async Task<string> GetAuthenticationAsync(InboundConfig config, SCIMDirections direction)
    {
        return await _authContext.GetTokenAsync(config, direction);
    }

    /// <inheritdoc/>
    public async Task<JObject> MapAndPreparePayloadAsync(InboundMappingConfig config, JObject users)
    {
        var configValidationResults = await _inboundMapper.ValidateMappingConfigAsync(config);
        if (configValidationResults.Item1)
        {
            var mappedPayload = await _inboundMapper.MapAsync(config, users, CorrelationID);

            var payloadValidationResults = await _inboundMapper.ValidateMappedPayloadAsync(mappedPayload);
            if (payloadValidationResults.Item1)
            {
                return mappedPayload;
            }
            else
            {
                throw new ApplicationException($"Mapped payload is invalid.\n{payloadValidationResults.Item2}");
            }
        }
        else
        {
            throw new ApplicationException($"Mapping configuration is invalid.\n{configValidationResults.Item2}");
        }
    }
}
