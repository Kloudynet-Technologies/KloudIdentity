//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Config;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.SCIM;

namespace KN.KloudIdentity.Mapper.MapperCore.Group
{
    /// <summary>
    /// Class for creating a new Core2Group resource.
    /// Implements the ICreateResource interface.
    /// </summary>
    public class CreateGroup : OperationsBase<Core2Group>, ICreateResource<Core2Group>
    {
        private MapperConfig _appConfig;

        /// <summary>
        /// Initializes a new instance of the CreateGroup class.
        /// </summary>
        /// <param name="configReader">An implementation of IConfigReader for reading configuration settings.</param>
        /// <param name="authContext">An implementation of IAuthContext for handling authentication.</param>
        public CreateGroup(IConfigReader configReader, IAuthContext authContext)
            : base(configReader, authContext)
        {
        }

        /// <summary>
        /// Executes the creation of a new group asynchronously.
        /// </summary>
        /// <param name="resource">The group resource to create.</param>
        /// <param name="appId">The ID of the application.</param>
        /// <param name="correlationID">The correlation ID.</param>
        /// <returns>The created group resource.</returns>
        public async Task<Core2Group> ExecuteAsync(Core2Group resource, string appId, string correlationID)
        {
            AppId = appId;
            Resource = resource;
            CorrelationID = correlationID;

            _appConfig = await GetAppConfigAsync();

            await MapAndPreparePayloadAsync();

            await CreateGroupAsync();

            return resource;
        }

        /// <summary>
        /// Map and prepare the payload to be sent to the API asynchronously.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public override async Task MapAndPreparePayloadAsync()
        {
            Payload = JSONParserUtil<Resource>.Parse(_appConfig.GroupSchema, Resource);
        }

        /// <summary>
        /// Asynchronously creates a new group by sending a request to the group provisioning API.
        /// Authentication is done using the authentication method specified in the application configuration.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="HttpRequestException">
        /// HTTP request failed with error: {response.StatusCode} - {response.ReasonPhrase}
        /// </exception>
        private async Task CreateGroupAsync()
        {
            var authConfig = _appConfig.AuthConfig;

            var token = await GetAuthenticationAsync(authConfig);

            using (var httpClient = new HttpClient())
            {
                httpClient.SetAuthenticationHeaders(authConfig, token);

                var response = await httpClient.PostAsJsonAsync(
                    _appConfig.GroupProvisioningApiUrl,
                    Payload
                );

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"Error creating group: {response.StatusCode} - {response.ReasonPhrase}"
                    );
                }
            }
        }
    }
}
