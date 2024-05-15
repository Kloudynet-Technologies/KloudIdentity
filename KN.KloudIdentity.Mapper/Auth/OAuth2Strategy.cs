//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using System.Security.Authentication;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using Microsoft.SCIM;
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

    public virtual async Task<string> GetTokenAsync(dynamic authConfig)
    {
        OAuth2Authentication oauth2Auth;
        ValidateParameters(authConfig, out oauth2Auth);

        switch (oauth2Auth.GrantType)
        {
            case OAuth2GrantTypes.ClientCredentials:
                return await GetClientCredentialsTokenAsync(oauth2Auth);
            case OAuth2GrantTypes.AuthorizationCode:
                return await GetAuthorizationCodeTokenAsync(oauth2Auth);
            case OAuth2GrantTypes.RefreshToken:
                return await GetRefreshTokenAsync(oauth2Auth);
            case OAuth2GrantTypes.DeviceCode:
                return await GetDeviceCodeTokenAsync(oauth2Auth);
            case OAuth2GrantTypes.PKCE:
                return await GetPKCETokenAsync(oauth2Auth);
            default:
                throw new ArgumentException("Unsupported grant type");
        }
    }

    public virtual async Task<string> GetClientCredentialsTokenAsync(OAuth2Authentication oauth2Auth)
    {
        var requestContent = new Dictionary<string, string>
                            {
                                { "client_id", oauth2Auth.ClientId },
                                { "client_secret", oauth2Auth.ClientSecret },
                                { "scope", string.Join(",", oauth2Auth.Scopes) },
                                { "grant_type", "client_credentials" }
                            };

        var tokenResponse = await RequestTokenAsync(oauth2Auth, requestContent);

        return "Bearer " + tokenResponse?.AccessToken;
    }

    public virtual async Task<string> GetAuthorizationCodeTokenAsync(OAuth2Authentication oauth2Auth)
    {
        // Implementation for Authorization Code grant type
        throw new NotImplementedException();
    }

    public virtual async Task<string> GetRefreshTokenAsync(OAuth2Authentication oauth2Auth)
    {
        var requestContent = new Dictionary<string, string>
                            {
                                { "client_id", oauth2Auth.ClientId },
                                { "client_secret", oauth2Auth.ClientSecret },
                                { "refresh_token", oauth2Auth.RefreshToken },
                                { "grant_type", "refresh_token" }
                            };

        var tokenResponse = await RequestTokenAsync(oauth2Auth, requestContent);
        return "Bearer " + tokenResponse?.AccessToken;
    }

    public virtual async Task<string> GetDeviceCodeTokenAsync(OAuth2Authentication oauth2Auth)
    {
        var client = new HttpClient();
        var requestContent = new Dictionary<string, string>
                            {
                                { "client_id", oauth2Auth.ClientId },
                                { "scope", string.Join(" ", oauth2Auth.Scopes) },
                                { "grant_type", "device_code" }
                            };

        var response = await client.PostAsync(oauth2Auth.TokenUrl, new FormUrlEncodedContent(requestContent));
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new AuthenticationException(responseContent);
        }

        var deviceCodeResponse = JsonConvert.DeserializeObject<DeviceCodeResponse>(responseContent);

        Console.WriteLine($"Please visit {deviceCodeResponse.VerificationUri} and enter the code {deviceCodeResponse.UserCode} to authenticate.");

        // Polling the token endpoint until the user has completed authentication
        while (true)
        {
            await Task.Delay(deviceCodeResponse.Interval * 1000); // Delay according to the interval provided by the response

            response = await client.PostAsync(oauth2Auth.TokenUrl, new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "client_id", oauth2Auth.ClientId },
                    { "device_code", deviceCodeResponse.DeviceCode },
                    { "grant_type", "device_code" }
                }));

            responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(responseContent);
                return "Bearer " + tokenResponse?.AccessToken;
            }
            else if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                var errorResponse = JsonConvert.DeserializeObject<OAuthError>(responseContent);
                if (errorResponse.Error == "authorization_pending")
                {
                    continue; // User has not yet completed authentication, continue polling
                }
                else if (errorResponse.Error == "slow_down")
                {
                    await Task.Delay(5000); // Slow down the polling according to the provided interval
                    continue;
                }
                else
                {
                    throw new AuthenticationException(errorResponse.ErrorDescription);
                }
            }
            else
            {
                throw new AuthenticationException("Unexpected response from token endpoint.");
            }
        }
    }


    public virtual async Task<string> GetPKCETokenAsync(OAuth2Authentication oauth2Auth)
    {
        // Implementation for PKCE grant type
        throw new NotImplementedException();
    }

    private async Task<TokenResponse> RequestTokenAsync(OAuth2Authentication oauth2Auth, Dictionary<string, string> requestContent)
    {
        var client = new HttpClient();

        var response = await client.PostAsync(oauth2Auth.TokenUrl, new FormUrlEncodedContent(requestContent));
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new AuthenticationException(responseContent);
        }

        return JsonConvert.DeserializeObject<TokenResponse>(responseContent);
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

        if (string.IsNullOrWhiteSpace(oauth2Auth.Authority))
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
            if (string.IsNullOrWhiteSpace(oauth2Auth.RefreshToken))
                throw new ArgumentNullException(nameof(oauth2Auth.RefreshToken));
        }
    }
}
