using System;
using System.Security.Authentication;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper;

public class DotRezAuthStrategy : IAuthStrategy
{
    private readonly IHttpClientFactory _httpClientFactory;

    public DotRezAuthStrategy(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public AuthenticationMethods AuthenticationMethod => AuthenticationMethods.DotRez;

    public async Task<string> GetTokenAsync(dynamic authConfig, dynamic[]? args = null)
    {
        ValidateParameters(authConfig, out OAuth2Authentication dotRezAuth);

        if (args == null || args.Length == 0 || args[0] == null)
        {
            throw new ArgumentException("Domain parameter is required for DotRez authentication.");
        }

        var tokenResponse = await RequestTokenAsync(dotRezAuth, args![0].ToString(), dotRezAuth.ClientId, dotRezAuth.ClientSecret);

        if (tokenResponse == null)
        {
            throw new AuthenticationException("Failed to obtain DotRez token.");
        }

        var result = new
        {
            apigeeToken = tokenResponse["apigeeToken"]?.ToString(),
            dotrezToken = tokenResponse["dotrezToken"]?.ToString()
        };

        return JsonConvert.SerializeObject(result);
    }

    private void ValidateParameters(dynamic authConfig, out OAuth2Authentication dotRezAuth)
    {
        dotRezAuth = JsonConvert.DeserializeObject<OAuth2Authentication>(authConfig.ToString());

        if (dotRezAuth == null)
        {
            throw new ArgumentException("Invalid authentication configuration.");
        }

        if (string.IsNullOrWhiteSpace(dotRezAuth.ClientId) ||
            string.IsNullOrWhiteSpace(dotRezAuth.ClientSecret) ||
            string.IsNullOrWhiteSpace(dotRezAuth.TokenUrl))
        {
            throw new ArgumentException("Missing required authentication parameters.");
        }
    }

    private async Task<JObject> RequestTokenAsync(OAuth2Authentication oauth2Auth, string domain, string username, string password)
    {
        var client = _httpClientFactory.CreateClient();

        // Create the request body as an anonymous object
        var requestBody = new
        {
            domain = domain ?? string.Empty,
            apigeeUsername = username ?? string.Empty,
            apigeePassword = password ?? string.Empty
        };

        var json = JsonConvert.SerializeObject(requestBody);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync(oauth2Auth.TokenUrl, content);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (string.IsNullOrEmpty(responseContent) || !response.IsSuccessStatusCode)
        {
            throw new AuthenticationException(responseContent);
        }

        return JsonConvert.DeserializeObject<JObject>(responseContent)!;
    }

    [Obsolete("Use GetTokenAsync(dynamic authConfig, dynamic[]? args) instead, as DotRez authentication requires additional parameters.")]
    public Task<string> GetTokenAsync(dynamic authConfig)
    {
        throw new NotSupportedException("Use GetTokenAsync(dynamic authConfig, dynamic[]? args) instead, as DotRez authentication requires additional parameters.");
    }
}
