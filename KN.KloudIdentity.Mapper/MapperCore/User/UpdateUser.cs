//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Config;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.SCIM;
using System.Text;

namespace KN.KloudIdentity.Mapper.MapperCore.User
{
    public class UpdateUser
        : OperationsBase<Core2EnterpriseUser>,
            IUpdateResource<Core2EnterpriseUser>
    {
        private MapperConfig _appConfig;

        /// <summary>
        /// Initializes a new instance of the CreateUser class.
        /// </summary>
        /// <param name="configReader">An implementation of IConfigReader for reading configuration settings.</param>
        /// <param name="authContext">An implementation of IAuthContext for handling authentication.</param>
        public UpdateUser(IConfigReader configReader, IAuthContext authContext)
            : base(configReader, authContext) { }

        public override async Task MapAndPreparePayloadAsync()
        {
            Payload = JSONParserUtil<Resource>.Parse(_appConfig.UserSchema, Resource);
        }

        public async Task<Resource> UpdateAsync(
            Resource resource,
            string appId,
            string correlationID
        )
        {
            AppId = appId;
            Resource = (Core2EnterpriseUser)resource;
            CorrelationID = correlationID;

            _appConfig = await GetAppConfigAsync();

            await MapAndPreparePayloadAsync();

            await UpdateUserAsync();

            return resource;
        }

        /// <summary>
        /// Asynchronously updates a user by sending a request to the user provisioning API.
        /// Authentication is done using the authentication method specified in the application configuration.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="HttpRequestException">
        /// HTTP request failed with error: {response.StatusCode} - {response.ReasonPhrase}
        /// </exception>
        private async Task UpdateUserAsync()
        {
            var authConfig = _appConfig.AuthConfig;

            var token = await GetAuthenticationAsync(authConfig);

            using (var httpClient = new HttpClient())
            {

                httpClient.SetAuthenticationHeaders(authConfig, token);

                var apiPath = DynamicApiUrlUtil.GetFullUrl(_appConfig.PATCHAPIForUsers, Resource.Identifier);
                var jsonPayload = Payload.ToString();
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await httpClient.PatchAsync(apiPath, content);

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
