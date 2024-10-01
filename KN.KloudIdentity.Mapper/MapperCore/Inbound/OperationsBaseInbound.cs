using KN.KloudIdentity.Mapper.Domain.Inbound;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Domain.Mapping.Inbound;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper.MapperCore.Inbound;

public abstract class OperationsBaseInbound : IAPIMapperBaseInbound
{
    private readonly IAuthContext _authContext;
    private IGetInboundAppConfigQuery _getInboundAppConfigQuery;

    public OperationsBaseInbound(
        IAuthContext authContext,
        IGetInboundAppConfigQuery getInboundAppConfigQuery
        )
    {
        _authContext = authContext;
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
    public async Task<JObject> InvokeAndFetchUsersAsync(InboundConfig config, string token)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public async Task<JObject> MapAndPreparePayloadAsync(InboundMappingConfig config, JObject users)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public async Task ProvisionUsersAsync(InboundConfig config, JObject mappedPayload)
    {
        throw new NotImplementedException();
    }
}
