//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Domain.Authentication;
using System.Net.Http.Headers;

namespace KN.KloudIdentity.Mapper.Utils
{
    public static class HttpClientExtensionsV2
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
            Dictionary<AuthenticationMethods, string> tokens
        )
        {
            if (tokens == null || tokens.Count == 0)
                return;

            var handler = new AuthenticationHeaderHandler();

            foreach (var (method, token) in tokens)
            {
                if (string.IsNullOrWhiteSpace(token))
                    continue;

                handler.ApplyHeader(httpClient, method, authConfig, token);
            }             
           
        }
    }
}