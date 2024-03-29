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

namespace KN.KloudIdentity.Mapper.MapperCore.User
{
    public class DeleteUser : OperationsBase<Core2EnterpriseUser>, IDeleteResource<Core2EnterpriseUser>
    {
        private AppConfig _appConfig;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IKloudIdentityLogger _logger;

        /// <summary>
        /// Initializes a new instance of the CreateUser class.
        /// </summary>
        /// <param name="configReader">An implementation of IConfigReader for reading configuration settings.</param>
        /// <param name="authContext">An implementation of IAuthContext for handling authentication.</param>
        public DeleteUser(IAuthContext authContext,
            IHttpClientFactory httpClientFactory,
            IGetFullAppConfigQuery getFullAppConfigQuery,
            IKloudIdentityLogger logger)
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
            // Retrieve application configuration asynchronously.
            _appConfig = await GetAppConfigAsync(appId);

            // Validate the request.
            ValidatedRequest(resourceIdentifier.Identifier, _appConfig);

            // Initiate the asynchronous deletion of a user/resource.
            await DeleteUserAsync(resourceIdentifier.Identifier);

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
        private async Task DeleteUserAsync(string identifier)
        {
            var userURIs = _appConfig.UserURIs.FirstOrDefault(x => x.SCIMDirection == SCIMDirections.Outbound);

            var token = await GetAuthenticationAsync(_appConfig);

            var httpClient = _httpClientFactory.CreateClient();

            Utils.HttpClientExtensions.SetAuthenticationHeaders(httpClient, _appConfig.AuthenticationMethod, _appConfig.AuthenticationDetails, token);

            var apiUrl = DynamicApiUrlUtil.GetFullUrl(userURIs.Delete!.ToString(), identifier);

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
        /// Validates the request by checking if the identifier and DELETEAPIForUsers are null or empty.
        /// </summary>
        /// <param name="identifier">The identifier to be validated.</param>
        /// <param name="appConfig">The mapper configuration containing DELETEAPIForUsers.</param>
        /// <exception cref="ArgumentNullException">Thrown when the identifier or DELETEAPIForUsers is null or empty.</exception>
        private void ValidatedRequest(string identifier, AppConfig appConfig)
        {
            var userURIs = _appConfig.UserURIs.FirstOrDefault(x => x.SCIMDirection == SCIMDirections.Outbound);

            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new ArgumentNullException(nameof(identifier), "Identifier cannot be null or empty");
            }
            if (userURIs == null || userURIs.Delete == null)
            {
                throw new ArgumentNullException(nameof(userURIs.Delete), "Delete endpoint cannot be null or empty");
            }
        }

        private async Task CreateLogAsync(AppConfig appConfig, string identifier, string correlationID)
        {
            var eventInfo = $"Delete User from the #{appConfig.AppName}({appConfig.AppId})";
            var logMessage = $"Delete user for the id {identifier}";

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
