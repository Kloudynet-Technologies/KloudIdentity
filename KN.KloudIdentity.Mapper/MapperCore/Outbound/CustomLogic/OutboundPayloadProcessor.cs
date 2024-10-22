using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Common;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using KN.KloudIdentity.Mapper.Domain.ExternalEndpoint;
using KN.KloudIdentity.Mapper.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper.MapperCore.Outbound.CustomLogic
{
    public class OutboundPayloadProcessor : IOutboundPayloadProcessor
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IKloudIdentityLogger _logger;

        public OutboundPayloadProcessor(IHttpClientFactory httpClientFactory, IKloudIdentityLogger logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<dynamic> ProcessAsync(dynamic payload, ExternalEndpointInfo endpointInfo, string correlationID, CancellationToken cancellationToken)
        {
            Validate(endpointInfo);

            var httpClient = _httpClientFactory.CreateClient();
            AddAuthenticationHeaders(httpClient, endpointInfo);
            httpClient.DefaultRequestHeaders.Add("X-Correlation-ID", correlationID);

            using (var response = await httpClient.PostAsJsonAsync(endpointInfo.EndpointUrl, payload as JObject, cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Custom Logic Execution: {response.StatusCode} - {response.ReasonPhrase}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var deserializedResponse = JsonConvert.DeserializeObject<dynamic>(responseContent!);

                if (deserializedResponse == null)
                {
                    throw new InvalidOperationException("Deserialization resulted in a null object.");
                }

                _ = CreateLogAsync(endpointInfo, correlationID, "Custom logic executed successfully");

                return deserializedResponse;
            }
        }

        private void AddAuthenticationHeaders(HttpClient httpClient, ExternalEndpointInfo endpointInfo)
        {
            switch (endpointInfo.AuthenticationMethod)
            {
                case AuthenticationMethods.APIKey:
                    httpClient.DefaultRequestHeaders.Add(endpointInfo.APIKeyAuth!.AuthHeaderName, endpointInfo.APIKeyAuth!.APIKey);
                    break;

                case AuthenticationMethods.Bearer:
                    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {endpointInfo.BearerAuth!.BearerToken}");
                    break;

                case AuthenticationMethods.None:
                    break;
            }
        }

        private void Validate(ExternalEndpointInfo endpointInfo)
        {
            if (string.IsNullOrEmpty(endpointInfo.EndpointUrl))
            {
                throw new ArgumentException("EndpointUrl is required.", nameof(endpointInfo.EndpointUrl));
            }

            switch (endpointInfo.AuthenticationMethod)
            {
                case AuthenticationMethods.APIKey:
                    ValidateApiKeyAuth(endpointInfo.APIKeyAuth);
                    break;

                case AuthenticationMethods.Bearer:
                    ValidateBearerAuth(endpointInfo.BearerAuth);
                    break;

                case AuthenticationMethods.None:
                    break;

                default:
                    throw new ArgumentException("Invalid or unsupported authentication method.", nameof(endpointInfo.AuthenticationMethod));
            }
        }

        private void ValidateApiKeyAuth(ExternalAPIKeyAuth? apiKeyAuth)
        {
            if (apiKeyAuth == null)
            {
                throw new ArgumentException("APIKeyAuth is required for APIKey authentication method.");
            }

            if (string.IsNullOrEmpty(apiKeyAuth.AuthHeaderName))
            {
                throw new ArgumentException("AuthHeaderName is required for APIKey authentication method.");
            }

            if (string.IsNullOrEmpty(apiKeyAuth.APIKey))
            {
                throw new ArgumentException("APIKey is required for APIKey authentication method.");
            }
        }

        private void ValidateBearerAuth(ExternalBearerAuth? bearerAuth)
        {
            if (bearerAuth == null)
            {
                throw new ArgumentException("BearerAuth is required for Bearer authentication method.");
            }

            if (string.IsNullOrEmpty(bearerAuth.BearerToken))
            {
                throw new ArgumentException("BearerToken is required for Bearer authentication method.");
            }
        }


        private async Task CreateLogAsync(ExternalEndpointInfo endpointInfo, string correlationID, string message)
        {
            var logMessage = $"Processing payload for application #{endpointInfo.AppId}: {message}";

            var logEntity = new CreateLogEntity(
                endpointInfo.AppId,
                LogType.Provision.ToString(),
                LogSeverities.Information,
                "Payload processed successfully",
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
