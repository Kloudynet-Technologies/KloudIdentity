using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Inbound;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Domain.Mapping.Inbound;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper.MapperCore.Inbound;

public abstract class OperationsBaseInbound : IAPIMapperBaseInbound
{
    private readonly IAuthContext _authContext;

    public OperationsBaseInbound(
        IAuthContext authContext)
    {
        _authContext = authContext;

        CorrelationID = Guid.NewGuid().ToString();
    }

    /// <inheritdoc/>
    public string CorrelationID { get; init; }

    /// <inheritdoc/>
    public Task<InboundConfig> GetAppConfigAsync(string appId)
    {
        throw new NotImplementedException();
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
