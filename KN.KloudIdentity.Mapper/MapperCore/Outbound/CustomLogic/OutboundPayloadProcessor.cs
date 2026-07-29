using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Common;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using KN.KloudIdentity.Mapper.Domain.ExternalEndpoint;
using KN.KloudIdentity.Mapper.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System.Text;

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

            dynamic result = endpointInfo.RequestBodyType switch
            {
                RequestBodyType.Json => await SendJsonAsync(httpClient, endpointInfo, payload, correlationID, cancellationToken),
                RequestBodyType.Xml => await SendXmlAsync(httpClient, endpointInfo, payload, correlationID, cancellationToken),
                _ => throw new NotSupportedException(
                    $"RequestBodyType '{endpointInfo.RequestBodyType}' is not supported for external custom-logic calls. AppId: {endpointInfo.AppId}")
            };

            Log.Information("Custom logic executed successfully. CorrelationID: {CorrelationID}", correlationID);
            _ = CreateLogAsync(endpointInfo, correlationID, "Custom logic executed successfully");

            return result;
        }

        /// <summary>
        /// Sends the payload as JSON (<c>application/json</c>) and returns the JSON-deserialized response.
        /// </summary>
        private async Task<dynamic> SendJsonAsync(HttpClient client, ExternalEndpointInfo endpointInfo, dynamic payload,
            string correlationID, CancellationToken cancellationToken)
        {
            var jsonPayload = payload as JObject;
            if (jsonPayload is null)
            {
                Log.Error(
                    "JSON custom-logic call requires a JObject payload. CorrelationID: {CorrelationID}, AppId: {AppId}",
                    correlationID, endpointInfo.AppId);
                throw new InvalidOperationException($"JSON custom-logic payload must be a JObject. AppId: {endpointInfo.AppId}");
            }

            using var response = await client.PostAsJsonAsync(endpointInfo.EndpointUrl, jsonPayload, cancellationToken);
            EnsureSuccess(response, correlationID);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var deserializedResponse = JsonConvert.DeserializeObject<dynamic>(responseContent!);

            if (deserializedResponse == null)
            {
                Log.Error("External API response is null. CorrelationID: {CorrelationID}", correlationID);
                throw new ArgumentNullException("External API response is null.");
            }

            return deserializedResponse;
        }

        /// <summary>
        /// Sends the payload as XML (<c>text/xml</c>) and returns the raw XML response body as a string
        /// (XML request → XML response; no JSON deserialization).
        /// </summary>
        private async Task<dynamic> SendXmlAsync(HttpClient client, ExternalEndpointInfo endpointInfo, dynamic payload,
            string correlationID, CancellationToken cancellationToken)
        {
            var xmlPayload = payload as string;
            if (string.IsNullOrWhiteSpace(xmlPayload))
            {
                Log.Error(
                    "XML custom-logic call requires a non-empty string payload. CorrelationID: {CorrelationID}, AppId: {AppId}",
                    correlationID, endpointInfo.AppId);
                throw new InvalidOperationException($"XML custom-logic payload must be a non-empty XML string. AppId: {endpointInfo.AppId}");
            }

            using var content = new StringContent(xmlPayload, Encoding.UTF8, "text/xml");
            using var response = await client.PostAsync(endpointInfo.EndpointUrl, content, cancellationToken);
            EnsureSuccess(response, correlationID);

            var responseXml = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(responseXml))
            {
                Log.Error("External API XML response is empty. CorrelationID: {CorrelationID}", correlationID);
                throw new InvalidOperationException($"External API returned an empty XML response. AppId: {endpointInfo.AppId}");
            }

            return responseXml;
        }

        /// <summary>
        /// Shared non-success handling: logs and throws <see cref="HttpRequestException"/> on a non-2xx response.
        /// </summary>
        private static void EnsureSuccess(HttpResponseMessage response, string correlationID)
        {
            if (!response.IsSuccessStatusCode)
            {
                Log.Error(
                    "Error occurred while executing custom logic. CorrelationID: {CorrelationID}, StatusCode: {StatusCode}, ReasonPhrase: {ReasonPhrase}",
                    correlationID, response.StatusCode, response.ReasonPhrase);
                throw new HttpRequestException(
                    $"Error occurred in custom logic execution: {response.StatusCode} - {response.ReasonPhrase}");
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