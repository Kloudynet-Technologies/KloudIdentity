﻿//------------------------------------------------------------
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
    /// Implementation of the IAddGroupMembers interface for adding members to a group.
    /// </summary>
    public class AddMembersToGroup : OperationsBase<Core2Group>, IAddGroupMembers
    {
        private MapperConfig _appConfig;
        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>
        /// Constructor for the AddMembersToGroup class.
        /// </summary>
        /// <param name="configReader">Configuration reader service.</param>
        /// <param name="authContext">Authentication context service.</param>
        public AddMembersToGroup(IConfigReader configReader, IAuthContext authContext, IHttpClientFactory httpClientFactory) : base(configReader, authContext)
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
        /// Adds the specified members to the group.
        /// </summary>
        /// <param name="groupId">ID of the group to which members should be added.</param>
        /// <param name="members">List of member IDs to be added.</param>
        /// <param name="appId">ID of the application.</param>
        /// <param name="correlationID">Correlation ID for tracking.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        public async Task AddAsync(string groupId, List<string> members, string appId, string correlationID)
        {
            // Set properties
            AppId = appId;
            CorrelationID = correlationID;

            _appConfig = await GetAppConfigAsync();

            await AddMembersToGroupAsync(groupId, members);
        }

        /// <summary>
        /// Asynchronously adds the specified members to the group using an HTTP PATCH request.
        /// </summary>
        /// <param name="groupId">ID of the group to which members should be added.</param>
        /// <param name="members">List of member IDs to be added.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        private async Task AddMembersToGroupAsync(string groupId, List<string> members)
        {
            var authConfig = _appConfig.AuthConfig;

            var token = await GetAuthenticationAsync(authConfig);

            var httpClient = _httpClientFactory.CreateClient();

            httpClient.SetAuthenticationHeaders(authConfig, token);

            // Construct the API path for adding members to the group
            var apiPath = DynamicApiUrlUtil.GetFullUrl(_appConfig.PATCHAPIForAddMemberToGroup, groupId);

            var jsonPayload = JsonConvert.SerializeObject(members);

            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            using (var response = await httpClient.PatchAsync(apiPath, content))
            { 
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"Error adding members to group: {response.StatusCode} - {response.ReasonPhrase}"
                    );
                }
            }
        }
    }

}