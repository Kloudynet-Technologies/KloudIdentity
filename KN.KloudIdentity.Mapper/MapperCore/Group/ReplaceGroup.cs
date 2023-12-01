//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Config;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.SCIM;
using System.Text;

namespace KN.KloudIdentity.Mapper.MapperCore.Group
{
    /// <summary>
    /// Responsible for replacing a group in the identity management system.
    /// </summary>
    public class ReplaceGroup : OperationsBase<Core2Group>, IReplaceResource<Core2Group>
    {
        private MapperConfig _mapperConfig;
        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>
        /// Constructor for the ReplaceGroup class.
        /// </summary>
        /// <param name="configReader">Configuration reader.</param>
        /// <param name="authContext">Authentication context.</param>
        public ReplaceGroup(IConfigReader configReader, IAuthContext authContext, IHttpClientFactory httpClientFactory) : base(configReader, authContext)
        {
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Asynchronously maps and prepares the payload for user replacement.
        /// </summary>
        public override async Task MapAndPreparePayloadAsync()
        {
            Payload = JSONParserUtil<Resource>.Parse(_mapperConfig.GroupSchema, Resource);
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
            AppId = appId;
            Resource = resource;
            CorrelationID = correlationID;

            _mapperConfig = await GetAppConfigAsync();

            await MapAndPreparePayloadAsync();

            await ReplaceGroupAsync();

            return resource;
        }

        /// <summary>
        /// Private asynchronous method for handling authentication and sending the user replacement request.
        /// </summary>
        private async Task ReplaceGroupAsync()
        {
            var authConfig = _mapperConfig.AuthConfig;

            var token = await GetAuthenticationAsync(authConfig);

            var httpClient = _httpClientFactory.CreateClient();

            httpClient.SetAuthenticationHeaders(authConfig, token);

            using (var response = await ProcessRequestAsync(_mapperConfig, httpClient))
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
        /// <param name="mapperConfig">The mapper configuration containing API details.</param>
        /// <param name="httpClient">The HTTP client used for making API requests.</param>
        /// <returns>
        /// A task representing the asynchronous operation. 
        /// The task result is an <see cref="HttpResponseMessage"/> if an HTTP request is made, or null if no request is made.
        /// </returns>
        private async Task<HttpResponseMessage?> ProcessRequestAsync(MapperConfig mapperConfig, HttpClient httpClient)
        {
            if (!string.IsNullOrWhiteSpace(mapperConfig.PUTAPIForGroups))
            {
                var apiPath = DynamicApiUrlUtil.GetFullUrl(mapperConfig.PUTAPIForGroups, Resource.Identifier);

                return await httpClient.PutAsJsonAsync(apiPath, Payload);
            }
            else if (!string.IsNullOrWhiteSpace(mapperConfig.PATCHAPIForGroups))
            {
                var apiPath = DynamicApiUrlUtil.GetFullUrl(mapperConfig.PATCHAPIForGroups, Resource.Identifier);
                var jsonPayload = Payload.ToString();
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                return await httpClient.PatchAsync(apiPath, content);
            }
            else
            {
                throw new ArgumentNullException("PUTAPIForGroups and PATCHAPIForGroups cannot both be null or empty");
            }
        }
    }
}
