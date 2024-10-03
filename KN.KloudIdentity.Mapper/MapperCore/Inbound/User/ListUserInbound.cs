using KN.KloudIdentity.Mapper.Domain.Mapping;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Web.Http;
using KN.KloudIdentity.Mapper.Domain.Inbound;
using KN.KloudIdentity.Mapper.MapperCore.Inbound.Utils;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KI.LogAggregator.Library;

namespace KN.KloudIdentity.Mapper.MapperCore.Inbound;

public class ListUserInbound : OperationsBaseInbound, IFetchInboundResources
{
    private readonly IAuthContext _authContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IKloudIdentityLogger _logger;

    public ListUserInbound(
        IAuthContext authContext,
        IHttpClientFactory httpClientFactory,
        IInboundMapper inboundMapper,
        IGetInboundAppConfigQuery getInboundAppConfigQuery,
        IKloudIdentityLogger logger) : base(authContext, inboundMapper, getInboundAppConfigQuery, logger)
    {
        _authContext = authContext;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<JObject?> FetchInboundResourcesAsync(InboundConfig inboundConfig, string correlationId, CancellationToken cancellationToken = default)
    {
        _ = CreateLogAsync(inboundConfig.AppId, LogSeverities.Information, "ListUserInbound started", correlationId);

        var restConfig = GetInboundRESTIntegrationConfig(inboundConfig);

        var token = await GetAuthenticationAsync(inboundConfig, SCIMDirections.Inbound);

        var client = _httpClientFactory.CreateClient();
        Mapper.Utils.HttpClientExtensions.SetAuthenticationHeaders(client, inboundConfig.AuthenticationMethodInbound, inboundConfig.AuthenticationDetails, token, SCIMDirections.Inbound);

        var response = await client.GetAsync(restConfig.UsersEndpoint);

        if (response.IsSuccessStatusCode)
        {
            _ = CreateLogAsync(inboundConfig.AppId, LogSeverities.Information, "ListUserInbound fetched users", correlationId);

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
                _ = CreateLogAsync(inboundConfig.AppId, LogSeverities.Error, "Unexpected JSON format", correlationId);

                throw new InvalidOperationException("Unexpected JSON format.");
            }
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _ = CreateLogAsync(inboundConfig.AppId, LogSeverities.Error, $"Error fetching users: {response.StatusCode}, {errorContent}", correlationId);

            throw new HttpResponseException(response.StatusCode);
        }
    }

    private InboundRESTIntegrationConfig GetInboundRESTIntegrationConfig(InboundConfig config)
    {
        var restConfig = JsonConvert.DeserializeObject<InboundRESTIntegrationConfig>(config.IntegrationDetails.ToString());

        return restConfig!;
    }

    private async Task CreateLogAsync(string appId, LogSeverities severity, string message, string correlationId)
    {
        await _logger.CreateLogAsync(new CreateLogEntity
        (
            appId,
            "Inbound",
            severity,
            "ListUserInbound",
            message,
            correlationId,
            "KN.KloudIdentity",
            DateTime.UtcNow,
            "SYSTEM",
            null,
            null
        ));
    }
}
