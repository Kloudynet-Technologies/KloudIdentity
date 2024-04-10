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
using System.Text;

namespace KN.KloudIdentity.Mapper.MapperCore.Group
{
    /// <summary>
    /// Responsible for replacing a group in the identity management system.
    /// </summary>
    public class ReplaceGroup : OperationsBase<Core2Group>, IReplaceResource<Core2Group>
    {
        private AppConfig _appConfig;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IKloudIdentityLogger _logger;

        /// <summary>
        /// Constructor for the ReplaceGroup class.
        /// </summary>
        /// <param name="configReader">Configuration reader.</param>
        /// <param name="authContext">Authentication context.</param>
        public ReplaceGroup(
            IAuthContext authContext,
            IHttpClientFactory httpClientFactory,
            IGetFullAppConfigQuery getFullAppConfigQuery,
            IKloudIdentityLogger logger)
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
        public async Task<Core2Group> ReplaceAsync(
            Core2Group resource,
            string appId,
            string correlationID
        )
        {
            _appConfig = await GetAppConfigAsync(appId);

            var attributes = _appConfig.GroupAttributeSchemas?.Where(x => x.HttpRequestType == HttpRequestTypes.PUT &&
            x.SCIMDirection == SCIMDirections.Outbound);

            var payload = await MapAndPreparePayloadAsync(
               attributes!.ToList(),
                resource
            );

            await ReplaceGroupAsync(payload, resource);

            await CreateLogAsync(_appConfig, resource.Identifier, correlationID);

            return resource;
        }

        /// <summary>
        /// Private asynchronous method for handling authentication and sending the user replacement request.
        /// </summary>
        private async Task ReplaceGroupAsync(JObject payload, Core2Group resource)
        {
            var authConfig = _appConfig.AuthenticationDetails;

            var token = await GetAuthenticationAsync(authConfig);

            var httpClient = _httpClientFactory.CreateClient();

            Utils.HttpClientExtensions.SetAuthenticationHeaders(httpClient, _appConfig.AuthenticationMethod, authConfig, token);

            using (var response = await ProcessRequestAsync(_appConfig, httpClient, resource, payload))
            {
                if (response != null && !response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"Error updating group: {response.StatusCode} - {response.ReasonPhrase}"
                    );
                }
            }
        }

        /// <summary>
        /// Processes the group replacement request based on the specified API configuration and HTTP client.
        /// </summary>
        /// <param name="appConfig">The mapper configuration containing API details.</param>
        /// <param name="httpClient">The HTTP client used for making API requests.</param>
        /// <returns>
        /// A task representing the asynchronous operation. 
        /// The task result is an <see cref="HttpResponseMessage"/> if an HTTP request is made, or null if no request is made.
        /// </returns>
        private async Task<HttpResponseMessage?> ProcessRequestAsync(AppConfig appConfig, HttpClient httpClient, Core2Group resource, JObject payload)
        {
            if (appConfig.GroupURIs!.Put != null)
            {
                var apiPath = DynamicApiUrlUtil.GetFullUrl(appConfig.GroupURIs.Put.ToString(), resource.Identifier);

                return await httpClient.PutAsJsonAsync(apiPath, payload);
            }
            else if (appConfig.GroupURIs.Patch != null)
            {
                var apiPath = DynamicApiUrlUtil.GetFullUrl(appConfig.GroupURIs.Patch.ToString(), resource.Identifier);
                var jsonPayload = payload.ToString();
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                return await httpClient.PatchAsync(apiPath, content);
            }
            else
            {
                throw new ArgumentNullException("PUTAPIForGroups and PATCHAPIForGroups cannot both be null or empty");
            }
        }

        private async Task CreateLogAsync(AppConfig appConfig, string identifier, string correlationID)
        {
            var eventInfo = $"Replace Group to the #{appConfig.AppName}({appConfig.AppId})";
            var logMessage = $"Replace group for the id {identifier}";

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
