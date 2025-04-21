using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Common;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using KN.KloudIdentity.Mapper.Domain.ExternalEndpoint;
using KN.KloudIdentity.Mapper.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

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

        public async Task<dynamic> ProcessAsync(dynamic payload, ExternalEndpointInfo endpointInfo,
            string correlationID, CancellationToken cancellationToken)
        {
            Validate(endpointInfo, correlationID);

            var httpClient = _httpClientFactory.CreateClient();

            AddAuthenticationHeaders(httpClient, endpointInfo);
            httpClient.DefaultRequestHeaders.Add("X-Correlation-ID", correlationID);
            httpClient.Timeout = TimeSpan.FromSeconds(5);

            using (var response =
                   await httpClient.PostAsJsonAsync(endpointInfo.EndpointUrl, payload as JObject, cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                {
                    Log.Error(
                        "Error occurred while executing custom logic. CorrelationID: {CorrelationID}, StatusCode: {StatusCode}, ReasonPhrase: {ReasonPhrase}",
                        correlationID, response.StatusCode, response.ReasonPhrase);
                    throw new HttpRequestException(
                        $"Error occurred in custom logic execution: {response.StatusCode} - {response.ReasonPhrase}");
                }

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var deserializedResponse = JsonConvert.DeserializeObject<dynamic>(responseContent!);

                if (deserializedResponse == null)
                {
                    Log.Error("External API response is null. CorrelationID: {CorrelationID}", correlationID);
                    throw new ArgumentNullException("External API response is null.");
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
                    httpClient.DefaultRequestHeaders.Add(endpointInfo.APIKeyAuth!.AuthHeaderName,
                        endpointInfo.APIKeyAuth!.APIKey);
                    break;

                case AuthenticationMethods.Bearer:
                    httpClient.DefaultRequestHeaders.Add("Authorization",
                        $"Bearer {endpointInfo.BearerAuth!.BearerToken}");
                    break;

                case AuthenticationMethods.None:
                    break;
            }
        }

        private void Validate(ExternalEndpointInfo endpointInfo, string correlationId)
        {
            if (string.IsNullOrEmpty(endpointInfo.EndpointUrl))
            {
                Log.Error(
                    "EndpointUrl is required but was null or empty. CorrelationID: {CorrelationID}, AppId: {AppId}",
                    correlationId, endpointInfo.AppId);
                throw new ArgumentNullException("EndpointUrl is required.", nameof(endpointInfo.EndpointUrl));
            }

            switch (endpointInfo.AuthenticationMethod)
            {
                case AuthenticationMethods.APIKey:
                    ValidateApiKeyAuth(endpointInfo.APIKeyAuth, correlationId, endpointInfo.AppId);
                    break;

                case AuthenticationMethods.Bearer:
                    ValidateBearerAuth(endpointInfo.BearerAuth, correlationId, endpointInfo.AppId);
                    break;

                case AuthenticationMethods.None:
                    break;

                default:
                    throw new NotSupportedException(
                        $"Invalid or unsupported authentication method: {endpointInfo.AuthenticationMethod}");
            }
        }

        private void ValidateApiKeyAuth(ExternalAPIKeyAuth? apiKeyAuth, string correlationId, string appId)
        {
            if (apiKeyAuth == null)
            {
                Log.Error(
                    "APIKeyAuth is required for APIKey authentication method. CorrelationID: {CorrelationID}, AppId: {AppId}",
                    correlationId, appId);
                throw new ArgumentNullException("APIKeyAuth is required for APIKey authentication method.");
            }

            if (string.IsNullOrEmpty(apiKeyAuth.AuthHeaderName))
            {
                Log.Error(
                    "AuthHeaderName is required for APIKey authentication method. CorrelationID: {CorrelationID}, AppId: {AppId}",
                    correlationId, appId);
                throw new ArgumentNullException("AuthHeaderName is required for APIKey authentication method.");
            }

            if (string.IsNullOrEmpty(apiKeyAuth.APIKey))
            {
                Log.Error(
                    "APIKey is required for APIKey authentication method. CorrelationID: {CorrelationID}, AppId: {AppId}",
                    correlationId, appId);
                throw new ArgumentNullException("APIKey is required for APIKey authentication method.");
            }
        }

        private void ValidateBearerAuth(ExternalBearerAuth? bearerAuth, string correlationId, string appId)
        {
            if (bearerAuth == null)
            {
                Log.Error(
                    "BearerAuth is required for Bearer authentication method. CorrelationID: {CorrelationID}, AppId: {AppId}",
                    correlationId, appId);
                throw new ArgumentNullException("BearerAuth is required for Bearer authentication method.");
            }

            if (string.IsNullOrEmpty(bearerAuth.BearerToken))
            {
                Log.Error(
                    "BearerToken is required for Bearer authentication method. CorrelationID: {CorrelationID}, AppId: {AppId}",
                    correlationId, appId);
                throw new ArgumentNullException("BearerToken is required for Bearer authentication method.");
            }
        }


        private async Task CreateLogAsync(ExternalEndpointInfo endpointInfo, string correlationID, string message)
        {
            var logMessage = $"Processing payload for application #{endpointInfo.AppId}: {message}";

            var logEntity = new CreateLogEntity(
                endpointInfo.AppId,
                LogType.Provision.ToString(),
                LogSeverities.Information,
                "Payload processed successfully for external API endpoint",
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