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
using Newtonsoft.Json.Linq;
using System.Text;

namespace KN.KloudIdentity.Mapper.MapperCore.User
{
    public class UpdateUser
        : OperationsBase<Core2EnterpriseUser>,
            IUpdateResource<Core2EnterpriseUser>
    {
        private AppConfig _appConfig;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IKloudIdentityLogger _logger;

        /// <summary>
        /// Initializes a new instance of the CreateUser class.
        /// </summary>
        /// <param name="configReader">An implementation of IConfigReader for reading configuration settings.</param>
        /// <param name="authContext">An implementation of IAuthContext for handling authentication.</param>
        public UpdateUser(IAuthContext authContext,
            IHttpClientFactory httpClientFactory,
            IGetFullAppConfigQuery getFullAppConfigQuery,
            IKloudIdentityLogger logger)
            : base(authContext, getFullAppConfigQuery)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task UpdateAsync(IPatch patch, string appId, string correlationID)
        {
            PatchRequest2 patchRequest = patch.PatchRequest as PatchRequest2;

            Core2EnterpriseUser user = new Core2EnterpriseUser();
            user.Apply(patchRequest);
            user.Identifier = patch.ResourceIdentifier.Identifier;

            _appConfig = await GetAppConfigAsync(appId);

            var payload = await MapAndPreparePayloadAsync(_appConfig.UserAttributeSchemas.ToList(), user);

            await UpdateUserAsync(user, payload);

            await CreateLogAsync(_appConfig, user.Identifier, correlationID);
        }

        /// <summary>
        /// Asynchronously updates a user by sending a request to the user provisioning API.
        /// Authentication is done using the authentication method specified in the application configuration.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="HttpRequestException">
        /// HTTP request failed with error: {response.StatusCode} - {response.ReasonPhrase}
        /// </exception>
        private async Task UpdateUserAsync(Core2EnterpriseUser resource, JObject payload)
        {
            var authConfig = _appConfig.AuthenticationDetails;

            var token = await GetAuthenticationAsync(_appConfig);

            var httpClient = _httpClientFactory.CreateClient();

            Utils.HttpClientExtensions.SetAuthenticationHeaders(httpClient, _appConfig.AuthenticationMethod, authConfig, token);

            var apiPath = DynamicApiUrlUtil.GetFullUrl(_appConfig.UserURIs.Patch!.ToString(), resource.Identifier);

            var jsonPayload = payload.ToString();

            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            using (var response = await httpClient.PatchAsync(apiPath, content))
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"Error updating user: {response.StatusCode} - {response.ReasonPhrase}"
                    );
                }
            }
        }

        private async Task CreateLogAsync(AppConfig appConfig, string identifier, string correlationID)
        {
            var eventInfo = $"Updated User to the #{appConfig.AppName}({appConfig.AppId})";
            var logMessage = $"Updated user for the id {identifier}";

            var logEntity = new CreateLogEntity(
                identifier,
                LogType.Edit.ToString(),
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
