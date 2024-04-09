using Microsoft.Graph;

namespace KN.KloudIdentity.Mapper.MapperCore.Inbound;

public interface IGraphClientUtil
{
    GraphServiceClient GetClient(string tenantId, string clientId, string clientSecret);
}
