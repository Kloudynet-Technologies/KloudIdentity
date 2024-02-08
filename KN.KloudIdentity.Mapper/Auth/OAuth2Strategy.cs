//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using System.Security.Authentication;
using System.Text.Json;
using KN.KloudIdentity.Mapper.Auth;
using KN.KloudIdentity.Mapper.Config;
using KN.KloudIdentity.Mapper.Domain.Authentication;

namespace KN.KloudIdentity.Mapper;

/// <summary>
/// OAuth2 authentication strategy.
/// </summary>
public class OAuth2Strategy : IAuthStrategy
{
    public AuthenticationMethods AuthenticationMethod => AuthenticationMethods.OIDC_ClientCrd;

    /// <summary>
    /// Gets the auth token for OAuth2.
    /// </summary>
    /// <param name="authConfig"></param>
    /// <returns></returns>
    public async Task<string> GetTokenAsync(dynamic authConfig)
    {
        ValidateParameters(authConfig);

        var oauth2Auth = authConfig as OAuth2ClientCrdAuthentication;

        var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, oauth2Auth.TokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "client_id", oauth2Auth.ClientId },
                { "client_secret", oauth2Auth.ClientSecret },
                { "scope", oauth2Auth.Scopes?.FirstOrDefault() },
                { "grant_type", "client_credentials" }
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
    private void ValidateParameters(dynamic authConfig)
    {
        if (authConfig is null or not OAuth2ClientCrdAuthentication)
        {
            throw new ArgumentNullException(nameof(authConfig));
        }

        var oauth2Auth = authConfig as OAuth2ClientCrdAuthentication;

        if (string.IsNullOrWhiteSpace(oauth2Auth.TokenUrl))
        {
            throw new ArgumentNullException(nameof(oauth2Auth.TokenUrl));
        }

        if (string.IsNullOrWhiteSpace(oauth2Auth.ClientId))
        {
            throw new ArgumentNullException(nameof(oauth2Auth.ClientId));
        }

        if (string.IsNullOrWhiteSpace(oauth2Auth.ClientSecret))
        {
            throw new ArgumentNullException(nameof(oauth2Auth.ClientSecret));
        }
    }
}
