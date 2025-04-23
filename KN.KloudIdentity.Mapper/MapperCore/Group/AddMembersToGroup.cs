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
    /// Implementation of the IAddGroupMembers interface for adding members to a group.
    /// </summary>
    public class AddMembersToGroup : OperationsBase<Core2Group>, IAddGroupMembers
    {
        private AppConfig _appConfig;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IKloudIdentityLogger _logger;

        /// <summary>
        /// Constructor for the AddMembersToGroup class.
        /// </summary>
        /// <param name="configReader">Configuration reader service.</param>
        /// <param name="authContext">Authentication context service.</param>
        public AddMembersToGroup(IAuthContext authContext,
            IHttpClientFactory httpClientFactory,
            IGetFullAppConfigQuery getFullAppConfigQuery,
            IKloudIdentityLogger logger)
            : base(authContext, getFullAppConfigQuery)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
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
            Log.Information($"Adding group members for {groupId}. AppId: {appId}, CorrelationID: {correlationID}");
            _appConfig = await GetAppConfigAsync(appId);

            await AddMembersToGroupAsync(groupId, members, correlationID);

            _ = CreateLogAsync(_appConfig, groupId, correlationID);
            
            Log.Information($"Added group members for {groupId}. AppId: {appId}, CorrelationID: {correlationID}");
        }

        /// <summary>
        /// Asynchronously adds the specified members to the group using an HTTP PATCH request.
        /// </summary>
        /// <param name="groupId">ID of the group to which members should be added.</param>
        /// <param name="members">List of member IDs to be added.</param>
        /// <param name="correlationId"></param>
        /// <returns>Task representing the asynchronous operation.</returns>
        private async Task AddMembersToGroupAsync(string groupId, List<string> members, string correlationId)
        {
            var authConfig = _appConfig.AuthenticationDetails;

            var groupURIs = _appConfig.GroupURIs?.FirstOrDefault();

            var token = await GetAuthenticationAsync(_appConfig, SCIMDirections.Outbound);

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
                        "Error adding members to group. AppId: {AppId}, CorrelationID: {CorrelationID}, Identifier: {Identifier}, StatusCode: {StatusCode}, ReasonPhrase: {ReasonPhrase}",
                        _appConfig.AppId, correlationId, groupId, response.StatusCode,
                        response.ReasonPhrase);
                    throw new HttpRequestException(
                        $"Error adding members to group: {response.StatusCode} - {response.ReasonPhrase}"
                    );
                }
            }
        }

        private async Task CreateLogAsync(AppConfig appConfig, string identifier, string correlationId)
        {
            var eventInfo = $"Added Members from the #{appConfig.AppName}({appConfig.AppId})";
            var logMessage = $"Added members for the id {identifier}";

            var logEntity = new CreateLogEntity(
                appConfig.AppId,
                LogType.Deprovision.ToString(),
                LogSeverities.Information,
                eventInfo,
                logMessage,
                correlationId,
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