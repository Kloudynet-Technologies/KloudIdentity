using KN.KloudIdentity.Mapper.Auth;
using KN.KloudIdentity.Mapper.Config;
using System.Net;
using System.Security.Authentication;
using System.Text.Json;

namespace KN.KloudIdentity.Mapper;

public class BasicAuthStrategy : IAuthStrategy
{
    public AuthenticationMethod AuthenticationMethod => AuthenticationMethod.Basic;

    public async Task<string> GetTokenAsync(AuthConfig authConfig)
    {
        HttpClient client = new HttpClient();

        // Create the request content with necessary parameters
        var requestContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("username", authConfig.Username),
            new KeyValuePair<string, string>("password", authConfig.Password),
            new KeyValuePair<string, string>("grant_type", authConfig.GrantType)
        });

        // Send the request
        HttpResponseMessage response = await client.PostAsync(authConfig.LoginUrl, requestContent);

        var responseContent = await response.Content.ReadAsStringAsync();

        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new AuthenticationException(responseContent);
        }

        var tokenResponse = JsonSerializer.Deserialize<HttpResponseMessage>(responseContent);

        return await tokenResponse.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Validates the parameters for Basic Auth.
    /// </summary>
    /// <param name="authConfig"></param>
    /// <exception cref="ArgumentNullException"></exception>
    private void ValidateParameters(AuthConfig authConfig)
    {
        if (authConfig == null)
        {
            throw new ArgumentNullException(nameof(authConfig));
        }

        if (string.IsNullOrWhiteSpace(authConfig.LoginUrl))
        {
            throw new ArgumentNullException(nameof(authConfig.LoginUrl));
        }

        if (string.IsNullOrWhiteSpace(authConfig.Password))
        {
            throw new ArgumentNullException(nameof(authConfig.Password));
        }

        if (string.IsNullOrWhiteSpace(authConfig.Username))
        {
            throw new ArgumentNullException(nameof(authConfig.Username));
        } 
    }
}
