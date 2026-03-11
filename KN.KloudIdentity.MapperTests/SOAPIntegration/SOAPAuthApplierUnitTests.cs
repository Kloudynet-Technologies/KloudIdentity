using System.Net;
using System.Net.Http;
using System.Xml;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.MapperCore;

namespace KN.KloudIdentity.MapperTests.SOAPIntegration;

public class SOAPAuthApplierUnitTests
{
    [Fact]
    public async Task SoapTransportAuthApplier_WithNtlmExplicitCredentials_SetsNetworkCredential()
    {
        var handler = new HttpClientHandler();
        var context = CreateContext(
            handler: handler,
            authOptions: new SOAPAuthenticationOptions
            {
                Transport = new BasicOrNtlmSoapAuthOptions
                {
                    Enabled = true,
                    UseNtlm = true,
                    UseDefaultCredentials = false,
                    Username = "ntlm-user",
                    Password = "ntlm-pass",
                    Domain = "CORP"
                }
            });

        await new SoapTransportAuthApplier().ApplyAsync(context);

        var credential = Assert.IsType<NetworkCredential>(handler.Credentials);
        Assert.Equal("ntlm-user", credential.UserName);
        Assert.Equal("ntlm-pass", credential.Password);
        Assert.Equal("CORP", credential.Domain);
        Assert.False(handler.UseDefaultCredentials);
    }

    [Fact]
    public async Task SoapTransportAuthApplier_WithNtlmDefaultCredentials_UsesDefaultCredentialFlag()
    {
        var handler = new HttpClientHandler();
        var context = CreateContext(
            handler: handler,
            authOptions: new SOAPAuthenticationOptions
            {
                Transport = new BasicOrNtlmSoapAuthOptions
                {
                    Enabled = true,
                    UseNtlm = true,
                    UseDefaultCredentials = true
                }
            });

        await new SoapTransportAuthApplier().ApplyAsync(context);

        Assert.True(handler.UseDefaultCredentials);
        Assert.NotNull(handler.Credentials);
    }

    [Fact]
    public async Task SoapTransportAuthApplier_WithAuthorizationPlacement_AddsBearerHeader()
    {
        var context = CreateContext(
            token: "token-abc",
            authOptions: new SOAPAuthenticationOptions
            {
                TokenPlacement = new SoapTokenPlacementOptions
                {
                    Enabled = true,
                    UseAuthorizationHeader = true
                }
            });

        await new SoapTransportAuthApplier().ApplyAsync(context);

        Assert.NotNull(context.Request.Headers.Authorization);
        Assert.Equal("Bearer", context.Request.Headers.Authorization!.Scheme);
        Assert.Equal("token-abc", context.Request.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task SoapTransportAuthApplier_WithCustomHttpPlacement_ReplacesTokenPlaceholder()
    {
        var context = CreateContext(
            token: "token-xyz",
            authOptions: new SOAPAuthenticationOptions
            {
                TokenPlacement = new SoapTokenPlacementOptions
                {
                    Enabled = true,
                    CustomHttpHeaders = new Dictionary<string, string>
                    {
                        ["X-Api-Token"] = "Bearer {{token}}"
                    }
                }
            });

        await new SoapTransportAuthApplier().ApplyAsync(context);

        Assert.True(context.Request.Headers.TryGetValues("X-Api-Token", out var values));
        Assert.Equal("Bearer token-xyz", values.Single());
    }

    [Fact]
    public async Task WsSecuritySoapAuthApplier_WithTimestamp_AddsExpectedWsSecurityNodes()
    {
        var context = CreateContext(
            authOptions: new SOAPAuthenticationOptions
            {
                WsSecurity = new WsSecuritySoapAuthOptions
                {
                    Enabled = true,
                    Username = "ws-user",
                    Password = "ws-pass",
                    IncludeTimestamp = true
                }
            });

        await new WsSecuritySoapAuthApplier().ApplyAsync(context);

        var doc = new XmlDocument();
        doc.LoadXml(context.Payload);
        var ns = new XmlNamespaceManager(doc.NameTable);
        ns.AddNamespace("soap", "http://schemas.xmlsoap.org/soap/envelope/");
        ns.AddNamespace("wsse", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd");
        ns.AddNamespace("wsu", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd");

        Assert.NotNull(doc.SelectSingleNode("/soap:Envelope/soap:Header/wsse:Security", ns));
        Assert.Equal("ws-user", doc.SelectSingleNode("//wsse:Username", ns)?.InnerText);
        Assert.Equal("ws-pass", doc.SelectSingleNode("//wsse:Password", ns)?.InnerText);
        Assert.NotNull(doc.SelectSingleNode("//wsu:Timestamp", ns));
    }

    [Fact]
    public async Task WsSecuritySoapAuthApplier_WithoutTimestamp_DoesNotAddTimestampNode()
    {
        var context = CreateContext(
            authOptions: new SOAPAuthenticationOptions
            {
                WsSecurity = new WsSecuritySoapAuthOptions
                {
                    Enabled = true,
                    Username = "ws-user",
                    Password = "ws-pass",
                    IncludeTimestamp = false
                }
            });

        await new WsSecuritySoapAuthApplier().ApplyAsync(context);

        var doc = new XmlDocument();
        doc.LoadXml(context.Payload);
        var ns = new XmlNamespaceManager(doc.NameTable);
        ns.AddNamespace("wsu", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd");

        Assert.Null(doc.SelectSingleNode("//wsu:Timestamp", ns));
    }

    [Fact]
    public async Task SoapTokenHeaderApplier_WithSoapHeaderPlacement_AddsConfiguredHeaderFragment()
    {
        var context = CreateContext(
            token: "soap-token",
            authOptions: new SOAPAuthenticationOptions
            {
                TokenPlacement = new SoapTokenPlacementOptions
                {
                    Enabled = true,
                    SoapHeaderTemplate = "<auth:Token xmlns:auth='urn:test'>{{token}}</auth:Token>"
                }
            });

        await new SoapTokenHeaderApplier().ApplyAsync(context);

        var doc = new XmlDocument();
        doc.LoadXml(context.Payload);
        var ns = new XmlNamespaceManager(doc.NameTable);
        ns.AddNamespace("soap", "http://schemas.xmlsoap.org/soap/envelope/");
        ns.AddNamespace("auth", "urn:test");

        Assert.Equal("soap-token", doc.SelectSingleNode("/soap:Envelope/soap:Header/auth:Token", ns)?.InnerText);
    }

    private static SoapAuthContext CreateContext(
        HttpClientHandler? handler = null,
        SOAPAuthenticationOptions? authOptions = null,
        string? token = null,
        string? payload = null)
    {
        return new SoapAuthContext
        {
            AppConfig = new AppConfig
            {
                AppId = "soap-app",
                AuthenticationDetails = new { },
                UserAttributeSchemas = new List<AttributeSchema>(),
                UserURIs = new List<UserURIs>()
            },
            Direction = SCIMDirections.Outbound,
            HttpClient = new HttpClient(),
            Request = new HttpRequestMessage(HttpMethod.Post, "https://soap.example.test/users"),
            Handler = handler,
            Token = token,
            AuthOptions = authOptions,
            Payload = payload ??
                "<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\"><soap:Body><CreateUser /></soap:Body></soap:Envelope>"
        };
    }
}
