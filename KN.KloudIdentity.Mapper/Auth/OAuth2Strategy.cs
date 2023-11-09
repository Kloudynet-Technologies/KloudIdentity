//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using System.Security.Authentication;
using System.Text.Json;
using KN.KloudIdentity.Mapper.Auth;
using KN.KloudIdentity.Mapper.Config;

namespace KN.KloudIdentity.Mapper;

/// <summary>
/// OAuth2 authentication strategy.
/// </summary>
public class OAuth2Strategy : IAuthStrategy
{
    public AuthenticationMethod AuthenticationMethod => AuthenticationMethod.OAuth2;

    /// <summary>
    /// Gets the auth token for OAuth2.
    /// </summary>
    /// <param name="authConfig"></param>
    /// <returns></returns>
    public async Task<string> GetTokenAsync(AuthConfig authConfig)
    {
        ValidateParameters(authConfig);

        var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, authConfig.OAuth2TokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "client_id", authConfig.ClientId },
                { "client_secret", authConfig.ClientSecret },
                { "scope", authConfig.Scope },
                { "grant_type", authConfig.GrantType }
            })
        };

        var response = await client.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new AuthenticationException(responseContent);
        }

        var tokenResponse = JsonSerializer.Deserialize<HttpResponseMessage>(responseContent);

        string token = await tokenResponse.Content.ReadAsStringAsync();

        return "Bearer " + token;
    }

    /// <summary>
    /// Validates the parameters for OAuth2.
    /// </summary>
    /// <param name="authConfig"></param>
    /// <exception cref="ArgumentNullException"></exception>
    private void ValidateParameters(AuthConfig authConfig)
    {
        if (authConfig == null)
        {
            throw new ArgumentNullException(nameof(authConfig));
        }

        if (string.IsNullOrWhiteSpace(authConfig.OAuth2TokenUrl))
        {
            throw new ArgumentNullException(nameof(authConfig.OAuth2TokenUrl));
        }

        if (string.IsNullOrWhiteSpace(authConfig.ClientId))
        {
            throw new ArgumentNullException(nameof(authConfig.ClientId));
        }

        if (string.IsNullOrWhiteSpace(authConfig.ClientSecret))
        {
            throw new ArgumentNullException(nameof(authConfig.ClientSecret));
        }

        if (string.IsNullOrWhiteSpace(authConfig.Scope))
        {
            throw new ArgumentNullException(nameof(authConfig.Scope));
        }

        if (string.IsNullOrWhiteSpace(authConfig.GrantType))
        {
            throw new ArgumentNullException(nameof(authConfig.GrantType));
        }
    }
}
