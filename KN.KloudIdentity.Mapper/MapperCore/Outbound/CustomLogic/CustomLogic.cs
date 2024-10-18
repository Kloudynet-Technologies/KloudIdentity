using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Common;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using KN.KloudIdentity.Mapper.Domain.ExternalEndpoint;
using KN.KloudIdentity.Mapper.Utils;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper.MapperCore.Outbound.CustomLogic
{
    public class CustomLogic : ICustomLogic
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IKloudIdentityLogger _logger;

        public CustomLogic(IHttpClientFactory httpClientFactory, IKloudIdentityLogger logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<JObject> ProcessAsync(JObject payload, ExternalEndpointInfo endpointInfo, string correlationID, CancellationToken cancellationToken)
        {
            var httpClient = _httpClientFactory.CreateClient();

            if (endpointInfo.AuthenticationMethod == AuthenticationMethods.APIKey)
            {
                httpClient.DefaultRequestHeaders.Add(endpointInfo.APIKeyAuth!.AuthHeaderName, endpointInfo.APIKeyAuth!.APIKey);
            }

            if(endpointInfo.AuthenticationMethod == AuthenticationMethods.Bearer)
            {
                httpClient.DefaultRequestHeaders.Add("Bearer", endpointInfo.BearerAuth!.BearerToken);
            }

            using (var response = await httpClient.PostAsJsonAsync(endpointInfo.EndpointUrl, payload, cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Custom Logic Execution: {response.StatusCode} - {response.ReasonPhrase}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();

                var jsonToken = JToken.Parse(responseContent);

                _ = CreateLogAsync(endpointInfo, correlationID, "Custom logic executed successfully");

                return (JObject)jsonToken;
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
