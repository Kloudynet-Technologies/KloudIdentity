using KN.KloudIdentity.Mapper.Config;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.SCIM;
using System.Net.Http.Json;
using System.Text;

namespace KN.KloudIdentity.Mapper.MapperCore.User
{
    /// <summary>
    /// Class responsible for replacing a user in the identity management system.
    /// </summary>
    public class ReplaceUser
        : OperationsBase<Core2EnterpriseUser>,
            IReplaceResource<Core2EnterpriseUser>
    {
        private MapperConfig _mapperConfig;

        /// <summary>
        /// Constructor for the ReplaceUser class.
        /// </summary>
        /// <param name="configReader">Configuration reader.</param>
        /// <param name="authContext">Authentication context.</param>
        public ReplaceUser(IConfigReader configReader, IAuthContext authContext)
            : base(configReader, authContext)
        {
            // Constructor implementation.
        }

        /// <summary>
        /// Asynchronously maps and prepares the payload for user replacement.
        /// </summary>
        public override async Task MapAndPreparePayloadAsync()
        {
            Payload = JSONParserUtil<Resource>.Parse(_mapperConfig.UserSchema, Resource);
        }

        /// <summary>
        /// Asynchronously replaces a user in the system.
        /// </summary>
        /// <param name="resource">User object to replace.</param>
        /// <param name="appId">Application ID.</param>
        /// <param name="correlationID">Correlation ID for tracking.</param>
        /// <returns>The replaced Core2EnterpriseUser object.</returns>
        public async Task<Core2EnterpriseUser> ReplaceAsync(
            Core2EnterpriseUser resource,
            string appId,
            string correlationID
        )
        {
            AppId = appId;
            Resource = resource;
            CorrelationID = correlationID;

            _mapperConfig = await GetAppConfigAsync();

            await MapAndPreparePayloadAsync();

            await ReplaceUserAsync();

            return resource;
        }

        /// <summary>
        /// Private asynchronous method for handling authentication and sending the user replacement request.
        /// </summary>
        private async Task ReplaceUserAsync()
        {
            var authConfig = _mapperConfig.AuthConfig;

            // Obtain authentication token.
            var token = await GetAuthenticationAsync(authConfig);

            using (var httpClient = new HttpClient())
            {
                // Set headers based on authentication method.
                httpClient.SetAuthenticationHeaders(authConfig, token);

                var response = await ProcessRequestAsync(_mapperConfig, httpClient);

                // Check if the request was successful; otherwise, throw an exception.
                if (response != null && !response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"Error updating user: {response.StatusCode} - {response.ReasonPhrase}"
                    );
                }
            }
        }

        /// <summary>
        /// Processes the user replacement request based on the specified API configuration and HTTP client.
        /// </summary>
        /// <param name="mapperConfig">The mapper configuration containing API details.</param>
        /// <param name="httpClient">The HTTP client used for making API requests.</param>
        /// <returns>
        /// A task representing the asynchronous operation. 
        /// The task result is an <see cref="HttpResponseMessage"/> if an HTTP request is made, or null if no request is made.
        /// </returns>
        private async Task<HttpResponseMessage?> ProcessRequestAsync(MapperConfig mapperConfig, HttpClient httpClient)
        {
            if (!string.IsNullOrWhiteSpace(mapperConfig.PUTAPIForUsers))
            {
                var apiPath = DynamicApiUrlUtil.GetFullUrl(mapperConfig.PUTAPIForUsers, Resource.Identifier);

                return await httpClient.PutAsJsonAsync(apiPath, Payload);
            }
            else if (!string.IsNullOrWhiteSpace(mapperConfig.PATCHAPIForUsers))
            {
                var apiPath = DynamicApiUrlUtil.GetFullUrl(mapperConfig.PATCHAPIForUsers, Resource.Identifier);
                var jsonPayload = Payload.ToString(); 
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                return await httpClient.PatchAsync(apiPath, content);
            }
            else
            {
                throw new ArgumentNullException("PUTAPIForUsers and PATCHAPIForUsers cannot both be null or empty");
            }
        }

    }
}
