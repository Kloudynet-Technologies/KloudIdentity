using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.MapperCore;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.SCIM;
using Serilog;

namespace KN.KloudIdentity.Mapper.MapperCore
{
    /// <summary>
    /// Generic SOAP integration method for SCIM Connector Service.
    /// </summary>
    public class SOAPIntegration : IIntegrationBase
    {
        private readonly IAuthContext _authContext;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly IKloudIdentityLogger _logger;
        private readonly AppSettings _appSettings;
        public IntegrationMethods IntegrationMethod { get; init; }

        public SOAPIntegration(
            IAuthContext authContext,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IOptions<AppSettings> appSettings,
            IKloudIdentityLogger logger)
        {
            IntegrationMethod = IntegrationMethods.SOAP;
            _authContext = authContext;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
            _appSettings = appSettings.Value;
        }

        public virtual async Task<dynamic> GetAuthenticationAsync(AppConfig config, SCIMDirections direction = SCIMDirections.Outbound, CancellationToken cancellationToken = default, params dynamic[] args)
        {
            // Reuse REST logic for token retrieval if needed
            return await _authContext.GetTokenAsync(config, direction);
        }

        // Overload that matches the interface (does not require AppConfig)
        public virtual async Task<dynamic> MapAndPreparePayloadAsync(IList<AttributeSchema> schema, Core2EnterpriseUser resource, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("AppConfig is required for SOAP payload mapping. Use the overload that accepts AppConfig.");
        }

        // SOAP-specific overload that accepts AppConfig
        public virtual async Task<dynamic> MapAndPreparePayloadAsync(IList<AttributeSchema> schema, Core2EnterpriseUser resource, AppConfig appConfig, CancellationToken cancellationToken = default)
        {
            string xmlTemplate = GetSoapTemplate("", SOAPActions.Create); // You may want to determine the template based on appConfig and action
            string payload = SOAPParserUtil<Core2EnterpriseUser>.BuildPayload(xmlTemplate, schema, resource);

            return await Task.FromResult(payload);
        }

        public virtual async Task<Core2EnterpriseUser?> ProvisionAsync(dynamic payload, AppConfig appConfig, string correlationId, CancellationToken cancellationToken = default)
        {
            Log.Information("SOAP Provisioning started. AppId: {AppId}, CorrelationID: {CorrelationID}", appConfig.AppId, correlationId);

            var userUri = appConfig.UserURIs?.FirstOrDefault()?.Post ?? throw new InvalidOperationException("User creation endpoint not configured.");
            string responseBody = await SendSoapRequestAsync(userUri, payload, appConfig, SCIMDirections.Outbound, correlationId, cancellationToken);
            string idVal = ExtractIdentifierFromSoapResponse(responseBody, appConfig);

            return new Core2EnterpriseUser() { Identifier = idVal };
        }

        public virtual Task<(bool, string[])> ValidatePayloadAsync(dynamic payload, AppConfig appConfig, string correlationId, CancellationToken cancellationToken = default)
        {
            // Optionally validate XML payload (schema, required fields, etc.)
            return Task.FromResult((true, Array.Empty<string>()));
        }

        public virtual async Task<Core2EnterpriseUser> GetAsync(string identifier, AppConfig appConfig, string correlationId, CancellationToken cancellationToken = default)
        {
            var userUri = appConfig.UserURIs?.FirstOrDefault()?.Get ?? throw new InvalidOperationException("GET API not configured.");
            string xmlTemplate = GetSoapTemplate(appConfig.AppId, SOAPActions.Get);
            string responseBody = await SendSoapRequestAsync(userUri, xmlTemplate, appConfig, SCIMDirections.Outbound, correlationId, cancellationToken);

            return ParseSoapUserResponse(responseBody);
        }

        public virtual async Task ReplaceAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig, string correlationId)
        {
            var userUri = appConfig.UserURIs?.FirstOrDefault()?.Put ?? throw new InvalidOperationException("Replace endpoint not configured.");
            await SendSoapRequestAsync(userUri, payload, appConfig, SCIMDirections.Outbound, correlationId);
        }

        public virtual async Task UpdateAsync(dynamic payload, Core2EnterpriseUser resource, AppConfig appConfig, string correlationId)
        {
            var userUri = appConfig.UserURIs?.FirstOrDefault()?.Patch ?? appConfig.UserURIs?.FirstOrDefault()?.Put ?? throw new InvalidOperationException("Update endpoint not configured.");
            await SendSoapRequestAsync(userUri, payload, appConfig, SCIMDirections.Outbound, correlationId);
        }

        public virtual async Task DeleteAsync(string identifier, AppConfig appConfig, string correlationId)
        {
            var userUri = appConfig.UserURIs?.FirstOrDefault()?.Delete ?? throw new InvalidOperationException("Delete endpoint not configured.");
            string xmlTemplate = GetSoapTemplate(appConfig.AppId, SOAPActions.Delete);
            await SendSoapRequestAsync(userUri, xmlTemplate, appConfig, SCIMDirections.Outbound, correlationId);
        }
        /// <summary>
        /// Sends a SOAP request to the specified URI with the given payload and handles common response logic.
        /// </summary>
        /// <param name="uri">The endpoint URI.</param>
        /// <param name="payload">The SOAP XML payload (string).</param>
        /// <param name="appConfig">App configuration.</param>
        /// <param name="direction">SCIM direction.</param>
        /// <param name="correlationId">Correlation ID for logging.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Response body as string.</returns>
        private async Task<string> SendSoapRequestAsync(Uri uri, string payload, AppConfig appConfig, SCIMDirections direction, string correlationId, CancellationToken cancellationToken = default)
        {
            var httpClient = await CreateHttpClientAsync(appConfig, direction, cancellationToken);
            var content = new StringContent(payload, Encoding.UTF8, "text/xml");
            var response = await httpClient.PostAsync(uri, content, cancellationToken);
            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            // Check for HTTP error
            if (!response.IsSuccessStatusCode)
            {
                Log.Error("SOAP request failed. AppId: {AppId}, CorrelationID: {CorrelationID}, StatusCode: {StatusCode}, Response: {ResponseBody}", appConfig.AppId, correlationId, response.StatusCode, responseBody);
                throw new HttpRequestException($"SOAP request failed: {response.StatusCode} - {responseBody}");
            }

            // Check for <soap:Fault> in the response body even if HTTP 200
            if (!string.IsNullOrEmpty(responseBody) && responseBody.Contains("<soap:Fault", StringComparison.OrdinalIgnoreCase))
            {
                Log.Error("SOAP Fault detected. AppId: {AppId}, CorrelationID: {CorrelationID}, Response: {ResponseBody}", appConfig.AppId, correlationId, responseBody);
                throw new HttpRequestException($"SOAP Fault detected in response: {responseBody}");
            }

            return responseBody;
        }

        private async Task<HttpClient> CreateHttpClientAsync(AppConfig appConfig, SCIMDirections direction, CancellationToken cancellationToken = default)
        {
            var client = _httpClientFactory.CreateClient();
            // Set headers, authentication, etc. as needed
            // Example: client.DefaultRequestHeaders.Add("SOAPAction", ...);
            // Add token if required
            var token = await GetAuthenticationAsync(appConfig, direction, cancellationToken);
            if (token is string t && !string.IsNullOrEmpty(t))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t);
            }

            return client;
        }

        private string GetSoapTemplate(string appId, SOAPActions action)
        {
            // Retrieve the appropriate SOAP template based on the action (create, update, etc.)
            // This could be from appConfig.SOAPTemplates or determined by the schema
            throw new NotImplementedException();
        }

        private string ExtractIdentifierFromSoapResponse(string responseBody, AppConfig appConfig)
        {
            // Parse SOAP response XML to extract identifier
            throw new NotImplementedException();
        }

        private Core2EnterpriseUser ParseSoapUserResponse(string responseBody)
        {
            // Parse SOAP response XML to Core2EnterpriseUser
            throw new NotImplementedException();
        }
    }
}
