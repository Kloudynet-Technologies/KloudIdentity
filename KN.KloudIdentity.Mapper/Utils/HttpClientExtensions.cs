using KN.KloudIdentity.Mapper.Auth;
using KN.KloudIdentity.Mapper.Config;
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
            AuthConfig authConfig,
            string token
        )
        {
            if (authConfig.AuthenticationMethod == AuthenticationMethod.None)
                return;

            if (authConfig.AuthenticationMethod == AuthenticationMethod.ApiKey)
            {
                if (string.IsNullOrWhiteSpace(authConfig.ApiKeyHeader))
                {
                    throw new ArgumentNullException(
                        nameof(authConfig.ApiKeyHeader),
                        "ApiKeyHeaderName cannot be null or empty when AuthenticationMethod is ApiKey"
                    );
                }

                httpClient.DefaultRequestHeaders.Add(authConfig.ApiKeyHeader, token);
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
