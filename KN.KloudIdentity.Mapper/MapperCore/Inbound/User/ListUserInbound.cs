
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Web.Http;

namespace KN.KloudIdentity.Mapper.MapperCore.Inbound;

public class ListUserInbound : OperationsBaseInbound, IFetchInboundResources<JObject>
{
    private readonly IAuthContext _authContext;
    private readonly IGetFullAppConfigQuery _getFullAppConfigQuery;
    private readonly IHttpClientFactory _httpClientFactory;
    private AppConfig _appConfig;
    public ListUserInbound(
        IAuthContext authContext,
        IGetFullAppConfigQuery getFullAppConfigQuery,
        IHttpClientFactory httpClientFactory
        ) : base(authContext, getFullAppConfigQuery)
    {
        _authContext = authContext;
        _getFullAppConfigQuery = getFullAppConfigQuery;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IList<JObject>> FetchInboundResourcesAsync(string appId, string correlationId, CancellationToken cancellationToken = default)
    {
        _appConfig = await GetAppConfigAsync(appId, correlationId);

        var userURIs = _appConfig.UserURIs.Where(x => x.SCIMDirection == SCIMDirections.Inbound).FirstOrDefault();

        if (userURIs != null && userURIs.List != null)
        {
            var token = await GetAuthenticationAsync(_appConfig, SCIMDirections.Inbound);

            var client = _httpClientFactory.CreateClient();
            Mapper.Utils.HttpClientExtensions.SetAuthenticationHeaders(client, _appConfig.AuthenticationMethodInbound, _appConfig.AuthenticationDetails, token, SCIMDirections.Inbound);

            var response = await client.GetAsync(userURIs.List);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();

                var users = JsonConvert.DeserializeObject<IList<JObject>>(content);

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
