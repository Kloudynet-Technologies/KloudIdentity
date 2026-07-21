//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web.Http;
using System.Xml;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.SCIM;
using Serilog;

namespace KN.KloudIdentity.Mapper.MapperCore;

public class EagleSOAPIntegration : SOAPIntegration
{
    private const string DefaultOutputFormat = "json";
    private const string DefaultExtractStreamName = "eagle_ml-2-0_default_out_extract_service";
    private readonly IHttpClientFactory _httpClientFactory;

    public EagleSOAPIntegration(
        IAuthContext authContext,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IOptions<AppSettings> appSettings,
        IKloudIdentityLogger logger,
        IEnumerable<ISoapAuthApplier>? soapAuthAppliers = null)
        : base(authContext, httpClientFactory, configuration, appSettings, logger,
               [new EagleSoapActionApplier(), .. (soapAuthAppliers ?? DefaultAppliers())])
    {
        _httpClientFactory = httpClientFactory;
        IntegrationMethod = IntegrationMethods.SOAPEagle;
    }

    private static IEnumerable<ISoapAuthApplier> DefaultAppliers() =>
        [new SoapTransportAuthApplier(), new WsSecuritySoapAuthApplier(), new SoapTokenHeaderApplier()];

    //Eagle always authenticates with HTTP Basic; a configured flow that resolves no token would
    //otherwise send the SOAP request unauthenticated (envelope-level appliers no-op by design).
    public override async Task<dynamic> GetAuthenticationAsync(
        AppConfig config,
        SCIMDirections direction = SCIMDirections.Outbound,
        CancellationToken cancellationToken = default,
        params dynamic[] args)
    {
        var result = await base.GetAuthenticationAsync(config, direction, cancellationToken, args);

        if (config.AuthenticationFlow?.Steps?.Count > 0
            && (result is not Dictionary<int, string> tokens || tokens.Count == 0))
        {
            var message =
                $"Authentication flow for AppId {config.AppId} resolved no token. Eagle requires an HTTP Basic step " +
                "(AuthenticationMethod=1) with portal-saved credentials; verify the AuthenticationFlow configuration.";
            Log.Error("Eagle authentication resolved no token. AppId: {AppId}, Direction: {Direction}", config.AppId, direction);
            throw new InvalidOperationException(message);
        }

        return result;
    }

    // Bypasses the base-class SendSoapRequestAsync to fix three issues specific to Eagle:
    //  1. WrapInSoapEnvelope only guards <soap:Envelope / <Envelope — Eagle uses <soapenv:Envelope
    //     (different prefix), causing double-wrapping and a 100-second timeout.
    //  2. The payload string may contain JSON-escaped quotes (\" from DB storage); must be
    //     unescaped before sending so Eagle receives well-formed XML.
    //  3. Basic auth must always come from the flow step regardless of
    //     AppConfig.AuthenticationMethodOutbound — the base ShouldResolveToken guard is skipped.
    protected override async Task<string> SendSoapRequestAsync(
        Uri uri,
        string payload,
        AppConfig appConfig,
        SCIMDirections direction,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        // Fix 2: unescape JSON-escaped quotes before sending over the wire.
        var soapPayload = UnescapeXmlPayload(payload);

        // Fix 1: Eagle template is already a complete SOAP envelope — do NOT wrap it.
        // Fix 3: resolve Basic auth from flow steps (same as RESTIntegrationV4; no ShouldResolveToken guard).
        var client = _httpClientFactory.CreateClient();
        var tokens = await GetAuthenticationAsync(appConfig, direction, cancellationToken) as Dictionary<int, string>;
        if (tokens != null)
        {
            var steps = appConfig.AuthenticationFlow?.Steps;
            foreach (var authToken in tokens)
            {
                var step = steps?.FirstOrDefault(s => s.StepOrder == authToken.Key);
                if (step == null
                    || step.AuthenticationMethod == AuthenticationMethods.None
                    || string.IsNullOrWhiteSpace(authToken.Value))
                    continue;

                Utils.HttpClientExtensions.SetAuthenticationHeaders(
                    client,
                    step.AuthenticationMethod,
                    NormalizeAuthenticationDetails(step.AuthenticationDetails),
                    authToken.Value);
            }
        }

        var request = new HttpRequestMessage(HttpMethod.Post, uri);
        // Eagle requires this SOAPAction header on every request (same as EagleSoapActionApplier).
        request.Headers.TryAddWithoutValidation("SOAPAction", "\"RunTaskRequestSync\"");
        request.Content = new StringContent(soapPayload, Encoding.UTF8, "text/xml");

        var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            Log.Error(
                "Eagle SOAP request failed. AppId: {AppId}, CorrelationId: {CorrelationId}, StatusCode: {StatusCode}, Response: {ResponseBody}",
                appConfig.AppId, correlationId, response.StatusCode, responseBody);
            throw new HttpRequestException($"Eagle SOAP request failed: {response.StatusCode} - {responseBody}");
        }

        if (!string.IsNullOrEmpty(responseBody) && EagleSoapFaultPattern.IsMatch(responseBody))
        {
            Log.Error(
                "Eagle SOAP Fault detected. AppId: {AppId}, CorrelationId: {CorrelationId}, Response: {ResponseBody}",
                appConfig.AppId, correlationId, responseBody);
            throw new HttpRequestException($"Eagle SOAP Fault detected: {responseBody}");
        }

        return responseBody;
    }

    // Matches a SOAP Fault element regardless of namespace prefix (<soapenv:Fault>, <soap:Fault>, <Fault>).
    private static readonly Regex EagleSoapFaultPattern =
        new(@"<(\w+:)?Fault[\s>/]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public override async Task<dynamic> MapAndPreparePayloadAsync(
        IList<AttributeSchema> schema,
        Core2EnterpriseUser resource,
        AppConfig appConfig,
        ActionStep actionStep,
        CancellationToken cancellationToken = default)
    {
        var template = actionStep.Template
            ?? throw new InvalidOperationException($"ActionStep {actionStep.StepOrder} has no template. AppId: {appConfig.AppId}");

        var injected = template.Replace("{{CorrelationId}}", Guid.NewGuid().ToString(), StringComparison.Ordinal)
                               .Replace("{{accountState}}", resource.Active ? "U" : "D", StringComparison.Ordinal);
        string payload = SOAPParserUtil<Core2EnterpriseUser>.BuildPayload(injected, schema, resource);
        EnsureAllPlaceholdersResolved(payload, appConfig.AppId);
        return await Task.FromResult(payload);
    }

    public override async Task<Core2EnterpriseUser?> ProvisionAsync(
        dynamic payload,
        AppConfig appConfig,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Use the ActionStep overload for SOAPEagle operations.");
    }

    public override async Task<Core2EnterpriseUser?> ProvisionAsync(
        dynamic payload,
        string appId,
        AppConfig appConfig,
        ActionStep actionStep,
        string correlationId,
        CancellationToken cancellationToken = default)
    {

        // Validate ActionStep configuration for Provision action
        ValidateActionStep(actionStep, "PROVISION");

        var userId = ExtractUserIdFromPayload((string)payload);

        LogEagleRequest("PROVISION", appConfig.AppId, userId, correlationId, (string)payload);

        var responseBody = await SendSoapRequestAsync(new Uri(actionStep.EndPoint), payload, appConfig, SCIMDirections.Outbound, correlationId, cancellationToken);
        ValidateEagleResponse(responseBody, appConfig.AppId);
        
        LogEagleSuccess("PROVISION", appConfig.AppId, responseBody);

        return new Core2EnterpriseUser { Identifier = userId };
    }

    public override Task ReplaceAsync(
        dynamic payload,
        Core2EnterpriseUser resource,
        AppConfig appConfig,
        string correlationId)
    {
        throw new NotSupportedException("Use the ActionStep overload for SOAPEagle operations.");
    }

    public override async Task<Core2EnterpriseUser> ReplaceAsync(
        dynamic payload,
        Core2EnterpriseUser resource,
        string appId,
        AppConfig appConfig,
        ActionStep actionStep,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ValidateActionStep(actionStep, "REPLACE");
        ValidateReinsertProcessingOption((string)payload, appConfig.AppId);

        LogEagleRequest("REPLACE", appConfig.AppId, resource.Identifier, correlationId, (string)payload);
        var responseBody = await SendSoapRequestAsync(
            new Uri(actionStep.EndPoint), payload, appConfig,
            SCIMDirections.Outbound, correlationId, cancellationToken);

        ValidateEagleResponse(responseBody, appConfig.AppId);
        LogEagleSuccess("REPLACE", appConfig.AppId, responseBody);
        return resource; 
    }

    public override Task UpdateAsync(
        dynamic payload,
        Core2EnterpriseUser resource,
        AppConfig appConfig,
        string correlationId)
    {
        throw new NotSupportedException("Use the ActionStep overload for SOAPEagle operations.");
    }

    public override async Task UpdateAsync(
        dynamic payload,
        Core2EnterpriseUser resource,
        string appId,
        AppConfig appConfig,
        ActionStep actionStep,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        await ReplaceAsync(payload, resource, appId, appConfig, actionStep, correlationId, cancellationToken);
    }

    public override Task DeleteAsync(
        string identifier,
        AppConfig appConfig,
        string correlationId)
    {
        throw new NotSupportedException("Use the ActionStep overload for SOAPEagle operations.");
    }
   
    public override async Task DeleteAsync(
        string identifier,
        string appId,
        AppConfig appConfig,
        ActionStep actionStep,
        string correlationId,
        CancellationToken cancellationToken = default)
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
        var injected = template.Replace("{{CorrelationId}}", Guid.NewGuid().ToString(), StringComparison.Ordinal);
        var soapPayload = SOAPParserUtil<Core2EnterpriseUser>.BuildPayload(injected, attributes, resource);
        EnsureAllPlaceholdersResolved(soapPayload, appConfig.AppId);

        LogEagleRequest("DELETE", appConfig.AppId, identifier, correlationId, soapPayload);
        var responseBody = await SendSoapRequestAsync(new Uri(actionStep.EndPoint), soapPayload, appConfig, SCIMDirections.Outbound, correlationId, cancellationToken);
        ValidateEagleResponse(responseBody, appConfig.AppId);
        LogEagleSuccess("DELETE", appConfig.AppId, responseBody);

        //A positive taskAcknowledgement means Eagle *accepted* the delete, not that it completed.
        //Confirm the end state via the Extract Service before reporting success to SCIM.
        await VerifyUserDeletedAsync(identifier, appConfig, correlationId, cancellationToken);
    }

    //Delete-verification retry policy (overridable in tests so unit tests don't actually sleep).
    protected virtual int DeleteVerifyMaxAttempts => 3;
    protected virtual int DeleteVerifyDelayMs => 2000;

    //Polls the Extract Service until the user is absent (delete confirmed). Absence — HTTP 404 or an
    //envelope with no user record — surfaces as HttpResponseException(NotFound) from the REST fetch.
    //Deleting an already-absent user therefore reports success (idempotent). If no GET endpoint is
    //configured, verification is skipped with a warning rather than failing the delete.
    private async Task VerifyUserDeletedAsync(
        string identifier, AppConfig appConfig, string correlationId, CancellationToken cancellationToken)
    {
        var getEndpoint = appConfig.Actions?
                .FirstOrDefault(a => a.ActionName == ActionNames.GET)?
                .ActionSteps?.OrderBy(s => s.StepOrder).FirstOrDefault()?.EndPoint
            ?? appConfig.UserURIs?.FirstOrDefault()?.Get?.ToString();

        if (string.IsNullOrWhiteSpace(getEndpoint))
        {
            Log.Warning(
                "Eagle delete accepted but verification skipped — no GET endpoint configured. AppId: {AppId}, userId: {UserId}, correlationId: {CorrelationId}",
                appConfig.AppId, identifier, correlationId);
            return;
        }

        for (var attempt = 1; attempt <= DeleteVerifyMaxAttempts; attempt++)
        {
            try
            {
                await FetchEagleUserViaRestAsync(getEndpoint, identifier, appConfig, cancellationToken);
            }
            catch (HttpResponseException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
            {
                return; // user gone → delete confirmed (or was already absent — idempotent success)
            }

            if (attempt < DeleteVerifyMaxAttempts)
            {
                await Task.Delay(DeleteVerifyDelayMs, cancellationToken);
            }
        }

        throw new InvalidOperationException(
            $"Eagle accepted the delete but the user is still returned by the Extract Service after " +
            $"{DeleteVerifyMaxAttempts} attempts. AppId: {appConfig.AppId}, userId: {identifier}, correlationId: {correlationId}");
    }

    public override async Task<Core2EnterpriseUser> GetAsync(
        string identifier,
        AppConfig appConfig,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var restBaseUrl = appConfig.UserURIs?.FirstOrDefault()?.Get?.ToString()
            ?? throw new InvalidOperationException(
                $"Eagle REST GET URI not configured for AppId {appConfig.AppId}. Set UserURIs[0].Get, or route " +
                "reads through the ActionStep overload (the normal path for IntegrationMethodOutbound = SOAPEagle).");

        return await FetchEagleUserViaRestAsync(restBaseUrl, identifier, appConfig, cancellationToken);
    }
   
    public override async Task<Core2EnterpriseUser> GetAsync(
        string identifier,
        AppConfig appConfig,
        ActionStep actionStep,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ValidateActionStep(actionStep, "GET");
        return await FetchEagleUserViaRestAsync(actionStep.EndPoint, identifier, appConfig, cancellationToken);
    }

    //Parse Eagle taskAcknowledgement/taskStatusResponse instead of a user record
    public override Core2EnterpriseUser ParseSoapUserResponse(string responseBody)
    {
        var xmlDoc = new XmlDocument { XmlResolver = null };
        xmlDoc.LoadXml(UnescapeXmlPayload(responseBody));

        ValidateEagleResponseCore(xmlDoc, appId: null);

        var correlationNode = xmlDoc.SelectSingleNode("//*[local-name()='correlationId']");
        return new Core2EnterpriseUser { Identifier = correlationNode?.InnerText ?? string.Empty };
    }

    //Eagle responses never carry a user identifier; return empty after response validation
    public override string ExtractIdentifierFromSoapResponse(string responseBody, AppConfig appConfig)
    {
        ValidateEagleResponse(responseBody, appConfig.AppId);
        return string.Empty;
    }

    //Validates an Eagle SOAP response by shape: taskAcknowledgement (async ack — isNegative) or
    //taskStatusResponse (synchronousExecution — status/severityCode/failedRecords). Unknown shapes
    //throw: a response must never default to success.
    private static void ValidateEagleResponse(string responseBody, string appId)
    {
        var xmlDoc = new XmlDocument { XmlResolver = null };
        xmlDoc.LoadXml(UnescapeXmlPayload(responseBody));
        ValidateEagleResponseCore(xmlDoc, appId);
    }

    private static void ValidateEagleResponseCore(XmlDocument xmlDoc, string? appId)
    {
        if (xmlDoc.SelectSingleNode("//*[local-name()='taskAcknowledgement']") != null)
        {
            var isNegativeNode = xmlDoc.SelectSingleNode("//*[local-name()='isNegative']");
            if (isNegativeNode?.InnerText.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) == true)
            {
                throw new InvalidOperationException(
                    $"Eagle operation failed (isNegative=true). AppId: {appId}, " +
                    $"correlationId: {FirstElementText(xmlDoc, "correlationId")}, messageId: {FirstElementText(xmlDoc, "messageId")}.");
            }

            return;
        }

        if (xmlDoc.SelectSingleNode("//*[local-name()='taskStatusResponse']") != null)
        {
            var status = FirstElementText(xmlDoc, "status");
            var detail = $"AppId: {appId}, correlationId: {FirstElementText(xmlDoc, "correlationId")}, " +
                         $"severityCode: {FirstElementText(xmlDoc, "severityCode")}, eagleStatId: {FirstElementText(xmlDoc, "eagleStatId")}";

            if (!string.Equals(status, "SUCCESS", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Eagle task did not succeed. Status: {status}. {detail}.");
            }

            var failedRecordsNodes = xmlDoc.SelectNodes("//*[local-name()='failedRecords']");
            if (failedRecordsNodes != null)
            {
                foreach (XmlNode node in failedRecordsNodes)
                {
                    var text = node.InnerText.Trim();
                    if (!long.TryParse(text, out var failed) || failed != 0)
                    {
                        throw new InvalidOperationException($"Eagle task reported failedRecords={text}. {detail}.");
                    }
                }
            }

            return;
        }

        throw new InvalidOperationException(
            $"Unrecognized Eagle response — expected taskAcknowledgement or taskStatusResponse. AppId: {appId}.");
    }

    // Removes JSON-escaped backslash-quotes (\" → ") that appear when the payload
    // template is stored or transported as a JSON string value and then used directly as XML.
    private static string UnescapeXmlPayload(string payload) =>
        payload.Contains('\\') ? payload.Replace("\\\"", "\"") : payload;

    private static string FirstElementText(XmlDocument xmlDoc, string localName) =>
        xmlDoc.SelectSingleNode($"//*[local-name()='{localName}']")?.InnerText.Trim() ?? "(missing)";

    //Links the SCIM correlationId to the Eagle correlationId in the outbound payload, so an Eagle
    //EJM task / load file (named by the Eagle correlationId) is traceable from KloudIdentity logs.
    //Identifiers only — never the payload body (contains user PII).
    private static void LogEagleRequest(string operation, string appId, string? userId, string scimCorrelationId, string payload)
    {
        Log.Information(
            "Eagle {Operation} request. AppId: {AppId}, userId: {UserId}, scimCorrelationId: {ScimCorrelationId}, eagleCorrelationId: {EagleCorrelationId}",
            operation, appId, userId ?? "(unknown)", scimCorrelationId, ExtractCorrelationId(payload));
    }

    private static void LogEagleSuccess(string operation, string appId, string responseBody)
    {
        try
        {
            var doc = new XmlDocument { XmlResolver = null };
            doc.LoadXml(UnescapeXmlPayload(responseBody));
            Log.Information(
                "Eagle {Operation} succeeded. AppId: {AppId}, eagleCorrelationId: {EagleCorrelationId}, status: {Status}, eagleStatId: {EagleStatId}",
                operation, appId,
                FirstElementText(doc, "correlationId"), FirstElementText(doc, "status"), FirstElementText(doc, "eagleStatId"));
        }
        catch (XmlException)
        {
            // Response already validated upstream; a log-only parse issue must never fail the operation.
        }
    }

    private static string ExtractCorrelationId(string xmlPayload)
    {
        try
        {
            var doc = new XmlDocument { XmlResolver = null };
            doc.LoadXml(UnescapeXmlPayload(xmlPayload));
            return doc.SelectSingleNode("//*[local-name()='correlationId']")?.InnerText.Trim() ?? "(none)";
        }
        catch (XmlException)
        {
            return "(unparseable)";
        }
    }

    //Fails fast at payload-build time when any {{token}} survives substitution — a mis-aligned
    //template/mapping pair must never reach Eagle as literal placeholder text.
    private static void EnsureAllPlaceholdersResolved(string payload, string appId)
    {
        var unresolved = Regex.Matches(payload, @"\{\{([^{}]+)\}\}")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (unresolved.Count > 0)
        {
            throw new InvalidOperationException(
                $"Eagle payload contains unresolved placeholders: {string.Join(", ", unresolved)}. " +
                $"Each placeholder must match an attribute mapping's DestinationField on the ActionStep. AppId: {appId}");
        }
    }

    //Eagle's default CHANGE behavior only ever merges group/role assignments — REINSERT is the only
    //mechanism to revoke or fully replace them. A REPLACE (SCIM PUT) or UPDATE (SCIM PATCH) template
    //missing <processingOptions>REINSERT</processingOptions> would silently degrade to merge-only
    //semantics, leaving revoked roles in place.
    private static void ValidateReinsertProcessingOption(string xmlPayload, string appId)
    {
        var xmlDoc = new XmlDocument { XmlResolver = null };
        xmlDoc.LoadXml(UnescapeXmlPayload(xmlPayload));

        var node = xmlDoc.SelectSingleNode("//*[local-name()='processingOptions']");
        if (node == null || node.InnerText.Trim() != "REINSERT")
        {
            throw new InvalidOperationException(
                "Eagle REPLACE/UPDATE payload must contain <processingOptions>REINSERT</processingOptions>; " +
                "without it Eagle merges instead of replacing and revoked roles are never removed. " +
                $"Fix the ActionStep template. AppId: {appId}");
        }
    }

    //Extracts user identifier from the outbound EML XML payload
    private static string ExtractUserIdFromPayload(string xmlPayload)
    {
        var xmlDoc = new XmlDocument { XmlResolver = null };
        xmlDoc.LoadXml(UnescapeXmlPayload(xmlPayload));

        var idNode = xmlDoc.SelectSingleNode("//*[local-name()='userId']");
        if (idNode == null || string.IsNullOrEmpty(idNode.InnerText))
            throw new InvalidOperationException("Eagle EML payload does not contain a <userId> element.");

        var userId = idNode.InnerText;
        if (userId.Contains("{{", StringComparison.Ordinal))
            throw new InvalidOperationException($"Eagle EML payload <userId> contains an unresolved placeholder '{userId}'. Verify the Identifier attribute mapping in AppConfig.");

        return userId;
    }

    //Issues HTTP GET to Eagle REST endpoint. Missing users throw HttpResponseException(NotFound) —
    //the only signal the SCIM ControllerTemplate maps to HTTP 404; any other exception surfaces as 500.
    private async Task<Core2EnterpriseUser> FetchEagleUserViaRestAsync(
        string baseUrl,
        string identifier,
        AppConfig appConfig,
        CancellationToken cancellationToken)
    {
        var separator = baseUrl.Contains('?') ? "&" : "?";
        var url = baseUrl.TrimEnd('/')
            + separator + "userid=" + Uri.EscapeDataString(identifier)
            + "&outputFormat=" + DefaultOutputFormat
            + "&streamName=" + DefaultExtractStreamName;
        var client = _httpClientFactory.CreateClient();

        var requiresAuth = appConfig.AuthenticationFlow?.Steps?.Count > 0
            || appConfig.AuthenticationMethodOutbound != AuthenticationMethods.None;

        if (requiresAuth)
        {
            if (await GetAuthenticationAsync(appConfig, SCIMDirections.Outbound, cancellationToken) is not Dictionary<int, string> tokenDict
                || tokenDict.Count == 0)
                throw new InvalidOperationException(
                    $"Authentication is required for Eagle REST GET (AppId: {appConfig.AppId}) but no token was resolved. " +
                    "Verify that AuthenticationFlow.Steps is correctly configured.");

            var steps = appConfig.AuthenticationFlow?.Steps;
            foreach (var authToken in tokenDict)
            {
                var step = steps?.FirstOrDefault(s => s.StepOrder == authToken.Key);
                var authMethod = step?.AuthenticationMethod ?? appConfig.AuthenticationMethodOutbound;
                var authDetails = step != null && HasAuthenticationDetails((object?)step.AuthenticationDetails)
                    ? NormalizeAuthenticationDetails(step.AuthenticationDetails)
                    : NormalizeAuthenticationDetails(appConfig.AuthenticationDetails);

                if (authMethod != AuthenticationMethods.None
                    && !string.IsNullOrWhiteSpace(authToken.Value))
                {
                    Utils.HttpClientExtensions.SetAuthenticationHeaders(
                        client, authMethod, authDetails, authToken.Value);
                }
            }
        }

        var response = await client.GetAsync(url, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new HttpResponseException(HttpStatusCode.NotFound);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseEagleRestUserResponse(json, identifier)
            ?? throw new HttpResponseException(HttpStatusCode.NotFound);
    }

    //Maps Eagle REST JSON (userAdministrationTransactionMessage envelope) to Core2EnterpriseUser;
    //returns null when the envelope carries no user record (not found / not yet visible)
    private static Core2EnterpriseUser? ParseEagleRestUserResponse(string json, string identifier)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Eagle REST response is not valid JSON. Response: {json}", ex);
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("userAdministrationTransactionMessage", out var message))
            {
                throw new InvalidOperationException(
                    $"Eagle REST response is not a userAdministrationTransactionMessage envelope. Response: {json}");
            }

            if (!message.TryGetProperty("userAdministrationTransaction", out var transactions)
                || transactions.ValueKind != JsonValueKind.Array
                || transactions.GetArrayLength() == 0
                || !transactions[0].TryGetProperty("user", out var user))
            {
                return null;
            }

            var userId = user.TryGetProperty("userId", out var userIdProp) ? userIdProp.GetString() : null;
            var displayName = user.TryGetProperty("userFullName", out var nameProp) ? nameProp.GetString() : null;
            var email = user.TryGetProperty("emailAddress", out var emailProp) ? emailProp.GetString() : null;

            //Round-trip identity: Create maps Entra UserName → Eagle userId, so Read must surface
            //userId as UserName. Email is an attribute, not identity.
            var result = new Core2EnterpriseUser
            {
                Identifier = userId ?? identifier,
                DisplayName = displayName ?? string.Empty,
                UserName = userId ?? identifier
            };

            if (!string.IsNullOrEmpty(email))
            {
                result.ElectronicMailAddresses =
                    [new ElectronicMailAddress { Value = email, ItemType = "work" }];
            }

            return result;
        }
    }
}
