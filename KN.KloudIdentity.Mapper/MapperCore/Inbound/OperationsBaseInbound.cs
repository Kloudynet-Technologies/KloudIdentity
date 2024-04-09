using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper.MapperCore.Inbound;

public abstract class OperationsBaseInbound : IAPIMapperBaseInbound<JObject>
{
    private readonly IAuthContext _authContext;
    private readonly IGetFullAppConfigQuery _getFullAppConfigQuery;

    public OperationsBaseInbound(
        IAuthContext authContext,
        IGetFullAppConfigQuery getFullAppConfigQuery)
    {
        _authContext = authContext;
        _getFullAppConfigQuery = getFullAppConfigQuery;
    }

    public virtual async Task<AppConfig> GetAppConfigAsync(string appId, string correlationId)
    {
        var result = await _getFullAppConfigQuery.GetAsync(appId);
        if (result == null)
        {
            throw new KeyNotFoundException($"App configuration not found for app ID {appId}.");
        }

        return result;
    }

    public virtual async Task<string> GetAuthenticationAsync(AppConfig config, SCIMDirections direction)
    {
        return await _authContext.GetTokenAsync(config, direction);
    }

    public Task<JObject> MapAndPreparePayloadAsync(IList<AttributeSchema> schema, JObject resource)
    {
        throw new NotImplementedException();
    }
}
