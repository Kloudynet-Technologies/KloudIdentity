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
using Serilog;

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
            Log.Information(
                "Executing DeleteGroup for {ResourceIdentifier}. AppId: {AppId}, CorrelationID: {CorrelationID}",
                resourceIdentifier, appId, correlationID);
            _appConfig = await GetAppConfigAsync(appId);

            // Validate the request.
            ValidatedRequest(resourceIdentifier.Identifier, _appConfig, correlationID);

            // Initiate the asynchronous deletion of a user/resource.
            await DeleteGroupAsync(resourceIdentifier.Identifier, correlationID);

            // Log the operation.
            _ = CreateLogAsync(_appConfig, resourceIdentifier.Identifier, correlationID);
            Log.Information(
                "DeleteGroup operation completed successfully for {ResourceIdentifier}. AppId: {AppId}, CorrelationID: {CorrelationID}",
                resourceIdentifier, appId, correlationID);
        }

        /// <summary>
        /// Deletes a user asynchronously by making an HTTP DELETE request to the user provisioning API.
        /// </summary>
        /// <param name="identifier">The identifier of the user to be deleted.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the identifier is null or empty.</exception>
        /// <exception cref="HttpRequestException">Thrown when the HTTP request fails.</exception>
        private async Task DeleteGroupAsync(string identifier, string correlationID)
        {
            var groupURIs = _appConfig?.GroupURIs?.FirstOrDefault();

            var authConfig = _appConfig?.AuthenticationDetails;

            var token = await GetAuthenticationAsync(_appConfig, SCIMDirections.Outbound);

            var httpClient = _httpClientFactory.CreateClient();

            Utils.HttpClientExtensions.SetAuthenticationHeaders(httpClient, _appConfig.AuthenticationMethodOutbound,
                authConfig, token);

            // Build the API URL.
            var apiUrl = DynamicApiUrlUtil.GetFullUrl(groupURIs!.Delete!.ToString(), identifier);

            using (var response = await httpClient.DeleteAsync(apiUrl))
            {
                if (!response.IsSuccessStatusCode)
                {
                    Log.Error(
                        "Error deleting group. Identifier: {Identifier}, AppId: {AppId}, CorrelationID: {CorrelationID}, StatusCode: {StatusCode}, ReasonPhrase: {ReasonPhrase}",
                        identifier, _appConfig.AppId, correlationID, response.StatusCode, response.ReasonPhrase);
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
        private void ValidatedRequest(string identifier, AppConfig appConfig, string correlationID)
        {
            var groupURIs = appConfig?.GroupURIs?.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(identifier))
            {
                Log.Error(
                    "No identifier provided for {ResourceIdentifier}. AppId: {AppId}, CorrelationID: {CorrelationID}",
                    identifier, appConfig?.AppId, correlationID);
                throw new ArgumentNullException(nameof(identifier), "Identifier cannot be null or empty");
            }

            if (groupURIs != null && groupURIs.Delete == null)
            {
                Log.Error(
                    "No DELETEAPIForGroups provided for {ResourceIdentifier}. AppId: {AppId}, CorrelationID: {CorrelationID}",
                    identifier, appConfig?.AppId, correlationID);
                throw new ArgumentNullException(nameof(groupURIs.Delete), "DELETEAPIForGroups cannot be null or empty");
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