using KN.KloudIdentity.Mapper.Domain.Mapping;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Web.Http;
using KN.KloudIdentity.Mapper.Domain.Inbound;
using KN.KloudIdentity.Mapper.MapperCore.Inbound.Utils;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;

namespace KN.KloudIdentity.Mapper.MapperCore.Inbound;

public class ListUserInbound : OperationsBaseInbound, IFetchInboundResources
{
    private readonly IAuthContext _authContext;
    private readonly IHttpClientFactory _httpClientFactory;
    public ListUserInbound(
        IAuthContext authContext,
        IHttpClientFactory httpClientFactory,
        IInboundMapper inboundMapper,
        IGetInboundAppConfigQuery getInboundAppConfigQuery) : base(authContext, inboundMapper, getInboundAppConfigQuery)
    {
        _authContext = authContext;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<JObject?> FetchInboundResourcesAsync(InboundConfig inboundConfig, string correlationId, CancellationToken cancellationToken = default)
    {
        var restConfig = GetInboundRESTIntegrationConfig(inboundConfig);

        var token = await GetAuthenticationAsync(inboundConfig, SCIMDirections.Inbound);

        var client = _httpClientFactory.CreateClient();
        Mapper.Utils.HttpClientExtensions.SetAuthenticationHeaders(client, inboundConfig.AuthenticationMethodInbound, inboundConfig.AuthenticationDetails, token, SCIMDirections.Inbound);

        var response = await client.GetAsync(restConfig.UsersEndpoint);

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();

            // Parse the content to a JToken
            var jsonToken = JToken.Parse(content);

            // Check if the token is an array or an object
            if (jsonToken is JArray)
            {
                var usersArray = (JArray)jsonToken;
                var usersObject = new JObject
                {
                    ["users"] = usersArray
                };
                return usersObject;
            }
            else if (jsonToken is JObject)
            {
                var usersObject = (JObject)jsonToken;
                return usersObject;
            }
            else
            {
                throw new InvalidOperationException("Unexpected JSON format.");
            }
        }
        else
        {
            throw new HttpResponseException(System.Net.HttpStatusCode.NotFound);
        }
    }

    private InboundRESTIntegrationConfig GetInboundRESTIntegrationConfig(InboundConfig config)
    {
        var restConfig = JsonConvert.DeserializeObject<InboundRESTIntegrationConfig>(config.IntegrationDetails.ToString());

        return restConfig!;
    }
}
