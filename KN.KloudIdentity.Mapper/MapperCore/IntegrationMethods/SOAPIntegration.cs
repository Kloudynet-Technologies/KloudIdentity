using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Authentication;
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
        private readonly IReadOnlyList<ISoapAuthApplier> _soapAuthAppliers;
        public IntegrationMethods IntegrationMethod { get; init; }

        public SOAPIntegration(
            IAuthContext authContext,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IOptions<AppSettings> appSettings,
            IKloudIdentityLogger logger,
            IEnumerable<ISoapAuthApplier>? soapAuthAppliers = null)
        {
            IntegrationMethod = IntegrationMethods.SOAP;
            _authContext = authContext;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
            _appSettings = appSettings.Value;
            _soapAuthAppliers = [.. (soapAuthAppliers ??
            [
                new SoapTransportAuthApplier(),
                new WsSecuritySoapAuthApplier(),
                new SoapTokenHeaderApplier()
            ])];
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
            var soapAuthOptions = ResolveSoapAuthenticationOptions(appConfig);
            var (httpClient, handler, token) = await CreateHttpClientAsync(appConfig, direction, soapAuthOptions, cancellationToken);

            var request = new HttpRequestMessage(HttpMethod.Post, uri);
            var authContext = new SoapAuthContext
            {
                AppConfig = appConfig,
                Direction = direction,
                HttpClient = httpClient,
                Request = request,
                Handler = handler,
                Token = token,
                AuthOptions = soapAuthOptions,
                Payload = payload
            };

            foreach (var applier in _soapAuthAppliers)
            {
                await applier.ApplyAsync(authContext, cancellationToken);
            }

            request.Content = new StringContent(authContext.Payload, Encoding.UTF8, "text/xml");
            var response = await httpClient.SendAsync(request, cancellationToken);
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

        private async Task<(HttpClient client, HttpClientHandler? handler, string? token)> CreateHttpClientAsync(AppConfig appConfig, SCIMDirections direction, SOAPAuthenticationOptions? soapAuthOptions, CancellationToken cancellationToken = default)
        {
            var isNtlmEnabled = soapAuthOptions?.Transport?.Enabled == true && soapAuthOptions.Transport.UseNtlm;
            HttpClientHandler? handler = null;
            var client = isNtlmEnabled
                ? CreateHttpClientForNtlm(out handler)
                : _httpClientFactory.CreateClient();

            string? token = null;
            if (ShouldResolveToken(appConfig, soapAuthOptions, direction))
            {
                token = await GetAuthenticationAsync(appConfig, direction, cancellationToken);
            }

            if (!isNtlmEnabled && !string.IsNullOrWhiteSpace(token) && appConfig.AuthenticationMethodOutbound != AuthenticationMethods.None)
            {
                Utils.HttpClientExtensions.SetAuthenticationHeaders(
                   client,
                   appConfig.AuthenticationMethodOutbound,
                   NormalizeAuthenticationDetails(appConfig.AuthenticationDetails),
                   token);
            }

            var customHttpClient = _appSettings.AppIntegrationConfigs?.FirstOrDefault(x => x.AppId == appConfig.AppId);
            if (customHttpClient?.HttpSettings?.Headers is { Count: > 0 })
            {
                client.SetCustomHeaders(customHttpClient.HttpSettings.Headers);
            }

            return (client, handler, token);
        }

        protected virtual HttpClient CreateHttpClientForNtlm(out HttpClientHandler handler)
        {
            // Handler is configured by DI for this named client, so no manual `new HttpClient(...)`.
            handler = null!;
            return _httpClientFactory.CreateClient(AppConstant.NtlmSoapClientName);
        }

        private static bool ShouldResolveToken(AppConfig appConfig, SOAPAuthenticationOptions? options, SCIMDirections direction)
        {
            var authMethod = direction == SCIMDirections.Inbound
                ? appConfig.AuthenticationMethodInbound
                : appConfig.AuthenticationMethodOutbound;

            if (authMethod != AuthenticationMethods.None)
            {
                return true;
            }

            var tokenPlacement = options?.TokenPlacement;
            if (tokenPlacement?.Enabled != true)
            {
                return false;
            }

            var hasEffectiveTokenPlacement =
                tokenPlacement.UseAuthorizationHeader
                || (tokenPlacement.CustomHttpHeaders != null && tokenPlacement.CustomHttpHeaders.Count > 0)
                || !string.IsNullOrWhiteSpace(tokenPlacement.SoapHeaderTemplate);

            if (hasEffectiveTokenPlacement)
            {
                throw new InvalidOperationException(
                    "Token placement is enabled for a SOAP integration, but the authentication method is set to 'None'. " +
                    "Configure a token-producing authentication method for the specified direction (inbound or outbound) " +
                    "when using token placement.");
            }

            return false;
        }

        private static SOAPAuthenticationOptions? ResolveSoapAuthenticationOptions(AppConfig appConfig)
        {
            if (appConfig.SOAPAuthenticationOptions != null)
            {
                return appConfig.SOAPAuthenticationOptions;
            }

            if (appConfig.AuthenticationDetails == null)
            {
                return null;
            }

            try
            {
                var authDetailsJson = JsonSerializer.Serialize(appConfig.AuthenticationDetails);
                using var document = JsonDocument.Parse(authDetailsJson);
                var root = document.RootElement;
                SOAPAuthenticationOptions? options;

                if (TryReadSoapAuthOptions(root, "SOAPAuthenticationOptions", out options)
                    || TryReadSoapAuthOptions(root, "SoapAuthenticationOptions", out options)
                    || TryReadSoapAuthOptions(root, "soapAuthenticationOptions", out options)
                    || TryReadSoapAuthOptions(root, "soapAuthOptions", out options))
                {
                    return options;
                }
            }
            catch (JsonException ex)
            {
                Log.Error(ex, "Failed to parse SOAP authentication options from AuthenticationDetails. AppId: {AppId}", appConfig.AppId);
                return null;
            }
            catch (InvalidOperationException ex)
            {
                Log.Error(ex, "Failed to parse SOAP authentication options from AuthenticationDetails. AppId: {AppId}", appConfig.AppId);
                return null;
            }
            catch
            {
                Log.Error("Failed to parse SOAP authentication options from AuthenticationDetails due to an unexpected error. AppId: {AppId}", appConfig.AppId);
                return null;
            }

            return null;
        }

        private static bool TryReadSoapAuthOptions(JsonElement root, string propertyName, out SOAPAuthenticationOptions? options)
        {
            options = null;
            if (!root.TryGetProperty(propertyName, out var value))
            {
                return false;
            }

            options = value.Deserialize<SOAPAuthenticationOptions>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return options != null;
        }

        private static dynamic NormalizeAuthenticationDetails(dynamic authenticationDetails)
        {
            if (authenticationDetails is null)
            {
                return "{}";
            }

            if (authenticationDetails is string authDetailsString)
            {
                return authDetailsString;
            }

            return JsonSerializer.Serialize(authenticationDetails);
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
