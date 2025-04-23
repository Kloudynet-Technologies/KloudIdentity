//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Common;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.SCIM;
using Newtonsoft.Json;
using System.Text;
using Serilog;

namespace KN.KloudIdentity.Mapper.MapperCore.Group
{
    /// <summary>
    /// Implementation of the IRemoveGroupMembers interface for removing members from a group.
    /// </summary>
    public class RemoveGroupMembers : OperationsBase<Core2Group>, IRemoveGroupMembers
    {
        private AppConfig _appConfig;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IKloudIdentityLogger _logger;

        /// <summary>
        /// Constructor for the RemoveMembersFromGroup class.
        /// </summary>
        /// <param name="configReader">Configuration reader service.</param>
        /// <param name="authContext">Authentication context service.</param>
        public RemoveGroupMembers(
            IAuthContext authContext,
            IHttpClientFactory httpClientFactory,
            IGetFullAppConfigQuery getFullAppConfigQuery,
            IKloudIdentityLogger logger) : base(authContext, getFullAppConfigQuery)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
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
            Log.Information($"Removing group members for {groupId}. AppId: {appId}, CorrelationID: {correlationID}");
            // Get application configuration
            _appConfig = await GetAppConfigAsync(appId);

            await RemoveMembersToGroupAsync(groupId, members, correlationID);

            _ = CreateLogAsync(_appConfig, groupId, correlationID);

            Log.Information($"Removed group members for {groupId}. AppId: {appId}, CorrelationID: {correlationID}");
        }

        /// <summary>
        /// Asynchronously removes the specified members from the group using an HTTP PATCH request.
        /// </summary>
        /// <param name="groupId">ID of the group from which members should be removed.</param>
        /// <param name="members">List of member IDs to be removed.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        private async Task RemoveMembersToGroupAsync(string groupId, List<string> members, string correlationID)
        {
            var groupURIs = _appConfig?.GroupURIs?.FirstOrDefault();

            // Get authentication configuration
            var authConfig = _appConfig.AuthenticationDetails;

            // Get authentication token
            var token = await GetAuthenticationAsync(authConfig, SCIMDirections.Outbound);

            // Use IHttpClientFactory to create an HttpClient instance
            var httpClient = _httpClientFactory.CreateClient();

            Utils.HttpClientExtensions.SetAuthenticationHeaders(httpClient, _appConfig.AuthenticationMethodOutbound,
                authConfig, token);

            // Construct the API path for adding members to the group
            var apiPath = DynamicApiUrlUtil.GetFullUrl(groupURIs!.Patch!.ToString(), groupId);

            var jsonPayload = JsonConvert.SerializeObject(members);

            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            using (var response = await httpClient.PatchAsync(apiPath, content))
            {
                if (!response.IsSuccessStatusCode)
                {
                    Log.Error(
                        "Error removing members from group. AppId: {AppId}, CorrelationID: {CorrelationID}, Identifier: {Identifier}, StatusCode: {StatusCode}, ReasonPhrase: {ReasonPhrase}",
                        _appConfig.AppId, correlationID, groupId, response.StatusCode,
                        response.ReasonPhrase);
                    throw new HttpRequestException(
                        $"Error removing members to group: {response.StatusCode} - {response.ReasonPhrase}"
                    );
                }
            }
        }

        private async Task CreateLogAsync(AppConfig appConfig, string identifier, string correlationID)
        {
            var eventInfo = $"Removed Members from the #{appConfig.AppName}({appConfig.AppId})";
            var logMessage = $"Removed members for the id {identifier}";

            var logEntity = new CreateLogEntity(
                appConfig.AppId,
                LogType.Deprovision.ToString(),
                LogSeverities.Information,
                eventInfo,
                logMessage,
                correlationID,
                AppConstant.LoggerName,
                DateTime.UtcNow,
                AppConstant.User,
                null,
                null
            );

            await _logger.CreateLogAsync(logEntity);
        }
    }
}