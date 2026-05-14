//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using System.Text.Json;
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

namespace KN.KloudIdentity.Mapper.MapperCore;

public class EagleSOAPIntegration : SOAPIntegration
{
    private const string EagleNamespace = "http://www.eagleinvsys.com/2011/EagleML-2-0";
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

    public override async Task<dynamic> MapAndPreparePayloadAsync(
        IList<AttributeSchema> schema,
        Core2EnterpriseUser resource,
        AppConfig appConfig,
        CancellationToken cancellationToken = default)
    {
        var template = appConfig.SOAPTemplates?.FirstOrDefault()
            ?? throw new InvalidOperationException($"SOAP template required. AppId: {appConfig.AppId}");

        // Inject {{CorrelationId}} before delegating to base
        var injected = template.Template.Replace("{{CorrelationId}}", Guid.NewGuid().ToString(), StringComparison.Ordinal);
        var patchedConfig = appConfig with { SOAPTemplates = [new SOAPTemplate(injected, template.Action)] };

        return await base.MapAndPreparePayloadAsync(schema, resource, patchedConfig, cancellationToken);
    }

    public override async Task<Core2EnterpriseUser?> ProvisionAsync(
        dynamic payload,
        AppConfig appConfig,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        //Extract userId from outbound payload; verify ACK
        var userUri = appConfig.UserURIs?.FirstOrDefault()?.Post
            ?? throw new InvalidOperationException("Eagle WSDL endpoint not configured.");

        var userId = ExtractUserIdFromPayload((string)payload);
        var responseBody = await SendSoapRequestAsync(userUri, payload, appConfig, SCIMDirections.Outbound, correlationId, cancellationToken);
        CheckEagleAck(responseBody, appConfig.AppId);

        return new Core2EnterpriseUser { Identifier = userId };
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
        var responseBody = await SendSoapRequestAsync(new Uri(actionStep.EndPoint), payload, appConfig, SCIMDirections.Outbound, correlationId, cancellationToken);
        CheckEagleAck(responseBody, appConfig.AppId);

        return new Core2EnterpriseUser { Identifier = userId };
    }

    public override async Task ReplaceAsync(
        dynamic payload,
        Core2EnterpriseUser resource,
        AppConfig appConfig,
        string correlationId)
    {
        var userUri = appConfig.UserURIs?.FirstOrDefault()?.Put
            ?? throw new InvalidOperationException("Eagle Replace endpoint not configured.");

        var responseBody = await SendSoapRequestAsync(userUri, payload, appConfig, SCIMDirections.Outbound, correlationId);
        CheckEagleAck(responseBody, appConfig.AppId);
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

        var responseBody = await SendSoapRequestAsync(
            new Uri(actionStep.EndPoint), payload, appConfig,
            SCIMDirections.Outbound, correlationId, cancellationToken);

        CheckEagleAck(responseBody, appConfig.AppId);
        return resource; // preserve caller's resource.Identifier — Eagle ACK carries correlationId, not the user ID
    }

    public override async Task UpdateAsync(
        dynamic payload,
        Core2EnterpriseUser resource,
        AppConfig appConfig,
        string correlationId)
    {
        var userUri = appConfig.UserURIs?.FirstOrDefault()?.Patch
                   ?? appConfig.UserURIs?.FirstOrDefault()?.Put
                   ?? throw new InvalidOperationException("Eagle Update endpoint not configured.");

        var responseBody = await SendSoapRequestAsync(userUri, payload, appConfig, SCIMDirections.Outbound, correlationId);
        CheckEagleAck(responseBody, appConfig.AppId);
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
        ValidateActionStep(actionStep, "UPDATE");

        var responseBody = await SendSoapRequestAsync(new Uri(actionStep.EndPoint), payload, appConfig, SCIMDirections.Outbound, correlationId, cancellationToken);
        CheckEagleAck(responseBody, appConfig.AppId);
    }

    public override async Task DeleteAsync(
        string identifier,
        AppConfig appConfig,
        string correlationId)
    {
        var userUri = appConfig.UserURIs?.FirstOrDefault()?.Delete
            ?? throw new InvalidOperationException("Eagle Delete endpoint not configured.");

        var template = appConfig.SOAPTemplates?.FirstOrDefault(t => t.Action == SOAPActions.Delete)
            ?? throw new InvalidOperationException($"SOAP template for DELETE action not configured. AppId: {appConfig.AppId}");

        var attributes = (appConfig.UserAttributeSchemas ?? Array.Empty<AttributeSchema>())
            .Where(p => p.HttpRequestType == HttpRequestTypes.DELETE)
            .ToList();

        if (attributes.Count == 0 || !attributes.Any(a => a.SourceValue.Equals("Identifier", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"DELETE attribute schema must include an Identifier mapping. AppId: {appConfig.AppId}");

        var resource = new Core2EnterpriseUser { Identifier = identifier };
        var injected = template.Template.Replace("{{CorrelationId}}", Guid.NewGuid().ToString(), StringComparison.Ordinal);
        var soapPayload = SOAPParserUtil<Core2EnterpriseUser>.BuildPayload(injected, attributes, resource);

        var responseBody = await SendSoapRequestAsync(userUri, soapPayload, appConfig, SCIMDirections.Outbound, correlationId);
        CheckEagleAck(responseBody, appConfig.AppId);
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

        var template = ResolveSoapTemplate(appConfig, MapHttpVerbToSoapAction(actionStep.HttpVerb, "DELETE"));
        var attributes = actionStep.UserAttributeSchemas?.ToList()
            ?? throw new InvalidOperationException($"No attributes configured on ActionStep {actionStep.StepOrder} for DELETE. AppId: {appConfig.AppId}");

        var resource = new Core2EnterpriseUser { Identifier = identifier };

         // Inject {{CorrelationId}} into template + ACK check
        var injected = template.Template.Replace("{{CorrelationId}}", Guid.NewGuid().ToString(), StringComparison.Ordinal);
        var soapPayload = SOAPParserUtil<Core2EnterpriseUser>.BuildPayload(injected, attributes, resource);

        var responseBody = await SendSoapRequestAsync(new Uri(actionStep.EndPoint), soapPayload, appConfig, SCIMDirections.Outbound, correlationId, cancellationToken);
        CheckEagleAck(responseBody, appConfig.AppId);
    }

    public override async Task<Core2EnterpriseUser> GetAsync(
        string identifier,
        AppConfig appConfig,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var restBaseUrl = appConfig.UserURIs?.FirstOrDefault()?.Get?.ToString()
            ?? throw new InvalidOperationException("Eagle REST GET URI not configured.");

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

    //Parse Eagle taskAcknowledgement instead of a user record
    public override Core2EnterpriseUser ParseSoapUserResponse(string responseBody)
    {
        var xmlDoc = new XmlDocument { XmlResolver = null };
        xmlDoc.LoadXml(responseBody);

        var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
        nsmgr.AddNamespace("eag1", EagleNamespace);

        var isNegativeNode = xmlDoc.SelectSingleNode("//eag1:isNegative", nsmgr);
        if (isNegativeNode?.InnerText.Equals("true", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new InvalidOperationException($"Eagle operation failed (isNegative=true). Response: {responseBody}");
        }

        var correlationNode = xmlDoc.SelectSingleNode("//eag1:correlationId", nsmgr);
        return new Core2EnterpriseUser { Identifier = correlationNode?.InnerText ?? string.Empty };
    }

    //Eagle ACK never contains a user identifier; return empty after ACK check
    public override string ExtractIdentifierFromSoapResponse(string responseBody, AppConfig appConfig)
    {
        CheckEagleAck(responseBody, appConfig.AppId);
        return string.Empty;
    }

    //Parses isNegative from Eagle taskAcknowledgement; throws on failure
    private static void CheckEagleAck(string responseBody, string appId)
    {
        var xmlDoc = new XmlDocument { XmlResolver = null };
        xmlDoc.LoadXml(responseBody);

        var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
        nsmgr.AddNamespace("eag1", EagleNamespace);

        var isNegativeNode = xmlDoc.SelectSingleNode("//eag1:isNegative", nsmgr);
        if (isNegativeNode?.InnerText.Equals("true", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new InvalidOperationException($"Eagle operation failed (isNegative=true). AppId: {appId}. Response: {responseBody}");
        }
    }

    //Extracts user identifier from the outbound EML XML payload
    private static string ExtractUserIdFromPayload(string xmlPayload)
    {
        var xmlDoc = new XmlDocument { XmlResolver = null };
        xmlDoc.LoadXml(xmlPayload);

        var idNode = xmlDoc.SelectSingleNode("//*[local-name()='userId']");
        if (idNode == null || string.IsNullOrEmpty(idNode.InnerText))
            throw new InvalidOperationException("Eagle EML payload does not contain a <userId> element.");

        var userId = idNode.InnerText;
        if (userId.Contains("{{", StringComparison.Ordinal))
            throw new InvalidOperationException($"Eagle EML payload <userId> contains an unresolved placeholder '{userId}'. Verify the Identifier attribute mapping in AppConfig.");

        return userId;
    }

    //Issues HTTP GET to Eagle REST endpoint; returns mapped user
    private async Task<Core2EnterpriseUser> FetchEagleUserViaRestAsync(
        string baseUrl,
        string identifier,
        AppConfig appConfig,
        CancellationToken cancellationToken)
    {
        var url = baseUrl.TrimEnd('/') + "?userid=" + Uri.EscapeDataString(identifier);
        var client = _httpClientFactory.CreateClient();

        if (appConfig.AuthenticationFlow?.Steps?.Count > 0)
        {
            if (await GetAuthenticationAsync(
                    appConfig, SCIMDirections.Outbound, cancellationToken)
                is Dictionary<int, string> tokenDict)
            {
                var steps = appConfig.AuthenticationFlow.Steps;
                foreach (var authToken in tokenDict)
                {
                    var step = steps.FirstOrDefault(s => s.StepOrder == authToken.Key);
                    var authMethod = step?.AuthenticationMethod ?? AuthenticationMethods.None;
                    var authDetails = step?.AuthenticationDetails != null
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
        }

        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseEagleRestUserResponse(json, identifier);
    }

    //Maps Eagle REST JSON response to Core2EnterpriseUser
    private static Core2EnterpriseUser ParseEagleRestUserResponse(string json, string identifier)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var userId = root.TryGetProperty("userId", out var userIdProp) ? userIdProp.GetString() : null;
        var displayName = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
        var email = root.TryGetProperty("emailAddress", out var emailProp) ? emailProp.GetString() : null;

        return new Core2EnterpriseUser
        {
            Identifier = userId ?? identifier,
            DisplayName = displayName ?? string.Empty,
            UserName = email ?? string.Empty
        };
    }
}
