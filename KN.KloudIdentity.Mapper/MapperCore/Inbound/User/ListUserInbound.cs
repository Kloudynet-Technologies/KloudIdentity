using KN.KloudIdentity.Mapper.Domain.Mapping;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Web.Http;
using KN.KloudIdentity.Mapper.Domain.Inbound;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Authentication;

namespace KN.KloudIdentity.Mapper.MapperCore.Inbound;

public class ListUserInbound : OperationsBaseInbound, IFetchInboundResources
{
    private readonly IAuthContext _authContext;
    private readonly IHttpClientFactory _httpClientFactory;
    public ListUserInbound(
        IAuthContext authContext,
        IHttpClientFactory httpClientFactory,
        IGetInboundAppConfigQuery getInboundAppConfigQuery
        ) : base(authContext, getInboundAppConfigQuery)
    {
        _authContext = authContext;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<JObject?> FetchInboundResourcesAsync(string appId, string correlationId, CancellationToken cancellationToken = default)
    {
        var inboundConfig = await GetAppConfigAsync(appId);

        var restConfig = GetInboundRESTIntegrationConfig(inboundConfig);

        if (!string.IsNullOrEmpty(restConfig.UsersEndpoint))
        {
            var token = await GetAuthenticationAsync(inboundConfig, SCIMDirections.Inbound);

            var client = _httpClientFactory.CreateClient();
            Mapper.Utils.HttpClientExtensions.SetAuthenticationHeaders(client, inboundConfig.AuthenticationMethodInbound, inboundConfig.AuthenticationDetails, token, SCIMDirections.Inbound);

            var response = await client.GetAsync(restConfig.UsersEndpoint);

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

    private InboundRESTIntegrationConfig GetInboundRESTIntegrationConfig(InboundConfig config)
    {
        var restConfig = JsonConvert.DeserializeObject<InboundRESTIntegrationConfig>(config.IntegrationDetails.ToString());

        return restConfig!;
    }
}
