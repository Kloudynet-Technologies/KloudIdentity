//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Domain.Authentication;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using Newtonsoft.Json;
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
            AuthenticationMethods method,
            dynamic authConfig,
            string token,
            SCIMDirections direction
        )
        {
            if (method == AuthenticationMethods.None)
                return;

            if (method == AuthenticationMethods.APIKey)
            {
                var apiKeyAuth = GetAuthConfig(authConfig, direction);

                if (apiKeyAuth.AuthHeaderName == null)
                {
                    throw new ArgumentNullException(
                        nameof(apiKeyAuth.AuthHeaderName),
                        "ApiKeyHeaderName cannot be null or empty when AuthenticationMethod is ApiKey"
                    );
                }

                httpClient.DefaultRequestHeaders.Add(apiKeyAuth.AuthHeaderName.ToString(), token);
            }
            else
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                    token
                );
            }
        }

        private static dynamic GetAuthConfig(dynamic authConfig, SCIMDirections direction)
        {
            var auths = JsonConvert.DeserializeObject<dynamic>(authConfig.ToString());

            return direction == SCIMDirections.Inbound ? auths.Inbound : auths.Outbound;
        }
    }
}
