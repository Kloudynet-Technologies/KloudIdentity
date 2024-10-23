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
using Newtonsoft.Json.Linq;
using System.Net.Http.Json;
using System.Text;

namespace KN.KloudIdentity.Mapper.MapperCore.User
{
    /// <summary>
    /// Class responsible for replacing a user in the identity management system.
    /// </summary>
    [Obsolete("This class is deprecated, use ReplaceUserV2 instead.")]
    public class ReplaceUser
        : OperationsBase<Core2EnterpriseUser>,
            IReplaceResource<Core2EnterpriseUser>
    {
        private AppConfig _appConfig;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IKloudIdentityLogger _logger;

        /// <summary>
        /// Constructor for the ReplaceUser class.
        /// </summary>
        /// <param name="configReader">Configuration reader.</param>
        /// <param name="authContext">Authentication context.</param>
        public ReplaceUser(IAuthContext authContext,
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
        /// Asynchronously replaces a user in the system.
        /// </summary>
        /// <param name="resource">User object to replace.</param>
        /// <param name="appId">Application ID.</param>
        /// <param name="correlationID">Correlation ID for tracking.</param>
        /// <returns>The replaced Core2EnterpriseUser object.</returns>
        [Obsolete("This method is deprecated, use ReplaceUserV2.ReplaceAsync instead.")]
        public async Task<Core2EnterpriseUser> ReplaceAsync(
            Core2EnterpriseUser resource,
            string appId,
            string correlationID
        )
        {
            _appConfig = await GetAppConfigAsync(appId);

            var attributes = _appConfig.UserAttributeSchemas.Where(x => x.HttpRequestType == HttpRequestTypes.PUT).ToList();

            var payload = await MapAndPreparePayloadAsync(attributes, resource);

            await ReplaceUserAsync(payload, resource);

            await CreateLogAsync(_appConfig, resource.Identifier, correlationID);

            return resource;
        }

        /// <summary>
        /// Private asynchronous method for handling authentication and sending the user replacement request.
        /// </summary>
        private async Task ReplaceUserAsync(JObject payload, Core2EnterpriseUser resource)
        {

            // Obtain authentication token.
            var token = await GetAuthenticationAsync(_appConfig, SCIMDirections.Outbound);

            var httpClient = _httpClientFactory.CreateClient();

            // Set headers based on authentication method.
            Utils.HttpClientExtensions.SetAuthenticationHeaders(httpClient, _appConfig.AuthenticationMethodOutbound, _appConfig.AuthenticationDetails, token);

            using (var response = await ProcessRequestAsync(_appConfig, httpClient, payload, resource))
            {
                // Check if the request was successful; otherwise, throw an exception.
                if (response != null && !response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"Error updating user: {response.StatusCode} - {response.ReasonPhrase}"
                    );
                }
            }
        }

        /// <summary>
        /// Processes the user replacement request based on the specified API configuration and HTTP client.
        /// </summary>
        /// <param name="appConfig">The mapper configuration containing API details.</param>
        /// <param name="httpClient">The HTTP client used for making API requests.</param>
        /// <returns>
        /// A task representing the asynchronous operation. 
        /// The task result is an <see cref="HttpResponseMessage"/> if an HTTP request is made, or null if no request is made.
        /// </returns>
        private async Task<HttpResponseMessage?> ProcessRequestAsync(AppConfig appConfig, HttpClient httpClient, JObject payload, Core2EnterpriseUser resource)
        {
            var userURIs = _appConfig.UserURIs.FirstOrDefault();
            if (userURIs.Put != null)
            {
                var apiPath = DynamicApiUrlUtil.GetFullUrl(userURIs.Put!.ToString(), resource.Identifier);

                return await httpClient.PutAsJsonAsync(apiPath, payload);
            }
            else if (userURIs.Patch != null)
            {
                var apiPath = DynamicApiUrlUtil.GetFullUrl(userURIs.Patch.ToString(), resource.Identifier);
                var jsonPayload = payload.ToString();
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                return await httpClient.PatchAsync(apiPath, content);
            }
            else
            {
                throw new ArgumentNullException("PUTAPIForUsers and PATCHAPIForUsers cannot both be null or empty");
            }
        }

        private async Task CreateLogAsync(AppConfig appConfig, string identifier, string correlationID)
        {
            var eventInfo = $"Replace User to the #{appConfig.AppName}({appConfig.AppId})";
            var logMessage = $"Replace user for the id {identifier}";

            var logEntity = new CreateLogEntity(
                appConfig.AppId,
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
