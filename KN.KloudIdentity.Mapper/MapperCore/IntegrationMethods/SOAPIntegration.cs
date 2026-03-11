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

        /// <summary>
        /// Maps user attributes to a SOAP XML payload based on the provided template in app configuration.
        /// </summary>
        /// <param name="schema">The list of attribute schemas to map.</param>
        /// <param name="resource">The user resource containing the attribute values.</param>
        /// <param name="appConfig">The application configuration containing the SOAP template. SOAPTemplates must only contain one template for the action.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The mapped SOAP XML payload.</returns>
        /// <exception cref="InvalidOperationException">Thrown if no SOAP template is configured.</exception>
        public virtual async Task<dynamic> MapAndPreparePayloadAsync(IList<AttributeSchema> schema, Core2EnterpriseUser resource, AppConfig appConfig, CancellationToken cancellationToken = default)
        {
            // It is assumed that the SOAP template passed is the correct one for the action.
            SOAPTemplate? template = appConfig.SOAPTemplates?.FirstOrDefault();
            if (template == null)
            {
                Log.Error("No SOAP template configured. AppId: {AppId}", appConfig.AppId);
                throw new InvalidOperationException("SOAP template is required for payload mapping.");
            }

            string payload = SOAPParserUtil<Core2EnterpriseUser>.BuildPayload(template.Template, schema, resource);

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
            SOAPTemplate? template = appConfig.SOAPTemplates?.FirstOrDefault(t => t.Action == SOAPActions.Get);
            if (template == null)
            {
                Log.Error("No SOAP template configured for GET action. AppId: {AppId}", appConfig.AppId);
                throw new InvalidOperationException("SOAP template is required for GET action.");
            }

            string xmlTemplate = template.Template;
            var attributes = appConfig.UserAttributeSchemas.Where(p => p.HttpRequestType == HttpRequestTypes.GET).ToList();
            if (!attributes.Any() || !attributes.Any(a => a.SourceValue.Equals("Identifier", StringComparison.OrdinalIgnoreCase)))
            {
                Log.Error("No valid attributes configured for GET action. AppId: {AppId}", appConfig.AppId);
                throw new InvalidOperationException("At least one attribute other than Identifier must be configured for GET action.");
            }

            Core2EnterpriseUser resource = new Core2EnterpriseUser { Identifier = identifier };

            var payload = SOAPParserUtil<Core2EnterpriseUser>.BuildPayload(xmlTemplate, attributes, resource);

            string responseBody = await SendSoapRequestAsync(userUri, payload, appConfig, SCIMDirections.Outbound, correlationId, cancellationToken);

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
            SOAPTemplate? template = appConfig.SOAPTemplates?.FirstOrDefault(t => t.Action == SOAPActions.Delete);
            if (template == null)
            {
                Log.Error("No SOAP template configured for DELETE action. AppId: {AppId}", appConfig.AppId);
                throw new InvalidOperationException("SOAP template is required for DELETE action.");
            }

            string xmlTemplate = template.Template;
            var attributes = appConfig.UserAttributeSchemas.Where(p => p.HttpRequestType == HttpRequestTypes.DELETE).ToList();
            if (!attributes.Any() || !attributes.Any(a => a.SourceValue.Equals("Identifier", StringComparison.OrdinalIgnoreCase)))
            {
                Log.Error("No valid attributes configured for DELETE action. AppId: {AppId}", appConfig.AppId);
                throw new InvalidOperationException("At least one attribute other than Identifier must be configured for DELETE action.");
            }

            Core2EnterpriseUser resource = new Core2EnterpriseUser { Identifier = identifier };

            var payload = SOAPParserUtil<Core2EnterpriseUser>.BuildPayload(xmlTemplate, attributes, resource);

            await SendSoapRequestAsync(userUri, payload, appConfig, SCIMDirections.Outbound, correlationId);
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

        public virtual string ExtractIdentifierFromSoapResponse(string responseBody, AppConfig appConfig)
        {
            var user = ParseSoapUserResponse(responseBody);
            if (string.IsNullOrEmpty(user.Identifier))
            {
                Log.Error("Identifier not found in SOAP response. AppId: {AppId}, Response: {ResponseBody}", appConfig.AppId, responseBody);
                throw new InvalidOperationException("Identifier not found in SOAP response.");
            }

            return user.Identifier;
        }

        public virtual Core2EnterpriseUser ParseSoapUserResponse(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                throw new ArgumentException("SOAP response body is empty.");
            }

            try
            {
                // Load the response into an XML document
                var xmlDoc = new System.Xml.XmlDocument
                {
                    XmlResolver = null // Prevent XXE by disabling external entity resolution
                };
                xmlDoc.LoadXml(responseBody);

                // Try to find the SOAP body node
                var nsmgr = new System.Xml.XmlNamespaceManager(xmlDoc.NameTable);
                nsmgr.AddNamespace("soap", "http://schemas.xmlsoap.org/soap/envelope/");
                var bodyNode = xmlDoc.SelectSingleNode("//soap:Body", nsmgr);

                if (bodyNode == null)
                {
                    throw new InvalidOperationException("SOAP Body not found in response.");
                }

                // Find the first element inside the body (the actual response payload)
                var userNode = bodyNode.FirstChild;
                if (userNode == null)
                {
                    throw new InvalidOperationException("No payload found in SOAP Body.");
                }

                // Map fields from the XML to Core2EnterpriseUser properties
                var user = new Core2EnterpriseUser();

                // Example: try to extract Identifier, UserName, DisplayName, etc.
                // Adjust element names as per your SOAP response schema
                var identifierNode = userNode.SelectSingleNode(".//*[local-name()='Identifier']");
                if (identifierNode != null)
                {
                    user.Identifier = identifierNode.InnerText;
                }

                var userNameNode = userNode.SelectSingleNode(".//*[local-name()='UserName']");
                if (userNameNode != null)
                {
                    user.UserName = userNameNode.InnerText;
                }

                var displayNameNode = userNode.SelectSingleNode(".//*[local-name()='DisplayName']");
                if (displayNameNode != null)
                {
                    user.DisplayName = displayNameNode.InnerText;
                }

                // Add more mappings as needed...

                return user;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to parse SOAP user response.");
                throw new InvalidOperationException("Failed to parse SOAP user response.", ex);
            }
        }
    }
}
