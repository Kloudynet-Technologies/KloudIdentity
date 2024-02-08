﻿//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Config;
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

        /// <summary>
        /// Initializes a new instance of the CreateUser class.
        /// </summary>
        /// <param name="configReader">An implementation of IConfigReader for reading configuration settings.</param>
        /// <param name="authContext">An implementation of IAuthContext for handling authentication.</param>
        public UpdateUser(IAuthContext authContext, IHttpClientFactory httpClientFactory, IGetFullAppConfigQuery getFullAppConfigQuery)
            : base(authContext, getFullAppConfigQuery)
        {
            _httpClientFactory = httpClientFactory;
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

            var token = await GetAuthenticationAsync(authConfig);

            var httpClient = _httpClientFactory.CreateClient();

            httpClient = Utils.HttpClientExtensions.SetAuthenticationHeaders(httpClient, authConfig, token);

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

    }
}
