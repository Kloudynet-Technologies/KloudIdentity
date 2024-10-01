
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Web.Http;
using KN.KloudIdentity.Mapper.Domain.Inbound;
using KN.KloudIdentity.Mapper.MapperCore.Inbound.Utils;

namespace KN.KloudIdentity.Mapper.MapperCore.Inbound;

public class ListUserInbound : OperationsBaseInbound, IFetchInboundResources
{
    private readonly IAuthContext _authContext;
    private readonly IHttpClientFactory _httpClientFactory;
    public ListUserInbound(
        IAuthContext authContext,
        IHttpClientFactory httpClientFactory,
        IInboundMapper inboundMapper
        ) : base(authContext, inboundMapper)
    {
        _authContext = authContext;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<JObject?> FetchInboundResourcesAsync(InboundConfig inboundConfig, string correlationId, CancellationToken cancellationToken = default)
    {
        if (inboundConfig.IntegrationMethodInbound == IntegrationMethods.REST)
        {
            if (inboundConfig.IntegrationDetails.ListUsersUrl != null && inboundConfig.IntegrationDetails.ListUsersUrl != string.Empty)
            {
                var token = await GetAuthenticationAsync(inboundConfig, SCIMDirections.Inbound);

                var client = _httpClientFactory.CreateClient();
                Mapper.Utils.HttpClientExtensions.SetAuthenticationHeaders(client, inboundConfig.AuthenticationMethodInbound, inboundConfig.AuthenticationDetails, token, SCIMDirections.Inbound);

                var response = await client.GetAsync(inboundConfig.IntegrationDetails.ListUsersUrl);

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
        else
        {
            throw new ApplicationException("Integration method is not REST.");
        }
    }
}
