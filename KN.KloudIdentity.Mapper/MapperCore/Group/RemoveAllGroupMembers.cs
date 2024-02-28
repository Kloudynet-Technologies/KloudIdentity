using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Common;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.SCIM;

namespace KN.KloudIdentity.Mapper.MapperCore.Group
{
    /// <summary>
    /// Class for removing all members from a group.
    /// </summary>
    public class RemoveAllGroupMembers : OperationsBase<Core2Group>, IRemoveAllGroupMembers
    {
        private AppConfig _appConfig;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IKloudIdentityLogger _logger;

        /// <summary>
        /// Constructor for RemoveAllGroupMembers class.
        /// </summary>
        /// <param name="configReader">The configuration reader.</param>
        /// <param name="authContext">The authentication context.</param>
        /// <param name="httpClientFactory">Factory for creating HttpClient instances.</param>
        public RemoveAllGroupMembers(
            IAuthContext authContext,
            IHttpClientFactory httpClientFactory,
            IGetFullAppConfigQuery getFullAppConfigQuery,
            IKloudIdentityLogger logger) : base(authContext, getFullAppConfigQuery)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
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
            _appConfig = await GetAppConfigAsync(appId);

            await RemoveAllGroupMembersAsync(groupId);

            _ = CreateLogAsync(_appConfig, groupId, correlationID);
        }

        /// <summary>
        /// Asynchronously removes all members from a group.
        /// </summary>
        /// <param name="groupId">The ID of the group from which members will be removed.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        /// <exception cref="Exception">Thrown if the removal operation fails.</exception>
        private async Task RemoveAllGroupMembersAsync(string groupId)
        {
            var authConfig = _appConfig.AuthenticationDetails;

            var token = await GetAuthenticationAsync(authConfig);

            var httpClient = _httpClientFactory.CreateClient();

            Utils.HttpClientExtensions.SetAuthenticationHeaders(httpClient, _appConfig.AuthenticationMethod, authConfig, token);

            var apiPath = DynamicApiUrlUtil.GetFullUrl(_appConfig.GroupURIs!.Patch!.ToString(), groupId);

            using (var response = await httpClient.PatchAsync(apiPath, null))
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to remove all members from group {groupId}.");
                }
            }
        }

        private async Task CreateLogAsync(AppConfig appConfig, string identifier, string correlationID)
        {
            var eventInfo = $"Removed Members from the #{appConfig.AppName}({appConfig.AppId})";
            var logMessage = $"Removed members for the id {identifier}";

            var logEntity = new CreateLogEntity(
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
