using KN.KloudIdentity.Mapper.Auth;
using KN.KloudIdentity.Mapper.Config;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.SCIM;
using System.Net.Http.Headers;

namespace KN.KloudIdentity.Mapper.MapperCore.User
{
    /// <summary>
    /// Class responsible for replacing a user in the identity management system.
    /// </summary>
    public class ReplaceUser
        : OperationsBase<Core2EnterpriseUser>,
            IReplaceResource<Core2EnterpriseUser>
    {
        private MapperConfig _mapperConfig; // Configuration for the user replacement operation.

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

                // Send HTTP PUT request to update the user in the provisioning API.
                var response = await httpClient.PutAsJsonAsync(
                    $"{_mapperConfig.UserProvisioningApiUrl}/{Resource.Identifier}",
                    Payload
                );

                // Check if the request was successful; otherwise, throw an exception.
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"Error updating user: {response.StatusCode} - {response.ReasonPhrase}"
                    );
                }
            }
        }
    }
}
