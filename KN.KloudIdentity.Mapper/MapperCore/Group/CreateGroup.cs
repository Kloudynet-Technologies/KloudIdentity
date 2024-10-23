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

namespace KN.KloudIdentity.Mapper.MapperCore.Group
{
    /// <summary>
    /// Class for creating a new Core2Group resource.
    /// Implements the ICreateResource interface.
    /// </summary>
    public class CreateGroup : OperationsBase<Core2Group>, ICreateResource<Core2Group>
    {
        private AppConfig _appConfig;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IKloudIdentityLogger _logger;

        /// <summary>
        /// Initializes a new instance of the CreateGroup class.
        /// </summary>
        /// <param name="configReader">An implementation of IConfigReader for reading configuration settings.</param>
        /// <param name="authContext">An implementation of IAuthContext for handling authentication.</param>
        public CreateGroup(IAuthContext authContext,
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
        /// Executes the creation of a new group asynchronously.
        /// </summary>
        /// <param name="resource">The group resource to create.</param>
        /// <param name="appId">The ID of the application.</param>
        /// <param name="correlationID">The correlation ID.</param>
        /// <returns>The created group resource.</returns>
        public async Task<Core2Group> ExecuteAsync(Core2Group resource, string appId, string correlationID)
        {
            _appConfig = await GetAppConfigAsync(appId);

            var attributes = _appConfig.GroupAttributeSchemas?.Where(x => x.HttpRequestType == HttpRequestTypes.POST).ToList();

            var payload = await MapAndPreparePayloadAsync(attributes, resource);

            await CreateGroupAsync(payload);

            await CreateLogAsync(_appConfig, correlationID);

            return resource;
        }

        /// <summary>
        /// Asynchronously creates a new group by sending a request to the group provisioning API.
        /// Authentication is done using the authentication method specified in the application configuration.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="HttpRequestException">
        /// HTTP request failed with error: {response.StatusCode} - {response.ReasonPhrase}
        /// </exception>
        private async Task CreateGroupAsync(JObject payload)
        {
            var groupURIs = _appConfig?.GroupURIs?.FirstOrDefault();

            var token = await GetAuthenticationAsync(_appConfig, SCIMDirections.Outbound);

            var httpClient = _httpClientFactory.CreateClient();

            Utils.HttpClientExtensions.SetAuthenticationHeaders(httpClient, _appConfig.AuthenticationMethodOutbound, _appConfig.AuthenticationDetails, token);

            using (var response = await httpClient.PostAsJsonAsync(
                 groupURIs!.Post!.ToString(),
                 payload
             ))
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"Error creating group: {response.StatusCode} - {response.ReasonPhrase}"
                    );
                }
            }
        }

        private async Task CreateLogAsync(AppConfig appConfig, string correlationID)
        {
            var logMessage = $"Group created to the application #{appConfig.AppName}({appConfig.AppId})";

            var logEntity = new CreateLogEntity(
                appConfig.AppId,
                LogType.Provision.ToString(),
                LogSeverities.Information,
                "Group created successfully",
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
