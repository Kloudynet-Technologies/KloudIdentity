//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Auth;
using KN.KloudIdentity.Mapper.Config;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using System.Net.Http.Headers;

namespace KN.KloudIdentity.Mapper.Utils
{
    public static class HttpClientExtensions
    {
        /// <summary>
        /// Extension method to set authentication headers based on the provided configuration.
        /// </summary>
        /// <param name="httpClient">The HttpClient instance.</param>
        /// <param name="authConfig">Authentication configuration.</param>
        /// <param name="token">Authentication token.</param>
        public static void SetAuthenticationHeaders(
            this HttpClient httpClient,
            dynamic authConfig,
            string token
        )
        {
            if (authConfig.AuthenticationMethod == AuthenticationMethod.None)
                return;

            if (authConfig.AuthenticationMethod == AuthenticationMethod.ApiKey)
            {
                var apiKeyAuth = authConfig as APIKeyAuthentication;

                if (string.IsNullOrWhiteSpace(apiKeyAuth.AuthHeaderName))
                {
                    throw new ArgumentNullException(
                        nameof(apiKeyAuth.AuthHeaderName),
                        "ApiKeyHeaderName cannot be null or empty when AuthenticationMethod is ApiKey"
                    );
                }

                httpClient.DefaultRequestHeaders.Add(apiKeyAuth.AuthHeaderName, token);
            }
            else
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                    token
                );
            }
        }
    }
}
