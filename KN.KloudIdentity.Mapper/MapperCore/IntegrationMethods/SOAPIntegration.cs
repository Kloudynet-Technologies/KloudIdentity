using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Common.Encryption;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
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
    public class SOAPIntegration : IIntegrationBaseV2
    {
        private static readonly JsonSerializerOptions CaseInsensitiveJsonOptions = new() { PropertyNameCaseInsensitive = true };

        // Attribute mappings arrive with URN-qualified destination fields (e.g., urn:kn:ki:schema:Identifier).
        protected const string UrnPrefix = "urn:kn:ki:schema:";

        // Matches a SOAP Fault element regardless of namespace prefix (<soap:Fault>, <soapenv:Fault>, <Fault>).
        // The trailing [\s>/] prevents false positives such as <faultstring> or <faultcode>.
        private static readonly Regex SoapFaultPattern = new(@"<(\w+:)?Fault[\s>/]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly IAuthContext _authContext;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly IKloudIdentityLogger _logger;
        private readonly AppSettings _appSettings;
        private readonly IReadOnlyList<ISoapAuthApplier> _soapAuthAppliers;
        private readonly ISecretManager? _secretManager;
        public IntegrationMethods IntegrationMethod { get; init; }

        public SOAPIntegration(
            IAuthContext authContext,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IOptions<AppSettings> appSettings,
            IKloudIdentityLogger logger,
            IEnumerable<ISoapAuthApplier>? soapAuthAppliers = null,
            ISecretManager? secretManager = null)
        {
            IntegrationMethod = IntegrationMethods.SOAP;
            _authContext = authContext;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
            _appSettings = appSettings.Value;
            _secretManager = secretManager;
            _soapAuthAppliers = [.. (soapAuthAppliers ??
            [
                new SoapTransportAuthApplier(),
                new WsSecuritySoapAuthApplier(),
                new SoapTokenHeaderApplier()
            ])];
        }

        public virtual async Task<dynamic> GetAuthenticationAsync(AppConfig config, SCIMDirections direction = SCIMDirections.Outbound, CancellationToken cancellationToken = default, params dynamic[] args)
        {
            Log.Information($"Getting authentication token for direction: {direction} for app: {config.AppId}");

            return await _authContext.GetTokenListAsync(config, direction);
        }

        // Overload that matches the interface (does not require AppConfig)
        public virtual Task<dynamic> MapAndPreparePayloadAsync(IList<AttributeSchema> schema, Core2EnterpriseUser resource, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("AppConfig is required for SOAP payload mapping. Use the overload that accepts AppConfig.");
        }

        /// <summary>
        /// Not supported for SOAP. Use the overload that accepts an ActionStep.
        /// </summary>
        public virtual Task<dynamic> MapAndPreparePayloadAsync(IList<AttributeSchema> schema, Core2EnterpriseUser resource, AppConfig appConfig, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("SOAP payload mapping requires an ActionStep. Use the overload that accepts ActionStep.");
        }

        /// <summary>
        /// Maps user attributes to a SOAP XML payload using the template stored on the ActionStep.
        /// </summary>
        public virtual async Task<dynamic> MapAndPreparePayloadAsync(IList<AttributeSchema> schema, Core2EnterpriseUser resource, AppConfig appConfig, ActionStep actionStep, CancellationToken cancellationToken = default)
        {
            var template = actionStep.Template
                ?? throw new InvalidOperationException($"ActionStep {actionStep.StepOrder} has no template. AppId: {appConfig.AppId}");

            string payload = SOAPParserUtil<Core2EnterpriseUser>.BuildPayload(template, schema, resource);
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

        public virtual Task<Core2EnterpriseUser> GetAsync(string identifier, AppConfig appConfig, string correlationId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Use the ActionStep overload for SOAP GET operations.");
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

        #region IIntegrationBaseV2 — action-step-aware methods

        public virtual async Task<Core2EnterpriseUser?> ProvisionAsync(dynamic payload, string appId, AppConfig appConfig, ActionStep actionStep, string correlationId, CancellationToken cancellationToken = default)
        {
            ValidateActionStep(actionStep, "PROVISION");

            Log.Information("SOAP Provisioning (V2) started. AppId: {AppId}, Step: {Step}, CorrelationID: {CorrelationID}",
                appConfig.AppId, actionStep.StepOrder, correlationId);

            var endpointUri = new Uri(actionStep.EndPoint);
            string responseBody = await SendSoapRequestAsync(endpointUri, payload, appConfig, SCIMDirections.Outbound, correlationId, cancellationToken);
            string idVal = ExtractIdentifierFromSoapResponse(responseBody, appConfig);

            return new Core2EnterpriseUser { Identifier = idVal };
        }

        public virtual async Task<Core2EnterpriseUser> GetAsync(string identifier, AppConfig appConfig, ActionStep actionStep, string correlationId, CancellationToken cancellationToken = default)
        {
            ValidateActionStep(actionStep, "GET");

            var template = actionStep.Template
                ?? throw new InvalidOperationException($"ActionStep {actionStep.StepOrder} has no template for GET. AppId: {appConfig.AppId}");

            var attributes = actionStep.UserAttributeSchemas?.ToList()
                ?? throw new InvalidOperationException($"No attributes configured on ActionStep {actionStep.StepOrder} for GET. AppId: {appConfig.AppId}");

            if (attributes.Count == 0)
                throw new InvalidOperationException($"ActionStep {actionStep.StepOrder} has no attributes for GET. AppId: {appConfig.AppId}");

            if (!attributes.Any(a => a.DestinationField.Replace(UrnPrefix, string.Empty) == "Identifier"))
                throw new InvalidOperationException($"ActionStep {actionStep.StepOrder} is missing an Identifier attribute mapping for GET. AppId: {appConfig.AppId}");

            var resource = new Core2EnterpriseUser { Identifier = identifier };
            var soapPayload = SOAPParserUtil<Core2EnterpriseUser>.BuildPayload(template, attributes, resource);

            var endpointUri = new Uri(actionStep.EndPoint);
            string responseBody = await SendSoapRequestAsync(endpointUri, soapPayload, appConfig, SCIMDirections.Outbound, correlationId, cancellationToken);

            return ParseSoapUserResponse(responseBody);
        }

        public virtual async Task<Core2EnterpriseUser> ReplaceAsync(dynamic payload, Core2EnterpriseUser resource, string appId, AppConfig appConfig, ActionStep actionStep, string correlationId, CancellationToken cancellationToken = default)
        {
            ValidateActionStep(actionStep, "REPLACE");

            var endpointUri = new Uri(actionStep.EndPoint);
            string responseBody = await SendSoapRequestAsync(endpointUri, payload, appConfig, SCIMDirections.Outbound, correlationId, cancellationToken);

            var parsedUser = ParseSoapUserResponse(responseBody);
            if (!string.IsNullOrEmpty(parsedUser.Identifier))
            {
                resource.Identifier = parsedUser.Identifier;
            }

            return resource;
        }

        public virtual async Task UpdateAsync(dynamic payload, Core2EnterpriseUser resource, string appId, AppConfig appConfig, ActionStep actionStep, string correlationId, CancellationToken cancellationToken = default)
        {
            ValidateActionStep(actionStep, "UPDATE");

            var endpointUri = new Uri(actionStep.EndPoint);
            await SendSoapRequestAsync(endpointUri, payload, appConfig, SCIMDirections.Outbound, correlationId, cancellationToken);
        }

        public virtual async Task DeleteAsync(string identifier, string appId, AppConfig appConfig, ActionStep actionStep, string correlationId, CancellationToken cancellationToken = default)
        {
            ValidateActionStep(actionStep, "DELETE");

            var template = actionStep.Template
                ?? throw new InvalidOperationException($"ActionStep {actionStep.StepOrder} has no template for DELETE. AppId: {appConfig.AppId}");

            var attributes = actionStep.UserAttributeSchemas?.ToList()
                ?? throw new InvalidOperationException($"No attributes configured on ActionStep {actionStep.StepOrder} for DELETE. AppId: {appConfig.AppId}");

            if (attributes.Count == 0)
                throw new InvalidOperationException($"ActionStep {actionStep.StepOrder} has no attributes for DELETE. AppId: {appConfig.AppId}");

            if (!attributes.Any(a => a.DestinationField.Replace(UrnPrefix, string.Empty) == "Identifier"))
                throw new InvalidOperationException($"ActionStep {actionStep.StepOrder} is missing an Identifier attribute mapping for DELETE. AppId: {appConfig.AppId}");

            var resource = new Core2EnterpriseUser { Identifier = identifier };
            var soapPayload = SOAPParserUtil<Core2EnterpriseUser>.BuildPayload(template, attributes, resource);

            var endpointUri = new Uri(actionStep.EndPoint);
            await SendSoapRequestAsync(endpointUri, soapPayload, appConfig, SCIMDirections.Outbound, correlationId, cancellationToken);
        }

        /// <summary>
        /// Validates that actionStep and its endpoint are provided.
        /// </summary>
        protected static void ValidateActionStep(ActionStep actionStep, string operationName)
        {
            ArgumentNullException.ThrowIfNull(actionStep, nameof(actionStep));

            if (string.IsNullOrWhiteSpace(actionStep.EndPoint))
            {
                throw new InvalidOperationException(
                    $"ActionStep endpoint must be provided for SOAP {operationName} operation (StepOrder: {actionStep.StepOrder}).");
            }
        }

        #endregion

        public virtual Task DeleteAsync(string identifier, AppConfig appConfig, string correlationId)
        {
            throw new NotSupportedException("Use the ActionStep overload for SOAP DELETE operations.");
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
        protected virtual async Task<string> SendSoapRequestAsync(Uri uri, string payload, AppConfig appConfig, SCIMDirections direction, string correlationId, CancellationToken cancellationToken = default)
        {
            var soapAuthOptions = await ResolveSoapSecretsAsync(ResolveSoapAuthenticationOptions(appConfig));
            var (httpClient, handler, token) = await CreateHttpClientAsync(appConfig, direction, soapAuthOptions, cancellationToken);
            // Only a client built around its own NTLM handler is ours to dispose; factory clients are not.
            using var ownedClient = handler != null ? httpClient : null;

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
                Payload = WrapInSoapEnvelope(payload)
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

            // Check for a SOAP Fault in the response body even if HTTP 200 (any namespace prefix — Eagle uses soapenv:)
            if (!string.IsNullOrEmpty(responseBody) && SoapFaultPattern.IsMatch(responseBody))
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
                var tokens = await GetAuthenticationAsync(appConfig, direction, cancellationToken) as Dictionary<int, string>;
                if (tokens != null && !isNtlmEnabled)
                {
                    var steps = appConfig.AuthenticationFlow?.Steps;
                    foreach (var authToken in tokens)
                    {
                        var step = steps?.FirstOrDefault(s => s.StepOrder == authToken.Key);
                        var authMethod = step?.AuthenticationMethod
                            ?? (direction == SCIMDirections.Inbound
                                ? appConfig.AuthenticationMethodInbound
                                : appConfig.AuthenticationMethodOutbound);
                        var authDetails = step != null && HasAuthenticationDetails((object?)step.AuthenticationDetails)
                            ? NormalizeAuthenticationDetails(step.AuthenticationDetails)
                            : NormalizeAuthenticationDetails(appConfig.AuthenticationDetails);

                        if (authMethod != AuthenticationMethods.None)
                        {
                            Utils.HttpClientExtensions.SetAuthenticationHeaders(
                                client, 
                                authMethod, 
                                authDetails, 
                                authToken.Value
                            );
                        }
                    }
                }
                token = tokens?.Values.FirstOrDefault();
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
            // NTLM credentials are per application, so they cannot go on the pooled handler the factory
            // shares between apps — and SoapTransportAuthApplier needs a handler to put them on (it throws
            // without one). A dedicated handler per request; SendSoapRequestAsync disposes the client.
            handler = new HttpClientHandler();
            return new HttpClient(handler, disposeHandler: true);
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

        /// <summary>
        /// Builds the SOAP auth options from the authentication flow. The Management Portal stores SOAP auth
        /// as three separate step methods (SoapNtlm/SoapTransport = 9, SoapWsSecurity = 8,
        /// SoapTokenPlacement = 10), each publishing its own FLAT details, so the options are aggregated
        /// across every step rather than read from one. The first step (by StepOrder) to supply a section wins.
        /// Details already in the nested SOAPAuthenticationOptions shape are still accepted, on any step.
        /// </summary>
        private static SOAPAuthenticationOptions? ResolveSoapAuthenticationOptions(AppConfig appConfig)
        {
            var steps = appConfig.AuthenticationFlow?.Steps;
            if (steps == null)
            {
                return null;
            }

            BasicOrNtlmSoapAuthOptions? transport = null;
            WsSecuritySoapAuthOptions? wsSecurity = null;
            SoapTokenPlacementOptions? tokenPlacement = null;

            foreach (var step in steps.OrderBy(s => s.StepOrder))
            {
                if (!HasAuthenticationDetails((object?)step.AuthenticationDetails)) continue;
                if (!TryGetDetailsElement((object?)step.AuthenticationDetails, out var root)) continue;

                if (TryExtractSoapAuthFromDetails(root, out SOAPAuthenticationOptions? nested) && nested != null)
                {
                    transport ??= nested.Transport;
                    wsSecurity ??= nested.WsSecurity;
                    tokenPlacement ??= nested.TokenPlacement;
                    continue;
                }

                try
                {
                    switch (step.AuthenticationMethod)
                    {
                        case AuthenticationMethods.SoapNtlm:
                            transport ??= root.Deserialize<BasicOrNtlmSoapAuthOptions>(CaseInsensitiveJsonOptions);
                            break;
                        case AuthenticationMethods.SoapWsSecurity:
                            wsSecurity ??= root.Deserialize<WsSecuritySoapAuthOptions>(CaseInsensitiveJsonOptions);
                            break;
                        case AuthenticationMethods.SoapTokenPlacement:
                            tokenPlacement ??= ReadFlatTokenPlacement(root, step.StepOrder);
                            break;
                    }
                }
                catch (JsonException ex)
                {
                    Log.Error(ex, "Failed to read {Method} details from authentication step {StepOrder}.",
                        step.AuthenticationMethod, step.StepOrder);
                }
            }

            if (transport == null && wsSecurity == null && tokenPlacement == null)
            {
                return null;
            }

            return new SOAPAuthenticationOptions
            {
                Transport = transport,
                WsSecurity = wsSecurity,
                TokenPlacement = tokenPlacement
            };
        }

        private static SoapTokenPlacementOptions? ReadFlatTokenPlacement(JsonElement root, int stepOrder)
        {
            var flat = root.Deserialize<FlatSoapTokenPlacement>(CaseInsensitiveJsonOptions);
            if (flat == null)
            {
                return null;
            }

            return new SoapTokenPlacementOptions
            {
                Enabled = flat.Enabled,
                UseAuthorizationHeader = flat.UseAuthorizationHeader,
                SoapHeaderTemplate = flat.SoapHeaderTemplate,
                CustomHttpHeaders = ParseCustomHttpHeaders(flat.CustomHttpHeaders, stepOrder),
                TokenPlaceholder = string.IsNullOrWhiteSpace(flat.TokenPlaceholder) ? "{{token}}" : flat.TokenPlaceholder
            };
        }

        // The portal stores CustomHttpHeaders as a JSON STRING ("{\"X-Api\":\"{{token}}\"}"), not an object.
        // An object is accepted too. Anything unparseable is logged and ignored rather than failing the request.
        private static Dictionary<string, string>? ParseCustomHttpHeaders(JsonElement value, int stepOrder)
        {
            try
            {
                switch (value.ValueKind)
                {
                    case JsonValueKind.String:
                        var text = value.GetString();
                        return string.IsNullOrWhiteSpace(text)
                            ? null
                            : JsonSerializer.Deserialize<Dictionary<string, string>>(text);
                    case JsonValueKind.Object:
                        return value.Deserialize<Dictionary<string, string>>();
                    case JsonValueKind.Null:
                    case JsonValueKind.Undefined:
                        return null;
                }
            }
            catch (JsonException ex)
            {
                Log.Warning(ex, "Ignoring unparseable CustomHttpHeaders on SOAP token placement step {StepOrder}.", stepOrder);
                return null;
            }

            Log.Warning("Ignoring CustomHttpHeaders of kind {Kind} on SOAP token placement step {StepOrder}.", value.ValueKind, stepOrder);
            return null;
        }

        // The flat shape the portal publishes for a SoapTokenPlacement step (GetSoapTokenPlacementAuthResult).
        private sealed record FlatSoapTokenPlacement
        {
            public bool Enabled { get; init; }
            public bool UseAuthorizationHeader { get; init; }
            public string? TokenPlaceholder { get; init; }
            public string? SoapHeaderTemplate { get; init; }
            public JsonElement CustomHttpHeaders { get; init; }
        }

        /// <summary>
        /// The portal publishes SOAP passwords as the literal "*": the ciphertext lives in Key Vault under
        /// KeyVaultReference and its IV in EncryptedData, exactly as for Basic auth (see BasicAuthStrategy).
        /// Resolves a password only where an applier will actually use it.
        /// </summary>
        private async Task<SOAPAuthenticationOptions?> ResolveSoapSecretsAsync(SOAPAuthenticationOptions? options)
        {
            if (options == null)
            {
                return null;
            }

            var transport = options.Transport;
            if (transport is { Enabled: true, UseNtlm: true, UseDefaultCredentials: false } && IsPasswordPlaceholder(transport.Password))
            {
                transport = transport with
                {
                    Password = await ResolveSecretAsync("SOAP transport", transport.KeyVaultReference, transport.EncryptedData)
                };
            }

            var wsSecurity = options.WsSecurity;
            if (wsSecurity is { Enabled: true } && IsPasswordPlaceholder(wsSecurity.Password))
            {
                wsSecurity = wsSecurity with
                {
                    Password = await ResolveSecretAsync("SOAP WS-Security", wsSecurity.KeyVaultReference, wsSecurity.EncryptedData)
                };
            }

            return options with { Transport = transport, WsSecurity = wsSecurity };
        }

        private static bool IsPasswordPlaceholder(string? password) =>
            string.IsNullOrWhiteSpace(password) || password == "*";

        private async Task<string?> ResolveSecretAsync(string section, string? keyVaultReference, EncryptedData? encryptedData)
        {
            // Nothing stored: leave the password empty so the applier reports the missing credential.
            if (string.IsNullOrWhiteSpace(keyVaultReference))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(encryptedData?.IV))
            {
                throw new InvalidOperationException(
                    $"{section} password is stored in Key Vault ('{keyVaultReference}') but no EncryptedData.IV was supplied to decrypt it.");
            }

            if (_secretManager == null)
            {
                throw new InvalidOperationException($"{section} password is stored in Key Vault, but no secret manager is configured.");
            }

            if (string.IsNullOrWhiteSpace(_appSettings.EncryptionKey))
            {
                throw new InvalidOperationException("Encryption key is not configured in EncryptionKey.");
            }

            var encryptedPassword = await _secretManager.GetSecretAsync(keyVaultReference);
            return EncryptionHelper.Decrypt(encryptedPassword, _appSettings.EncryptionKey, encryptedData.IV);
        }

        // AuthenticationDetails arrives as a JsonElement (snapshot, System.Text.Json), a JToken (Newtonsoft),
        // a JSON string, or a plain object (tests). Normalise all of them to a JSON object element.
        private static bool TryGetDetailsElement(object? details, out JsonElement element)
        {
            element = default;
            try
            {
                var json = details switch
                {
                    JsonElement jsonElement => jsonElement.GetRawText(),
                    string text => text,
                    Newtonsoft.Json.Linq.JToken token => token.ToString(Newtonsoft.Json.Formatting.None),
                    _ => JsonSerializer.Serialize(details)
                };

                using var document = JsonDocument.Parse(json);
                element = document.RootElement.Clone();
                return element.ValueKind == JsonValueKind.Object;
            }
            catch (JsonException ex)
            {
                Log.Error(ex, "Failed to read AuthenticationDetails as JSON.");
                return false;
            }
        }

        private static bool TryReadSoapAuthOptions(JsonElement root, string propertyName, out SOAPAuthenticationOptions? options)
        {
            options = null;
            if (!root.TryGetProperty(propertyName, out var value))
            {
                return false;
            }

            options = value.Deserialize<SOAPAuthenticationOptions>(CaseInsensitiveJsonOptions);

            return options != null;
        }

        private static bool TryDeserializeSoapAuthDirectly(JsonElement root, out SOAPAuthenticationOptions? options)
        {
            options = root.Deserialize<SOAPAuthenticationOptions>(CaseInsensitiveJsonOptions);

            if (options == null)
                return false;

            // Only accept if at least one section is populated — prevents false positives
            // when unrelated auth details happen to deserialize without error.
            return options.Transport != null
                || options.WsSecurity != null
                || options.TokenPlacement != null;
        }

        private static bool TryExtractSoapAuthFromDetails(JsonElement root, out SOAPAuthenticationOptions? options)
        {
            options = null;
            try
            {
                if (TryDeserializeSoapAuthDirectly(root, out options))
                    return true;

                if (TryReadSoapAuthOptions(root, "SOAPAuthenticationOptions", out options)
                    || TryReadSoapAuthOptions(root, "SoapAuthenticationOptions", out options)
                    || TryReadSoapAuthOptions(root, "soapAuthenticationOptions", out options)
                    || TryReadSoapAuthOptions(root, "soapAuthOptions", out options))
                    return true;
            }
            catch (JsonException ex)
            {
                Log.Error(ex, "Failed to extract SOAPAuthenticationOptions from AuthenticationDetails.");
            }
            catch (InvalidOperationException ex)
            {
                Log.Error(ex, "Failed to extract SOAPAuthenticationOptions from AuthenticationDetails.");
            }

            return false;
        }

        private static string WrapInSoapEnvelope(string payload)
        {
            var trimmed = payload.TrimStart();
            if (trimmed.StartsWith("<soap:Envelope", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("<Envelope", StringComparison.OrdinalIgnoreCase))
                return payload;

            return $"<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                   $"<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\">" +
                   $"<soap:Header/>" +
                   $"<soap:Body>{payload}</soap:Body>" +
                   $"</soap:Envelope>";
        }

        // AuthenticationDetails is dynamic and often holds a JsonElement (a struct) after JSON
        // deserialization — `details != null` on it throws RuntimeBinderException. Box to object
        // first, and treat a JSON null/undefined element as absent.
        protected static bool HasAuthenticationDetails(object? details) =>
            details is JsonElement element
                ? element.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
                : details is not null;

        protected static dynamic NormalizeAuthenticationDetails(dynamic authenticationDetails)
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
