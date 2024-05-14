//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using System.Security.Authentication;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using Newtonsoft.Json;

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
        OAuth2Authentication oauth2Auth;
        ValidateParameters(authConfig, out oauth2Auth);

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

        var tokenResponse = System.Text.Json.JsonSerializer.Deserialize<HttpResponseMessage>(responseContent);

        string token = await tokenResponse.Content.ReadAsStringAsync();

        return "Bearer " + token;
    }

    /// <summary>
    /// Validates the parameters for OAuth2.
    /// </summary>
    /// <param name="authConfig"></param>
    /// <exception cref="ArgumentNullException"></exception>
    private void ValidateParameters(dynamic authConfig, out OAuth2Authentication oauth2Auth)
    {
        oauth2Auth = JsonConvert.DeserializeObject<OAuth2Authentication>(authConfig.ToString());

        if (oauth2Auth is null || authConfig is null)
        {
            throw new ArgumentNullException(nameof(authConfig));
        }

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

        if(string.IsNullOrWhiteSpace(oauth2Auth.Authority))
        {
            throw new ArgumentNullException(nameof(oauth2Auth.Authority));
        }

        if (oauth2Auth.GrantType == OAuth2GrantTypes.AuthorizationCode)
        {
            if (string.IsNullOrWhiteSpace(oauth2Auth.AuthorizationCode))
                throw new ArgumentNullException(nameof(oauth2Auth.AuthorizationCode));

            if (string.IsNullOrWhiteSpace(oauth2Auth.RedirectUri))
                throw new ArgumentNullException(nameof(oauth2Auth.RedirectUri));
        }

        if (oauth2Auth.GrantType == OAuth2GrantTypes.RefreshToken)
        {
            if(string.IsNullOrWhiteSpace(oauth2Auth.RefreshToken))
                throw new ArgumentNullException(nameof(oauth2Auth.RefreshToken));
        }
    }
}
