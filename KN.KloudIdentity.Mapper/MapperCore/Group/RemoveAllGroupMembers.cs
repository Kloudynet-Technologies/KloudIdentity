﻿using KN.KloudIdentity.Mapper.Config;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.SCIM;

namespace KN.KloudIdentity.Mapper.MapperCore.Group
{
    /// <summary>
    /// Class for removing all members from a group.
    /// </summary>
    public class RemoveAllGroupMembers : OperationsBase<Core2Group>, IRemoveAllGroupMembers
    {
        private MapperConfig _appConfig;
        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>
        /// Constructor for RemoveAllGroupMembers class.
        /// </summary>
        /// <param name="configReader">The configuration reader.</param>
        /// <param name="authContext">The authentication context.</param>
        /// <param name="httpClientFactory">Factory for creating HttpClient instances.</param>
        public RemoveAllGroupMembers(
            IConfigReader configReader,
            IAuthContext authContext,
            IHttpClientFactory httpClientFactory) : base(configReader, authContext)
        {
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Not implemented in this class.
        /// </summary>
        /// <returns>NotImplementedException.</returns>
        /// <exception cref="NotImplementedException"></exception>
        public override Task MapAndPreparePayloadAsync()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Removes all members from a group asynchronously.
        /// </summary>
        /// <param name="groupId">The ID of the group from which members will be removed.</param>
        /// <param name="appId">The ID of the application.</param>
        /// <param name="correlationID">The correlation ID for tracking the operation.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        public async Task RemoveAsync(string groupId, string appId, string correlationID)
        {
            AppId = appId;
            CorrelationID = correlationID;

            _appConfig = await GetAppConfigAsync();

            await RemoveAllGroupMembersAsync(groupId);
        }

        /// <summary>
        /// Asynchronously removes all members from a group.
        /// </summary>
        /// <param name="groupId">The ID of the group from which members will be removed.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        /// <exception cref="Exception">Thrown if the removal operation fails.</exception>
        private async Task RemoveAllGroupMembersAsync(string groupId)
        {
            var authConfig = _appConfig.AuthConfig;

            var token = await GetAuthenticationAsync(authConfig);

            var httpClient = _httpClientFactory.CreateClient();

            httpClient.SetAuthenticationHeaders(authConfig, token);

            var apiPath = DynamicApiUrlUtil.GetFullUrl(_appConfig.PATCHAPIForRemoveAllMembersFromGroup, groupId);

            using (var response = await httpClient.PatchAsync(apiPath, null))
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to remove all members from group {groupId}.");
                }
            }
        }
    }

}