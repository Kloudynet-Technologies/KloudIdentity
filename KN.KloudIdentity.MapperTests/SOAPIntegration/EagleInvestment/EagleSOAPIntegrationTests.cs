//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.MapperCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.SCIM;
using Moq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml;

namespace KN.KloudIdentity.MapperTests.SOAPIntegration.EagleInvestment;

public class EagleSOAPIntegrationTests
{
    #region  Payload Validation

    [Fact]
    public async Task MapAndPreparePayloadAsync_InjectsValidGuidAsCorrelationId()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig(
            templates:
            [
                new("<env><correlationId>{{CorrelationId}}</correlationId></env>", SOAPActions.Create)
            ]);

        var payload = await sut.MapAndPreparePayloadAsync(
            new List<AttributeSchema>(), new Core2EnterpriseUser(), appConfig);

        string result = Assert.IsType<string>(payload);
        var xmlDoc = new XmlDocument { XmlResolver = null };
        xmlDoc.LoadXml(result);

        var node = xmlDoc.SelectSingleNode("//*[local-name()='correlationId']");
        Assert.NotNull(node);
        Assert.True(Guid.TryParse(node!.InnerText, out _));
    }

    [Fact]
    public async Task MapAndPreparePayloadAsync_TwoConsecutiveCalls_ProduceDifferentCorrelationIds()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig(
            templates:
            [
                new("<env><correlationId>{{CorrelationId}}</correlationId></env>", SOAPActions.Create)
            ]);

        var payload1 = await sut.MapAndPreparePayloadAsync(
            new List<AttributeSchema>(), new Core2EnterpriseUser(), appConfig);
        var payload2 = await sut.MapAndPreparePayloadAsync(
            new List<AttributeSchema>(), new Core2EnterpriseUser(), appConfig);

        static string ExtractCorrelationId(string xml)
        {
            var doc = new XmlDocument { XmlResolver = null };
            doc.LoadXml(xml);
            return doc.SelectSingleNode("//*[local-name()='correlationId']")!.InnerText;
        }

        var id1 = ExtractCorrelationId((string)payload1);
        var id2 = ExtractCorrelationId((string)payload2);
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public async Task MapAndPreparePayloadAsync_WithNoTemplate_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig() with { SOAPTemplates = null };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.MapAndPreparePayloadAsync(new List<AttributeSchema>(), new Core2EnterpriseUser(), appConfig));
    }

    [Fact]
    public async Task MapAndPreparePayloadAsync_WithUserFields_MapsBothCorrelationIdAndAttributes()
    {
        var sut = CreateSut();
        var schema = new List<AttributeSchema>
        {
            new() { DestinationField = "Identifier", SourceValue = "Identifier", MappingType = MappingTypes.Direct },
            new() { DestinationField = "UserName",   SourceValue = "UserName",   MappingType = MappingTypes.Direct }
        };
        var appConfig = CreateAppConfig(
            templates:
            [
                new(
                    "<env><correlationId>{{CorrelationId}}</correlationId><userId>{{Identifier}}</userId><userName>{{UserName}}</userName></env>",
                    SOAPActions.Create)
            ]);
        var resource = new Core2EnterpriseUser { Identifier = "eagle-123", UserName = "john.doe" };

        var payload = await sut.MapAndPreparePayloadAsync(schema, resource, appConfig);

        string result = Assert.IsType<string>(payload);
        var xmlDoc = new XmlDocument { XmlResolver = null };
        xmlDoc.LoadXml(result);

        var corrNode = xmlDoc.SelectSingleNode("//*[local-name()='correlationId']");
        Assert.NotNull(corrNode);
        Assert.True(Guid.TryParse(corrNode!.InnerText, out _));
        Assert.Contains("eagle-123", result);
        Assert.Contains("john.doe", result);
    }

    #endregion

    #region  Error Handling

    [Fact]
    public void ParseSoapUserResponse_WhenIsNegativeTrue_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        Assert.Throws<InvalidOperationException>(() =>
            sut.ParseSoapUserResponse(EagleAckXml(isNegative: true, correlationId: "c-err")));
    }

    [Fact]
    public async Task ProvisionAsync_WhenAckIsNegative_ThrowsInvalidOperationException()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    EagleAckXml(isNegative: true, correlationId: "c-neg"),
                    Encoding.UTF8, "text/xml")
            });
        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ProvisionAsync("<env><userId>user-001</userId></env>", appConfig, "corr-t06"));
    }

    [Fact]
    public async Task ProvisionAsync_WithHttpFailure_ThrowsHttpRequestException()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("bad request", Encoding.UTF8, "text/plain")
            });
        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            sut.ProvisionAsync("<env><userId>user-001</userId></env>", appConfig, "corr-t07"));
    }

    [Fact]
    public async Task ProvisionAsync_WithSoapFault_ThrowsHttpRequestException()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
                      <soap:Body>
                        <soap:Fault>
                          <faultcode>soap:Server</faultcode>
                          <faultstring>Eagle internal error</faultstring>
                        </soap:Fault>
                      </soap:Body>
                    </soap:Envelope>
                    """, Encoding.UTF8, "text/xml")
            });
        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            sut.ProvisionAsync("<env><userId>user-001</userId></env>", appConfig, "corr-t08"));
    }

    [Fact]
    public async Task GetAsync_WhenRestEndpointNotConfigured_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig() with
        {
            UserURIs =
            [
                new()
                {
                    AppId   = "eagle-app",
                    BaseUrl = "https://eagle.test",
                    Post    = new Uri("https://eagle.test/EagleMLWebService20"),
                    Put     = new Uri("https://eagle.test/EagleMLWebService20"),
                    Patch   = new Uri("https://eagle.test/EagleMLWebService20"),
                    Delete  = new Uri("https://eagle.test/EagleMLWebService20"),
                    Get     = null!
                }
            ]
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GetAsync("user-001", appConfig, "corr-t09"));
    }

    [Fact]
    public async Task GetAsync_WhenRestReturns404_ThrowsHttpRequestException()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.NotFound));
        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            sut.GetAsync("user-001", appConfig, "corr-t10"));
    }

    #endregion

    #region  Success Confirmation

    [Fact]
    public async Task ProvisionAsync_WithValidAck_ReturnsIdentifierFromPayload()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig();

        var result = await sut.ProvisionAsync(
            "<env><userId>john.doe</userId></env>", appConfig, "corr-t11");

        Assert.NotNull(result);
        Assert.Equal("john.doe", result.Identifier);
    }

    [Fact]
    public async Task ProvisionAsyncV2_WithValidAckAndActionStep_ReturnsIdentifierFromPayload()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig();
        var step = CreateActionStep(httpVerb: HttpVerbs.POST);

        var result = await sut.ProvisionAsync(
            "<env><userId>jane.smith</userId></env>",
            "eagle-app", appConfig, step, "corr-t12");

        Assert.NotNull(result);
        Assert.Equal("jane.smith", result.Identifier);
    }

    [Fact]
    public void ParseSoapUserResponse_WhenIsNegativeFalse_DoesNotThrow()
    {
        var sut = CreateSut();
        var user = sut.ParseSoapUserResponse(EagleAckXml(isNegative: false, correlationId: "c-001"));

        Assert.Equal("c-001", user.Identifier);
    }

    [Fact]
    public async Task DeleteAsyncV2_WithValidAck_CompletesWithoutException()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig();
        var step = CreateActionStep(
            httpVerb: HttpVerbs.DELETE,
            attributes:
            [
                new() { DestinationField = "Identifier", SourceValue = "Identifier", MappingType = MappingTypes.Direct, HttpRequestType = HttpRequestTypes.DELETE }
            ]);

        await sut.DeleteAsync("user-001", "eagle-app", appConfig, step, "corr-t14");
    }

    [Fact]
    public async Task UpdateAsyncV2_WithValidAck_CompletesWithoutException()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig();
        var step = CreateActionStep(httpVerb: HttpVerbs.PATCH);
        var resource = new Core2EnterpriseUser { Identifier = "user-001" };

        await sut.UpdateAsync("<env><userId>user-001</userId></env>",
            resource, "eagle-app", appConfig, step, "corr-t15");
    }

    #endregion

    #region  Action Mapping

    [Fact]
    public async Task ProvisionAsync_SetsSOAPActionHeader_RunTaskRequestSync()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    EagleAckXml(isNegative: false, correlationId: "c-hdr"),
                    Encoding.UTF8, "text/xml")
            });
        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();

        await sut.ProvisionAsync("<env><userId>u-001</userId></env>", appConfig, "corr-t16");

        Assert.True(handler.LastHeaders.TryGetValue("SOAPAction", out var soapAction));
        Assert.Equal("\"RunTaskRequestSync\"", soapAction);
    }

    [Fact]
    public async Task ProvisionAsync_SendsToWsdlEndpoint_NotRestEndpoint()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    EagleAckXml(isNegative: false, correlationId: "c-uri"),
                    Encoding.UTF8, "text/xml")
            });
        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();

        await sut.ProvisionAsync("<env><userId>u-001</userId></env>", appConfig, "corr-t17");

        Assert.Equal("https://eagle.test/EagleMLWebService20", handler.LastRequestUri?.ToString());
        Assert.NotEqual("https://eagle.test/eagle/v2/users", handler.LastRequestUri?.ToString());
    }

    [Fact]
    public async Task DeleteAsync_WhenNoDeleteAttributesConfigured_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig(); // schema = empty → no DELETE-typed attributes

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.DeleteAsync("user-001", appConfig, "corr-del-guard-1"));
    }

    [Fact]
    public async Task DeleteAsync_WhenIdentifierMappingMissing_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig(
            schema:
            [
                new() { DestinationField = "Email", SourceValue = "UserName", MappingType = MappingTypes.Direct, HttpRequestType = HttpRequestTypes.DELETE }
            ]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.DeleteAsync("user-001", appConfig, "corr-del-guard-2"));
    }

    [Fact]
    public async Task DeleteAsync_EmlBodyContainsActionDelete_FromTemplate()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    EagleAckXml(isNegative: false, correlationId: "c-del"),
                    Encoding.UTF8, "text/xml")
            });
        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig(
            templates:
            [
                new("<env><action>CREATE</action><correlationId>{{CorrelationId}}</correlationId></env>", SOAPActions.Create),
                new("<env><action>CHANGE</action><correlationId>{{CorrelationId}}</correlationId></env>",  SOAPActions.Update),
                new("<env><action>DELETE</action><correlationId>{{CorrelationId}}</correlationId><userId>{{Identifier}}</userId></env>", SOAPActions.Delete)
            ],
            schema:
            [
                new() { DestinationField = "Identifier", SourceValue = "Identifier", MappingType = MappingTypes.Direct, HttpRequestType = HttpRequestTypes.DELETE }
            ]);

        await sut.DeleteAsync("user-del-001", appConfig, "corr-t18");

        Assert.Contains("<action>DELETE</action>", handler.LastRequestBody);
        Assert.Contains("user-del-001", handler.LastRequestBody);
    }

    [Fact]
    public async Task UpdateAsync_EmlBodyContainsActionChange_FromTemplate()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    EagleAckXml(isNegative: false, correlationId: "c-upd"),
                    Encoding.UTF8, "text/xml")
            });
        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();
        const string payload = "<env><action>CHANGE</action><userId>user-upd-001</userId></env>";
        var resource = new Core2EnterpriseUser { Identifier = "user-upd-001" };

        await sut.UpdateAsync(payload, resource, appConfig, "corr-t19");

        Assert.Contains("<action>CHANGE</action>", handler.LastRequestBody);
    }

    #endregion

    #region  Conditional Logic

    [Fact]
    public void ExtractIdentifierFromSoapResponse_WhenAckPositive_ReturnsEmptyString()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig();

        var result = sut.ExtractIdentifierFromSoapResponse(
            EagleAckXml(isNegative: false, correlationId: "c-ok"), appConfig);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ExtractIdentifierFromSoapResponse_WhenAckNegative_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig();

        Assert.Throws<InvalidOperationException>(() =>
            sut.ExtractIdentifierFromSoapResponse(
                EagleAckXml(isNegative: true, correlationId: "c-neg"), appConfig));
    }

    [Fact]
    public async Task ProvisionAsync_WhenPayloadHasNoUserIdElement_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ProvisionAsync("<env><name>John</name></env>", appConfig, "corr-t22"));
    }

    [Fact]
    public async Task ProvisionAsync_WhenUserIdIsUnresolvedPlaceholder_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ProvisionAsync("<env><userId>{{Identifier}}</userId></env>", appConfig, "corr-placeholder"));
    }

    [Fact]
    public async Task MapAndPreparePayloadAsync_OriginalTemplateNotMutated_AfterCall()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig(
            templates:
            [
                new("<env><correlationId>{{CorrelationId}}</correlationId></env>", SOAPActions.Create)
            ]);
        var originalTemplate = appConfig.SOAPTemplates!.First().Template;

        await sut.MapAndPreparePayloadAsync(
            [], new Core2EnterpriseUser(), appConfig);

        Assert.Equal(originalTemplate, appConfig.SOAPTemplates!.First().Template);
        Assert.Contains("{{CorrelationId}}", appConfig.SOAPTemplates!.First().Template);
    }

    #endregion

    #region  REST Integration for GET

    [Fact]
    public async Task GetAsync_IssuesHttpGetMethod_NotPost()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    EagleRestUserJson("u1", "Alice"), Encoding.UTF8, "application/json")
            });
        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();

        await sut.GetAsync("u1", appConfig, "corr-t24");

        Assert.Equal(HttpMethod.Get, handler.LastRequestMethod);
    }

    [Fact]
    public async Task GetAsync_BuildsQueryUrl_WithUrlEncodedUserId()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    EagleRestUserJson("john.doe", "John Doe"), Encoding.UTF8, "application/json")
            });
        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();

        await sut.GetAsync("john.doe", appConfig, "corr-t25");

        Assert.Equal(
            "https://eagle.test/eagle/v2/users?userid=john.doe",
            handler.LastRequestUri?.ToString());
    }

    [Fact]
    public async Task GetAsync_WhenRestReturnsValidJson_ReturnsMappedUser()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    EagleRestUserJson("u1", "Alice", "alice@test.com"), Encoding.UTF8, "application/json")
            });
        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();

        var result = await sut.GetAsync("u1", appConfig, "corr-t26");

        Assert.Equal("u1", result.Identifier);
        Assert.Equal("Alice", result.DisplayName);
        Assert.Equal("alice@test.com", result.UserName);
    }

    [Fact]
    public async Task GetAsyncV2_UsesActionStepEndpoint_IgnoresUserUrisGet()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    EagleRestUserJson("u1", "Alice"), Encoding.UTF8, "application/json")
            });
        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();
        var step = CreateActionStep(endpoint: "https://eagle-v2.test/users", httpVerb: HttpVerbs.GET);

        await sut.GetAsync("u1", appConfig, step, "corr-t27");

        Assert.Contains("eagle-v2.test/users", handler.LastRequestUri?.ToString());
        Assert.DoesNotContain("eagle.test/eagle/v2/users", handler.LastRequestUri?.ToString() ?? string.Empty);
    }

    [Fact]
    public async Task GetAsyncV2_WithValidRestResponse_ReturnsMappedUser()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    EagleRestUserJson("u2", "Bob", "bob@test.com"), Encoding.UTF8, "application/json")
            });
        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();
        var step = CreateActionStep(endpoint: "https://eagle-v2.test/users", httpVerb: HttpVerbs.GET);

        var result = await sut.GetAsync("u2", appConfig, step, "corr-t28");

        Assert.Equal("u2", result.Identifier);
        Assert.Equal("Bob", result.DisplayName);
        Assert.Equal("bob@test.com", result.UserName);
    }

    #endregion

    #region Test Infrastructure

    private static EagleSOAPIntegration CreateSut(TestHttpMessageHandler? handler = null, string token = "test-token")
    {
        handler ??= new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    EagleAckXml(isNegative: false, correlationId: "default-corr"),
                    Encoding.UTF8, "text/xml")
            });

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handler));

        var authContextMock = new Mock<IAuthContext>();
        authContextMock
            .Setup(ctx => ctx.GetTokenAsync(It.IsAny<object>(), It.IsAny<SCIMDirections>()))
            .Returns(Task.FromResult(token));

        var options = Options.Create(new AppSettings());
        var configuration = new ConfigurationBuilder().Build();
        var loggerMock = new Mock<IKloudIdentityLogger>();

        return new EagleSOAPIntegration(
            authContextMock.Object,
            httpClientFactoryMock.Object,
            configuration,
            options,
            loggerMock.Object);
    }

    private static AppConfig CreateAppConfig(
        ICollection<SOAPTemplate>? templates = null,
        ICollection<AttributeSchema>? schema = null,
        AuthenticationMethods authMethodOutbound = AuthenticationMethods.Basic,
        dynamic? authDetails = null)
    {
        return new AppConfig
        {
            AppId = "eagle-app",
            IntegrationMethodOutbound = IntegrationMethods.SOAP,
            AuthenticationMethodOutbound = authMethodOutbound,
            AuthenticationDetails = authDetails ?? new { Username = "eagleuser", Password = "eaglepass" },
            UserAttributeSchemas = schema ?? new List<AttributeSchema>(),
            UserURIs =
            [
                new()
                {
                    AppId   = "eagle-app",
                    BaseUrl = "https://eagle.test",
                    Post    = new Uri("https://eagle.test/EagleMLWebService20"),
                    Put     = new Uri("https://eagle.test/EagleMLWebService20"),
                    Patch   = new Uri("https://eagle.test/EagleMLWebService20"),
                    Delete  = new Uri("https://eagle.test/EagleMLWebService20"),
                    Get     = new Uri("https://eagle.test/eagle/v2/users")
                }
            ],
            SOAPTemplates = templates ?? DefaultSOAPTemplates()
        };
    }

    private static ActionStep CreateActionStep(
        string endpoint = "https://eagle.test/EagleMLWebService20",
        HttpVerbs httpVerb = HttpVerbs.POST,
        ICollection<AttributeSchema>? attributes = null)
    {
        return new ActionStep
        {
            EndPoint = endpoint,
            HttpVerb = httpVerb,
            StepOrder = 1,
            IsMandatory = true,
            UserAttributeSchemas = attributes
        };
    }

    private static List<SOAPTemplate> DefaultSOAPTemplates() =>
    [
        new("<env><correlationId>{{CorrelationId}}</correlationId><userId>{{Identifier}}</userId></env>", SOAPActions.Create),
        new("<env><correlationId>{{CorrelationId}}</correlationId><userId>{{Identifier}}</userId></env>", SOAPActions.Update),
        new("<env><correlationId>{{CorrelationId}}</correlationId><userId>{{Identifier}}</userId></env>", SOAPActions.Delete)
    ];

    private static string EagleAckXml(bool isNegative, string correlationId) =>
        $"""
        <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"
                       xmlns:eag1="http://www.eagleinvsys.com/2011/EagleML-2-0">
          <soap:Body>
            <eag1:taskAcknowledgement>
              <eag1:correlationId>{correlationId}</eag1:correlationId>
              <eag1:isNegative>{isNegative.ToString().ToLower()}</eag1:isNegative>
            </eag1:taskAcknowledgement>
          </soap:Body>
        </soap:Envelope>
        """;

    private static string EagleRestUserJson(string userId, string displayName, string emailAddress = "") =>
        $$"""{"userId":"{{userId}}","name":"{{displayName}}","emailAddress":"{{emailAddress}}"}""";

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public TestHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
            => _responseFactory = responseFactory;

        public string LastRequestBody { get; private set; } = string.Empty;
        public AuthenticationHeaderValue? LastAuthorizationHeader { get; private set; }
        public Dictionary<string, string> LastHeaders { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
        public Uri? LastRequestUri { get; private set; }
        public HttpMethod LastRequestMethod { get; private set; } = HttpMethod.Get;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastRequestMethod = request.Method;
            LastRequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            LastAuthorizationHeader = request.Headers.Authorization;
            LastHeaders = request.Headers
                .ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);

            return _responseFactory(request);
        }
    }

    #endregion
}
