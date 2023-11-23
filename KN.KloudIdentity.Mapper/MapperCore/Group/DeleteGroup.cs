//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Config;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.SCIM;

namespace KN.KloudIdentity.Mapper.MapperCore.Group
{
    /// <summary>
    /// Deletes a group in the identity management system.
    /// </summary>
    public class DeleteGroup : OperationsBase<Core2Group>, IDeleteResource<Core2Group>
    {
        private MapperConfig _appConfig;

        /// <summary>
        /// Initializes a new instance of the DeleteUser class.
        /// </summary>
        /// <param name="configReader">An implementation of IConfigReader for reading configuration settings.</param>
        /// <param name="authContext">An implementation of IAuthContext for handling authentication.</param>
        public DeleteGroup(IConfigReader configReader, IAuthContext authContext) : base(configReader, authContext)
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
            AppId = appId;
            CorrelationID = correlationID;

            _appConfig = await GetAppConfigAsync();


            // Validate the request.
            ValidatedRequest(resourceIdentifier.Identifier, _appConfig);

            // Initiate the asynchronous deletion of a user/resource.
            await DeleteGroupAsync(resourceIdentifier.Identifier);

        }

        /// <summary>
        /// Map and prepare the payload to be sent to the API asynchronously.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
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
        private async Task DeleteGroupAsync(string identifier)
        {
            var authConfig = _appConfig.AuthConfig;

            var token = await GetAuthenticationAsync(authConfig);

            using (var httpClient = new HttpClient())
            {
                httpClient.SetAuthenticationHeaders(authConfig, token);

                var apiUrl = DynamicApiUrlUtil.GetFullUrl(_appConfig.DELETEAPIForGroups, identifier);
                var response = await httpClient.DeleteAsync(apiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"HTTP request failed with error: {response.StatusCode} - {response.ReasonPhrase}"
                    );
                }                
            }
        }

        /// <summary>
        /// Validates the request by checking if the identifier and DELETEAPIForGroups are null or empty.
        /// </summary>
        /// <param name="identifier">The identifier to be validated.</param>
        /// <param name="mapperConfig">The mapper configuration containing DELETEAPIForGroups.</param>
        /// <exception cref="ArgumentNullException">Thrown when the identifier or DELETEAPIForGroups is null or empty.</exception>
        private void ValidatedRequest(string identifier, MapperConfig mapperConfig)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new ArgumentNullException(nameof(identifier), "Identifier cannot be null or empty");
            }
            if (string.IsNullOrWhiteSpace(mapperConfig.DELETEAPIForGroups))
            {
                throw new ArgumentNullException(nameof(mapperConfig.DELETEAPIForGroups), "DELETEAPIForGroups cannot be null or empty");
            }
        }
    }
}
