//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

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
    /// Deletes a group in the identity management system.
    /// </summary>
    public class DeleteGroup : OperationsBase<Core2Group>, IDeleteResource<Core2Group>
    {
        private AppConfig _appConfig;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IKloudIdentityLogger _logger;

        /// <summary>
        /// Initializes a new instance of the DeleteUser class.
        /// </summary>
        /// <param name="configReader">An implementation of IConfigReader for reading configuration settings.</param>
        /// <param name="authContext">An implementation of IAuthContext for handling authentication.</param>
        public DeleteGroup(IAuthContext authContext,
            IHttpClientFactory httpClientFactory,
            IGetFullAppConfigQuery getFullAppConfigQuery,
            IKloudIdentityLogger logger
            )
            : base(authContext, getFullAppConfigQuery)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
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
            _appConfig = await GetAppConfigAsync(appId);

            // Validate the request.
            ValidatedRequest(resourceIdentifier.Identifier, _appConfig);

            // Initiate the asynchronous deletion of a user/resource.
            await DeleteGroupAsync(resourceIdentifier.Identifier);

            // Log the operation.
            await CreateLogAsync(_appConfig, resourceIdentifier.Identifier, correlationID);
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
            var authConfig = _appConfig.AuthenticationDetails;

            var token = await GetAuthenticationAsync(authConfig);

            var httpClient = _httpClientFactory.CreateClient();

            Utils.HttpClientExtensions.SetAuthenticationHeaders(httpClient, _appConfig.AuthenticationMethodOutbound, authConfig, token);

            // Build the API URL.
            var apiUrl = DynamicApiUrlUtil.GetFullUrl(_appConfig.GroupURIs!.Delete!.ToString(), identifier);

            using (var response = await httpClient.DeleteAsync(apiUrl))
            {
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
        /// <param name="appConfig">The mapper configuration containing DELETEAPIForGroups.</param>
        /// <exception cref="ArgumentNullException">Thrown when the identifier or DELETEAPIForGroups is null or empty.</exception>
        private void ValidatedRequest(string identifier, AppConfig appConfig)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new ArgumentNullException(nameof(identifier), "Identifier cannot be null or empty");
            }
            if (appConfig.GroupURIs != null && appConfig.GroupURIs.Delete == null)
            {
                throw new ArgumentNullException(nameof(appConfig.GroupURIs.Delete), "DELETEAPIForGroups cannot be null or empty");
            }
        }

        private async Task CreateLogAsync(AppConfig appConfig, string identifier, string correlationID)
        {
            var eventInfo = $"Delete Group from the #{appConfig.AppName}({appConfig.AppId})";
            var logMessage = $"Delete group for the id {identifier}";

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
