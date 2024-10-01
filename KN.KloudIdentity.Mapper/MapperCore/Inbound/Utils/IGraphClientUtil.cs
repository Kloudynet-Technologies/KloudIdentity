namespace KN.KloudIdentity.Mapper.MapperCore.Inbound;

public interface IGraphClientUtil
{
    Task<HttpClient> GetClientAsync(string tenantId, string clientId, string clientSecret);
}
