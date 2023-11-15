using KN.KloudIdentity.Mapper.Auth;
using KN.KloudIdentity.Mapper.Config;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.SCIM;
using System.Net.Http.Headers;

namespace KN.KloudIdentity.Mapper.MapperCore.User
{
    public class DeleteUser : OperationsBase<Core2EnterpriseUser>, IDeleteResource<Core2EnterpriseUser>
    {
        private MapperConfig _appConfig;

        /// <summary>
        /// Initializes a new instance of the CreateUser class.
        /// </summary>
        /// <param name="configReader">An implementation of IConfigReader for reading configuration settings.</param>
        /// <param name="authContext">An implementation of IAuthContext for handling authentication.</param>
        public DeleteUser(IConfigReader configReader, IAuthContext authContext) : base(configReader, authContext)
        {
        }

        /// <summary>
        /// Initiates the asynchronous deletion of a resource using the provided resource identifier, application ID, and correlation ID.
        /// </summary>
        /// <param name="resourceIdentifier">The identifier of the resource to be deleted.</param>
        /// <param name="appId">The application ID associated with the operation.</param>
        /// <param name="correlationID">The correlation ID associated with the operation.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        public async Task DeleteAsync(IResourceIdentifier resourceIdentifier, string appId, string correlationID)
        {
            // Set application ID and correlation ID.
            AppId = appId;
            CorrelationID = correlationID;

            // Retrieve application configuration asynchronously.
            _appConfig = await GetAppConfigAsync();

            // Initiate the asynchronous deletion of a user/resource.
            await DeleteUserAsync(resourceIdentifier.Identifier);
        }

        public override Task MapAndPreparePayloadAsync()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Deletes a user asynchronously by making an HTTP DELETE request to the user provisioning API.
        /// </summary>
        /// <param name="identifier">The identifier of the user to be deleted.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the identifier is null or empty.</exception>
        /// <exception cref="HttpRequestException">Thrown when the HTTP request fails.</exception>
        private async Task DeleteUserAsync(string identifier)
        {
            var authConfig = _appConfig.AuthConfig;

            var token = await GetAuthenticationAsync(authConfig);

            using (var httpClient = new HttpClient())
            {
                httpClient.SetAuthenticationHeaders(authConfig, token);

                var response = await httpClient.DeleteAsync($"{_appConfig.UserProvisioningApiUrl}/{identifier}");

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"HTTP request failed with error: {response.StatusCode} - {response.ReasonPhrase}"
                    );
                }
            }
        }

    }
}
