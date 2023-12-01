//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Config;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.SCIM;
using Newtonsoft.Json;
using System.Text;

namespace KN.KloudIdentity.Mapper.MapperCore.Group
{
    /// <summary>
    /// Implementation of the IRemoveGroupMembers interface for removing members from a group.
    /// </summary>
    public class RemoveGroupMembers : OperationsBase<Core2Group>, IRemoveGroupMembers
    {
        private MapperConfig _appConfig;
        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>
        /// Constructor for the RemoveMembersFromGroup class.
        /// </summary>
        /// <param name="configReader">Configuration reader service.</param>
        /// <param name="authContext">Authentication context service.</param>
        public RemoveGroupMembers(
            IConfigReader configReader,
            IAuthContext authContext,
            IHttpClientFactory httpClientFactory) : base(configReader, authContext)
        {
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Implementation of the MapAndPreparePayloadAsync method.
        /// This method is not implemented in this class.
        /// </summary>
        /// <returns>Task representing the asynchronous operation.</returns>
        public override Task MapAndPreparePayloadAsync()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Removes the specified members from the group.
        /// </summary>
        /// <param name="groupId">ID of the group from which members should be removed.</param>
        /// <param name="members">List of member IDs to be removed.</param>
        /// <param name="appId">ID of the application.</param>
        /// <param name="correlationID">Correlation ID for tracking.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        public async Task RemoveAsync(string groupId, List<string> members, string appId, string correlationID)
        {
            // Set properties
            AppId = appId;
            CorrelationID = correlationID;

            // Get application configuration
            _appConfig = await GetAppConfigAsync();

            await RemoveMembersToGroupAsync(groupId, members);
        }

        /// <summary>
        /// Asynchronously removes the specified members from the group using an HTTP PATCH request.
        /// </summary>
        /// <param name="groupId">ID of the group from which members should be removed.</param>
        /// <param name="members">List of member IDs to be removed.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        private async Task RemoveMembersToGroupAsync(string groupId, List<string> members)
        {
            // Get authentication configuration
            var authConfig = _appConfig.AuthConfig;

            // Get authentication token
            var token = await GetAuthenticationAsync(authConfig);

            // Use IHttpClientFactory to create an HttpClient instance
            var httpClient = _httpClientFactory.CreateClient();

            httpClient.SetAuthenticationHeaders(authConfig, token);

            // Construct the API path for adding members to the group
            var apiPath = DynamicApiUrlUtil.GetFullUrl(_appConfig.PATCHAPIForRemoveMemberFromGroup, groupId);

            var jsonPayload = JsonConvert.SerializeObject(members);

            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            using (var response = await httpClient.PatchAsync(apiPath, content))
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"Error removing members to group: {response.StatusCode} - {response.ReasonPhrase}"
                    );
                }
            }
        }
    }

}
