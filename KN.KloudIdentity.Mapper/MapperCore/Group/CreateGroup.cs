//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper.MapperCore.Group
{
    /// <summary>
    /// Class for creating a new Core2Group resource.
    /// Implements the ICreateResource interface.
    /// </summary>
    public class CreateGroup : OperationsBase<Core2Group>, ICreateResource<Core2Group>
    {
        private AppConfig _appConfig;
        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>
        /// Initializes a new instance of the CreateGroup class.
        /// </summary>
        /// <param name="configReader">An implementation of IConfigReader for reading configuration settings.</param>
        /// <param name="authContext">An implementation of IAuthContext for handling authentication.</param>
        public CreateGroup(IAuthContext authContext, IHttpClientFactory httpClientFactory, IGetFullAppConfigQuery getFullAppConfigQuery)
            : base(authContext, getFullAppConfigQuery)
        {
            _httpClientFactory = httpClientFactory;
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
            _appConfig = await GetAppConfigAsync(appId);

            var payload = await MapAndPreparePayloadAsync(_appConfig.GroupAttributeSchemas!.ToList(), resource);

            await CreateGroupAsync(payload);

            return resource;
        }

        /// <summary>
        /// Asynchronously creates a new group by sending a request to the group provisioning API.
        /// Authentication is done using the authentication method specified in the application configuration.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="HttpRequestException">
        /// HTTP request failed with error: {response.StatusCode} - {response.ReasonPhrase}
        /// </exception>
        private async Task CreateGroupAsync(JObject payload)
        {
            var authConfig = _appConfig.AuthenticationDetails;

            var token = await GetAuthenticationAsync(authConfig);

            var httpClient = _httpClientFactory.CreateClient();

            httpClient = Utils.HttpClientExtensions.SetAuthenticationHeaders(httpClient, authConfig, token);

            using (var response = await httpClient.PostAsJsonAsync(
                 _appConfig.GroupURIs!.Post!.ToString(),
                 payload
             ))
            {
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
