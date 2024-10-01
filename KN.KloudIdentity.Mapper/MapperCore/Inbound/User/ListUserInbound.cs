
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Web.Http;
using KN.KloudIdentity.Mapper.Domain.Inbound;

namespace KN.KloudIdentity.Mapper.MapperCore.Inbound;

public class ListUserInbound : OperationsBaseInbound, IFetchInboundResources
{
    private readonly IAuthContext _authContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private InboundConfig _inboundConfig;
    public ListUserInbound(
        IAuthContext authContext,
        IHttpClientFactory httpClientFactory
        ) : base(authContext)
    {
        _authContext = authContext;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<JObject?> FetchInboundResourcesAsync(string appId, string correlationId, CancellationToken cancellationToken = default)
    {
        _inboundConfig = await GetAppConfigAsync(appId);

        if (_inboundConfig.ListUsersUrl != null && _inboundConfig.ListUsersUrl != string.Empty)
        {
            var token = await GetAuthenticationAsync(_inboundConfig, SCIMDirections.Inbound);

            var client = _httpClientFactory.CreateClient();
            Mapper.Utils.HttpClientExtensions.SetAuthenticationHeaders(client, _inboundConfig.AuthenticationMethodInbound, _inboundConfig.AuthenticationDetails, token, SCIMDirections.Inbound);

            var response = await client.GetAsync(_inboundConfig.ListUsersUrl);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();

                var users = JsonConvert.DeserializeObject<JObject>(content);

                return users;
            }
            else
            {
                throw new HttpResponseException(System.Net.HttpStatusCode.NotFound);
            }
        }
        else
        {
            throw new ApplicationException("List API for users is not configured.");
        }
    }
}
