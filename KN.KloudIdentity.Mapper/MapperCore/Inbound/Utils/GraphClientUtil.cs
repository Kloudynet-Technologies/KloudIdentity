using System.Net.Http.Headers;
using Microsoft.Identity.Client;

namespace KN.KloudIdentity.Mapper.MapperCore.Inbound;

public class GraphClientUtil : IGraphClientUtil
{
    private readonly HttpClient _graphHttpClient;

    public GraphClientUtil(IHttpClientFactory httpClientFactory)
    {
        _graphHttpClient = httpClientFactory.CreateClient();
    }

    public async Task<HttpClient> GetClientAsync(string tenantId, string clientId, string clientSecret)
    {
        var accessToken = await GetAccessTokenAsync(tenantId, clientId, clientSecret);
        _graphHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return _graphHttpClient;
    }

    private async Task<string> GetAccessTokenAsync(string tenantId, string clientId, string clientSecret)
    {
        var confidentialClient = ConfidentialClientApplicationBuilder
                .Create(clientId)
                .WithClientSecret(clientSecret)
                .WithAuthority(new Uri($"https://login.microsoftonline.com/{tenantId}"))
                .Build();

        var authResult = await confidentialClient.AcquireTokenForClient(new string[] { "https://graph.microsoft.com/.default" }).ExecuteAsync();
        var token = authResult.AccessToken;

        return token;
    }
}
