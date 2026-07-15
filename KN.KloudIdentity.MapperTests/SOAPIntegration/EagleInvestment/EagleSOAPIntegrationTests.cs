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
using System.Web.Http;
using System.Xml;
using System.Xml.Linq;

namespace KN.KloudIdentity.MapperTests.SOAPIntegration.EagleInvestment;

public class EagleSOAPIntegrationTests
{
    #region  Payload Validation

    [Fact]
    public async Task MapAndPreparePayloadAsync_InjectsValidGuidAsCorrelationId()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig();
        var step = CreateActionStep(template: "<env><correlationId>{{CorrelationId}}</correlationId></env>");

        var payload = await sut.MapAndPreparePayloadAsync(
            new List<AttributeSchema>(), new Core2EnterpriseUser(), appConfig, step);

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
        var appConfig = CreateAppConfig();
        var step = CreateActionStep(template: "<env><correlationId>{{CorrelationId}}</correlationId></env>");

        var payload1 = await sut.MapAndPreparePayloadAsync(
            new List<AttributeSchema>(), new Core2EnterpriseUser(), appConfig, step);
        var payload2 = await sut.MapAndPreparePayloadAsync(
            new List<AttributeSchema>(), new Core2EnterpriseUser(), appConfig, step);

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
        var appConfig = CreateAppConfig();
        var step = CreateActionStep(template: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.MapAndPreparePayloadAsync(new List<AttributeSchema>(), new Core2EnterpriseUser(), appConfig, step));
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
        var appConfig = CreateAppConfig();
        var step = CreateActionStep(
            template: "<env><correlationId>{{CorrelationId}}</correlationId><userId>{{Identifier}}</userId><userName>{{UserName}}</userName></env>");
        var resource = new Core2EnterpriseUser { Identifier = "eagle-123", UserName = "john.doe" };

        var payload = await sut.MapAndPreparePayloadAsync(schema, resource, appConfig, step);

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
    public async Task GetAsync_WhenRestReturns404_ThrowsHttpResponseExceptionNotFound()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.NotFound));
        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();

        var ex = await Assert.ThrowsAsync<HttpResponseException>(() =>
            sut.GetAsync("user-001", appConfig, "corr-t10"));
        Assert.Equal(HttpStatusCode.NotFound, ex.Response.StatusCode);
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
        //Verification (GAP-DEL-02): POST → positive ack, follow-up GET → 404 (user gone) → completes.
        var handler = new TestHttpMessageHandler(req =>
            req.Method == HttpMethod.Post
                ? XmlResponse(EagleAckXml(isNegative: false, correlationId: "c-del"))
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();
        var step = CreateActionStep(
            httpVerb: HttpVerbs.DELETE,
            template: "<env><correlationId>{{CorrelationId}}</correlationId><userId>{{Identifier}}</userId></env>",
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

        await sut.UpdateAsync("<env><processingOptions>REINSERT</processingOptions><userId>user-001</userId></env>",
            resource, "eagle-app", appConfig, step, "corr-t15");
    }

    #endregion

    #region  Action Mapping

    [Fact]
    public async Task ProvisionAsync_SetsSOAPActionHeader_RunTaskRequestSync()
    {
        var handler = new TestHttpMessageHandler(_ =>
            XmlResponse(EagleAckXml(isNegative: false, correlationId: "c-hdr")));
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
            XmlResponse(EagleAckXml(isNegative: false, correlationId: "c-uri")));
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
        var appConfig = CreateAppConfig();
        var step = CreateActionStep(
            httpVerb: HttpVerbs.DELETE,
            template: "<env><userId>{{Identifier}}</userId></env>",
            attributes: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.DeleteAsync("user-001", "eagle-app", appConfig, step, "corr-del-guard-1"));
    }

    [Fact]
    public async Task DeleteAsync_WhenEmptyAttributesOnStep_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig();
        var step = CreateActionStep(
            httpVerb: HttpVerbs.DELETE,
            template: "<env><userId>{{Identifier}}</userId></env>",
            attributes: new List<AttributeSchema>());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.DeleteAsync("user-001", "eagle-app", appConfig, step, "corr-del-guard-2"));
    }

    [Fact]
    public async Task DeleteAsync_WhenIdentifierMappingMissingOnStep_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig();
        var step = CreateActionStep(
            httpVerb: HttpVerbs.DELETE,
            template: "<env><email>{{Email}}</email></env>",
            attributes:
            [
                new() { DestinationField = "Email", SourceValue = "UserName", MappingType = MappingTypes.Direct, HttpRequestType = HttpRequestTypes.DELETE }
            ]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.DeleteAsync("user-001", "eagle-app", appConfig, step, "corr-del-guard-3"));
    }

    [Fact]
    public async Task DeleteAsync_EmlBodyContainsActionDelete_FromTemplate()
    {
        //Method-aware handler: POST → ack, GET (verification) → 404. Assert on the POST request body,
        //since the last request is now the verification GET.
        var handler = new TestHttpMessageHandler(req =>
            req.Method == HttpMethod.Post
                ? XmlResponse(EagleAckXml(isNegative: false, correlationId: "c-del"))
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();
        var step = CreateActionStep(
            httpVerb: HttpVerbs.DELETE,
            template: "<env><action>DELETE</action><correlationId>{{CorrelationId}}</correlationId><userId>{{Identifier}}</userId></env>",
            attributes:
            [
                new() { DestinationField = "Identifier", SourceValue = "Identifier", MappingType = MappingTypes.Direct, HttpRequestType = HttpRequestTypes.DELETE }
            ]);

        await sut.DeleteAsync("user-del-001", "eagle-app", appConfig, step, "corr-t18");

        var postBody = handler.Requests.First(r => r.Method == HttpMethod.Post).Body;
        Assert.Contains("<action>DELETE</action>", postBody);
        Assert.Contains("user-del-001", postBody);
    }

    [Fact]
    public async Task UpdateAsync_EmlBodyContainsActionChange_FromTemplate()
    {
        var handler = new TestHttpMessageHandler(_ =>
            XmlResponse(EagleAckXml(isNegative: false, correlationId: "c-upd")));
        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();
        var step = CreateActionStep(httpVerb: HttpVerbs.PATCH);
        const string payload = "<env><action>CHANGE</action><processingOptions>REINSERT</processingOptions><userId>user-upd-001</userId></env>";
        var resource = new Core2EnterpriseUser { Identifier = "user-upd-001" };

        await sut.UpdateAsync(payload, resource, "eagle-app", appConfig, step, "corr-t19");

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
        const string originalTemplate = "<env><correlationId>{{CorrelationId}}</correlationId></env>";
        var step = CreateActionStep(template: originalTemplate);
        var appConfig = CreateAppConfig();

        await sut.MapAndPreparePayloadAsync(
            [], new Core2EnterpriseUser(), appConfig, step);

        Assert.Equal(originalTemplate, step.Template);
        Assert.Contains("{{CorrelationId}}", step.Template);
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
            "https://eagle.test/eagle/v2/users?userid=john.doe&outputFormat=json&streamName=eagle_ml-2-0_default_out_extract_service",
            handler.LastRequestUri?.ToString());
    }

    [Fact]
    public async Task GetAsyncV2_WhenEndpointAlreadyHasQueryString_AppendsWithAmpersand()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    EagleRestUserJson("u1", "Alice"), Encoding.UTF8, "application/json")
            });
        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();
        var step = CreateActionStep(endpoint: "https://eagle-v2.test/users?env=prod", httpVerb: HttpVerbs.GET);

        await sut.GetAsync("u1", appConfig, step, "corr-t29");

        Assert.Equal(
            "https://eagle-v2.test/users?env=prod&userid=u1&outputFormat=json&streamName=eagle_ml-2-0_default_out_extract_service",
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
        Assert.Equal("u1", result.UserName);
        Assert.Equal("alice@test.com", Assert.Single(result.ElectronicMailAddresses).Value);
    }

    [Fact]
    public async Task GetAsync_WithGoldenFixture_ReturnsMappedUser()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    LoadFixture("eagle-rest-get-user.json"), Encoding.UTF8, "application/json")
            });
        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();

        var result = await sut.GetAsync("EAGLE_TEST_USER01", appConfig, "corr-fixture-01");

        Assert.Equal("EAGLE_TEST_USER01", result.Identifier);
        Assert.Equal("Eagle Test User", result.DisplayName);
        Assert.Equal("EAGLE_TEST_USER01", result.UserName);
        Assert.Equal("eagle.testuser01@example.test", Assert.Single(result.ElectronicMailAddresses).Value);
    }

    [Fact]
    public async Task GetAsync_WhenUserNotFound_ThrowsHttpResponseExceptionNotFound()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    LoadFixture("eagle-rest-get-user-notfound.json"), Encoding.UTF8, "application/json")
            });
        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();

        var ex = await Assert.ThrowsAsync<HttpResponseException>(() =>
            sut.GetAsync("missing-user", appConfig, "corr-fixture-02"));
        Assert.Equal(HttpStatusCode.NotFound, ex.Response.StatusCode);
    }

    [Fact]
    public async Task GetAsync_WhenTransactionHasNoUserNode_ThrowsHttpResponseExceptionNotFound()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"userAdministrationTransactionMessage":{"userAdministrationTransaction":[{"status":"NO_DATA"}]}}""",
                    Encoding.UTF8, "application/json")
            });
        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();

        var ex = await Assert.ThrowsAsync<HttpResponseException>(() =>
            sut.GetAsync("missing-user", appConfig, "corr-fixture-03"));
        Assert.Equal(HttpStatusCode.NotFound, ex.Response.StatusCode);
    }

    [Fact]
    public async Task GetAsync_WhenResponseIsNotEagleEnvelope_ThrowsInvalidOperationException()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"error":"unexpected server document"}""", Encoding.UTF8, "application/json")
            });
        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GetAsync("u1", appConfig, "corr-fixture-04"));
        Assert.Contains("unexpected server document", ex.Message);
    }

    [Fact]
    public async Task GetAsync_WhenResponseIsNotJson_ThrowsInvalidOperationException()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "<html><body>Eagle error page</body></html>", Encoding.UTF8, "text/html")
            });
        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GetAsync("u1", appConfig, "corr-fixture-05"));
        Assert.Contains("not valid JSON", ex.Message);
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
        Assert.Equal("u2", result.UserName);
        Assert.Equal("bob@test.com", Assert.Single(result.ElectronicMailAddresses).Value);
    }

    #endregion

    #region Replace V2

    [Fact]
    public async Task ReplaceAsyncV2_WithValidAck_CompletesAndPreservesOriginalIdentifier()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig();
        var step = CreateActionStep(httpVerb: HttpVerbs.PUT);
        var resource = new Core2EnterpriseUser { Identifier = "user-replace-001" };

        var result = await sut.ReplaceAsync(
            "<env><processingOptions>REINSERT</processingOptions><userId>user-replace-001</userId></env>",
            resource, "eagle-app", appConfig, step, "corr-replace-01");

        Assert.Equal("user-replace-001", result.Identifier);
    }

    [Fact]
    public async Task ReplaceAsyncV2_WhenAckIsNegative_ThrowsInvalidOperationException()
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
        var step = CreateActionStep(httpVerb: HttpVerbs.PUT);
        var resource = new Core2EnterpriseUser { Identifier = "user-002" };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ReplaceAsync("<env><processingOptions>REINSERT</processingOptions><userId>user-002</userId></env>",
                resource, "eagle-app", appConfig, step, "corr-replace-02"));
    }

    [Fact]
    public async Task ReplaceAsyncV2_WithoutReinsertProcessingOptions_ThrowsBeforeSendingSoapRequest()
    {
        var handler = new TestHttpMessageHandler(_ =>
            XmlResponse(EagleAckXml(isNegative: false, correlationId: "c-guard")));
        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();
        var step = CreateActionStep(httpVerb: HttpVerbs.PUT);
        var resource = new Core2EnterpriseUser { Identifier = "user-003" };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ReplaceAsync("<env><userId>user-003</userId></env>",
                resource, "eagle-app", appConfig, step, "corr-replace-03"));

        Assert.Contains("REINSERT", ex.Message);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ReplaceAsyncV2_WithWrongProcessingOptionsValue_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig();
        var step = CreateActionStep(httpVerb: HttpVerbs.PUT);
        var resource = new Core2EnterpriseUser { Identifier = "user-004" };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ReplaceAsync("<env><processingOptions>CHANGE</processingOptions><userId>user-004</userId></env>",
                resource, "eagle-app", appConfig, step, "corr-replace-04"));

        Assert.Contains("REINSERT", ex.Message);
    }

    [Fact]
    public async Task UpdateAsyncV2_WithoutReinsertProcessingOptions_ThrowsBeforeSendingSoapRequest()
    {
        //Phase 2 (GAP-UPD-03): UPDATE now enforces REINSERT like REPLACE. Eagle's default CHANGE is
        //merge-only, so an update without REINSERT would silently retain revoked roles. The working
        //Postman CHANGE capture uses REINSERT — this codifies that policy.
        var handler = new TestHttpMessageHandler(_ =>
            XmlResponse(EagleAckXml(isNegative: false, correlationId: "c-upd-guard")));
        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();
        var step = CreateActionStep(httpVerb: HttpVerbs.PATCH);
        var resource = new Core2EnterpriseUser { Identifier = "user-005" };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.UpdateAsync("<env><userId>user-005</userId></env>",
                resource, "eagle-app", appConfig, step, "corr-update-noreinsert"));

        Assert.Contains("REINSERT", ex.Message);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task UpdateAsyncV2_WithReinsertProcessingOptions_CompletesSuccessfully()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig();
        var step = CreateActionStep(httpVerb: HttpVerbs.PATCH);
        var resource = new Core2EnterpriseUser { Identifier = "user-005" };

        await sut.UpdateAsync(
            "<env><processingOptions>REINSERT</processingOptions><userId>user-005</userId></env>",
            resource, "eagle-app", appConfig, step, "corr-update-reinsert");
    }

    #endregion

    #region Get with Authentication Flow

    [Fact]
    public async Task GetAsync_WithAuthFlow_SetsAuthorizationHeaderOnRestRequest()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    EagleRestUserJson("u1", "Alice"), Encoding.UTF8, "application/json")
            });
        var sut = CreateSut(handler, token: "bearer-token-xyz");
        var appConfig = CreateAppConfig(authenticationFlow: new AuthenticationFlow
        {
            Steps =
            [
                CreateSoapFlowStep(
                    authenticationDetails: new { Token = "bearer-token-xyz" },
                    method: AuthenticationMethods.Bearer)
            ]
        });

        await sut.GetAsync("u1", appConfig, "corr-auth-flow-get");

        Assert.True(handler.LastHeaders.ContainsKey("Authorization"));
        Assert.Contains("bearer-token-xyz", handler.LastHeaders["Authorization"]);
    }

    [Fact]
    public async Task GetAsyncV2_WithAuthFlow_SetsAuthorizationHeaderOnRestRequest()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    EagleRestUserJson("u2", "Bob"), Encoding.UTF8, "application/json")
            });
        var sut = CreateSut(handler, token: "bearer-token-xyz");
        var appConfig = CreateAppConfig(authenticationFlow: new AuthenticationFlow
        {
            Steps =
            [
                CreateSoapFlowStep(
                    authenticationDetails: new { Token = "bearer-token-xyz" },
                    method: AuthenticationMethods.Bearer)
            ]
        });
        var step = CreateActionStep(endpoint: "https://eagle-v2.test/users", httpVerb: HttpVerbs.GET);

        await sut.GetAsync("u2", appConfig, step, "corr-auth-flow-get-v2");

        Assert.True(handler.LastHeaders.ContainsKey("Authorization"));
        Assert.Contains("bearer-token-xyz", handler.LastHeaders["Authorization"]);
    }

    #endregion

    #region Eagle taskStatusResponse Handling (golden fixtures from Postman captures)

    // Milestone A (plan-125-phase1): TDD baseline for GAP-CRT-01 / GAP-UPD-01 / GAP-CODE-01 / GAP-CODE-02.
    // The synchronous ADD/CHANGE responses captured in Postman are eag:taskStatusResponse documents
    // (status / severityCode / failedRecords) — a shape the current CheckEagleAck never validates.

    [Fact]
    public async Task ProvisionAsync_WithTaskStatusSuccess_Succeeds()
    {
        var handler = new TestHttpMessageHandler(_ =>
            XmlResponse(LoadFixture("TaskStatusResponse_Add_Success.xml")));
        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();

        var result = await sut.ProvisionAsync(
            "<env><userId>KI_PNB_VERIFY_01</userId></env>", appConfig, "corr-msa-01");

        Assert.NotNull(result);
        Assert.Equal("KI_PNB_VERIFY_01", result.Identifier);
    }

    [Fact]
    public async Task ProvisionAsync_WithTaskStatusFailure_Throws()
    {
        var handler = new TestHttpMessageHandler(_ =>
            XmlResponse(LoadFixture("TaskStatusResponse_Failure.xml")));
        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ProvisionAsync("<env><userId>KI_PNB_VERIFY_01</userId></env>", appConfig, "corr-msa-02"));

        Assert.Contains("FAILURE", ex.Message);
    }

    [Fact]
    public async Task ProvisionAsync_WithFailedRecords_Throws()
    {
        var handler = new TestHttpMessageHandler(_ =>
            XmlResponse(LoadFixture("TaskStatusResponse_FailedRecords.xml")));
        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ProvisionAsync("<env><userId>KI_PNB_VERIFY_01</userId></env>", appConfig, "corr-msa-03"));

        Assert.Contains("failedRecords", ex.Message);
    }

    [Fact]
    public async Task UpdateAsyncV2_WithTaskStatusFailure_Throws()
    {
        var handler = new TestHttpMessageHandler(_ =>
            XmlResponse(LoadFixture("TaskStatusResponse_Failure.xml")));
        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();
        var step = CreateActionStep(httpVerb: HttpVerbs.PATCH);
        var resource = new Core2EnterpriseUser { Identifier = "KI_PNB_VERIFY_01" };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.UpdateAsync("<env><userId>KI_PNB_VERIFY_01</userId></env>",
                resource, "eagle-app", appConfig, step, "corr-msa-04"));
    }

    [Fact]
    public async Task ProvisionAsync_WithSoapenvFaultAndHttp200_Throws()
    {
        var handler = new TestHttpMessageHandler(_ =>
            XmlResponse(LoadFixture("SoapFault_SoapenvPrefix.xml")));
        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            sut.ProvisionAsync("<env><userId>KI_PNB_VERIFY_01</userId></env>", appConfig, "corr-msa-05"));
    }

    #endregion

    #region Basic Authentication (Milestone D — config-only decision, proven by tests)

    // Decision 13 Jul 2026: Eagle authenticates with plain HTTP Basic on both surfaces.
    // These tests prove the existing pipeline (Basic flow step → token → Authorization header)
    // works end-to-end with no SOAP-level (WS-Security) authentication involved.

    [Fact]
    public async Task ProvisionAsync_WithBasicAuthFlow_SendsBasicAuthorizationHeaderOnSoapPost()
    {
        var basicToken = Convert.ToBase64String(Encoding.UTF8.GetBytes("soap_user:secret"));
        var handler = new TestHttpMessageHandler(_ =>
            XmlResponse(EagleAckXml(isNegative: false, correlationId: "c-basic")));
        var sut = CreateSut(handler, token: basicToken);
        var appConfig = CreateAppConfig(
            authMethodOutbound: AuthenticationMethods.Basic,
            authenticationFlow: new AuthenticationFlow
            {
                Steps =
                [
                    CreateSoapFlowStep(
                        authenticationDetails: new { Username = "soap_user", KeyVaultReference = "kv-ref" },
                        method: AuthenticationMethods.Basic)
                ]
            });

        await sut.ProvisionAsync("<env><userId>u-basic</userId></env>", appConfig, "corr-msd-01");

        Assert.NotNull(handler.LastAuthorizationHeader);
        Assert.Equal("Basic", handler.LastAuthorizationHeader!.Scheme);
        Assert.Equal(basicToken, handler.LastAuthorizationHeader.Parameter);
        Assert.DoesNotContain("wsse:Security", handler.LastRequestBody);
        Assert.DoesNotContain("UsernameToken", handler.LastRequestBody);
    }

    [Fact]
    public async Task GetAsync_WithBasicAuthFlow_SendsBasicAuthorizationHeader()
    {
        var basicToken = Convert.ToBase64String(Encoding.UTF8.GetBytes("soap_user:secret"));
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    EagleRestUserJson("u-basic", "Basic User"), Encoding.UTF8, "application/json")
            });
        var sut = CreateSut(handler, token: basicToken);
        var appConfig = CreateAppConfig(
            authMethodOutbound: AuthenticationMethods.Basic,
            authenticationFlow: new AuthenticationFlow
            {
                Steps =
                [
                    CreateSoapFlowStep(
                        authenticationDetails: new { Username = "soap_user", KeyVaultReference = "kv-ref" },
                        method: AuthenticationMethods.Basic)
                ]
            });

        var result = await sut.GetAsync("u-basic", appConfig, "corr-msd-02");

        Assert.Equal("u-basic", result.Identifier);
        Assert.NotNull(handler.LastAuthorizationHeader);
        Assert.Equal("Basic", handler.LastAuthorizationHeader!.Scheme);
        Assert.Equal(basicToken, handler.LastAuthorizationHeader.Parameter);
    }

    [Fact]
    public async Task ProvisionAsync_WhenAuthFlowResolvesNoToken_Throws()
    {
        //Regression tripwire: a configured flow that yields no token (e.g. mis-configured back to
        //WS-Security, which GetTokenListAsync skips) must fail BEFORE any unauthenticated send.
        var handler = new TestHttpMessageHandler(_ =>
            XmlResponse(EagleAckXml(isNegative: false, correlationId: "c-no-token")));
        var sut = CreateSut(handler, tokens: new Dictionary<int, string>());
        var appConfig = CreateAppConfig(
            authMethodOutbound: AuthenticationMethods.Basic,
            authenticationFlow: new AuthenticationFlow
            {
                Steps =
                [
                    CreateSoapFlowStep(
                        authenticationDetails: new { Username = "soap_user", KeyVaultReference = "kv-ref" },
                        method: AuthenticationMethods.Basic)
                ]
            });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ProvisionAsync("<env><userId>u-no-token</userId></env>", appConfig, "corr-msd-03"));

        Assert.Contains("resolved no token", ex.Message);
        Assert.Empty(handler.Requests);
    }

    #endregion

    #region Payload Placeholder Guard & Golden Request Shape (Milestone E)

    [Fact]
    public async Task MapAndPreparePayloadAsync_WithUnmappedPlaceholder_ThrowsListingTokenNames()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig();
        var step = CreateActionStep(
            template: "<env><correlationId>{{CorrelationId}}</correlationId><userId>{{userId}}</userId><email>{{emailAddress}}</email></env>");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.MapAndPreparePayloadAsync(new List<AttributeSchema>(), new Core2EnterpriseUser(), appConfig, step));

        Assert.Contains("userId", ex.Message);
        Assert.Contains("emailAddress", ex.Message);
        Assert.DoesNotContain("CorrelationId", ex.Message); // reserved placeholder is injected, never unresolved
    }

    [Fact]
    public async Task MapAndPreparePayloadAsync_WithCorrectedCreateTemplate_MatchesGoldenAddRequestStructure()
    {
        //Golden request-shape test (GAP-TEST-04 seed): the corrected Create template + mappings must
        //produce a payload structurally identical to the Postman-verified ADD request.
        var sut = CreateSut();
        var appConfig = CreateAppConfig();
        var step = CreateActionStep(template: LoadFixture("CorrectedTemplate_CreateUser.xml"));
        var schema = new List<AttributeSchema>
        {
            new() { DestinationField = "userId",       SourceValue = "UserName",                            MappingType = MappingTypes.Direct,   IsRequired = true },
            new() { DestinationField = "emailAddress", SourceValue = "ElectronicMailAddresses[0].Value",    MappingType = MappingTypes.Direct },
            new() { DestinationField = "userFullName", SourceValue = "DisplayName",                         MappingType = MappingTypes.Direct,   IsRequired = true },
            new() { DestinationField = "companyName",  SourceValue = "Eagle Investment Systems",            MappingType = MappingTypes.Constant }
        };
        var resource = new Core2EnterpriseUser
        {
            UserName = "KI_PNB_VERIFY_01",
            DisplayName = "PNB VERIFY USER 01",
            Active = true,
            ElectronicMailAddresses = new[]
            {
                new ElectronicMailAddress { Value = "pnb.verify01@pnb.com", ItemType = "work" }
            }
        };

        string payload = (string)await sut.MapAndPreparePayloadAsync(schema, resource, appConfig, step);

        Assert.DoesNotContain("{{", payload);

        var doc = new XmlDocument { XmlResolver = null };
        doc.LoadXml(payload);
        Assert.Equal("KI_PNB_VERIFY_01", doc.SelectSingleNode("//*[local-name()='userId']")!.InnerText);
        Assert.Equal("pnb.verify01@pnb.com", doc.SelectSingleNode("//*[local-name()='emailAddress']")!.InnerText);
        Assert.Equal("PNB VERIFY USER 01", doc.SelectSingleNode("//*[local-name()='userFullName']")!.InnerText);
        Assert.Equal("Eagle Investment Systems", doc.SelectSingleNode("//*[local-name()='companyName']")!.InnerText);
        Assert.Equal("U", doc.SelectSingleNode("//*[local-name()='accountState']")!.InnerText);
        Assert.True(Guid.TryParse(doc.SelectSingleNode("//*[local-name()='correlationId']")!.InnerText, out _));

        //Structural equality with the Postman ADD request: identical element names in document order
        Assert.Equal(ElementSequence(LoadFixture("AddUserRequest_Golden.xml")), ElementSequence(payload));
    }

    private static string[] ElementSequence(string xml) =>
        XDocument.Parse(xml).Descendants().Select(e => e.Name.LocalName).ToArray();

    #endregion

    #region GET Round-Trip Mapping (Phase 2 — Milestone A, GAP-GET-03)

    // Create maps Entra UserName → Eagle userId; Read must return the same identity so Entra
    // matching stays consistent. Eagle userId is the join key — email is an attribute, not identity.

    [Fact]
    public async Task GetAsync_MapsUserIdToUserName_NotEmail()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    EagleRestUserJson("KI_PNB_VERIFY_01", "PNB VERIFY USER 01", "ki.pnb.verify01@pnb.com"),
                    Encoding.UTF8, "application/json")
            });
        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();

        var result = await sut.GetAsync("KI_PNB_VERIFY_01", appConfig, "corr-p2a-01");

        Assert.Equal("KI_PNB_VERIFY_01", result.UserName);
        Assert.NotEqual("ki.pnb.verify01@pnb.com", result.UserName);
    }

    [Fact]
    public async Task GetAsync_MapsEmailAddressIntoElectronicMailAddresses()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    EagleRestUserJson("KI_PNB_VERIFY_01", "PNB VERIFY USER 01", "ki.pnb.verify01@pnb.com"),
                    Encoding.UTF8, "application/json")
            });
        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();

        var result = await sut.GetAsync("KI_PNB_VERIFY_01", appConfig, "corr-p2a-02");

        var email = Assert.Single(result.ElectronicMailAddresses);
        Assert.Equal("ki.pnb.verify01@pnb.com", email.Value);
        Assert.Equal("work", email.ItemType);
    }

    #endregion

    #region accountState Reserved Placeholder (Phase 2 — Milestone B, GAP-CRT-04)

    // {{accountState}} is a reserved, code-injected placeholder like {{CorrelationId}}:
    // SCIM active=true → "U" (enabled), active=false → "D" (Account Disabled in Eagle).
    // Entra disable-on-deprovision arrives as an update with active=false — the Update template
    // carrying {{accountState}} is what propagates the disable to Eagle.

    [Fact]
    public async Task MapAndPreparePayloadAsync_ActiveTrue_InjectsAccountStateU()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig();
        var step = CreateActionStep(
            template: "<env><accountState>{{accountState}}</accountState><userId>u1</userId></env>");
        var resource = new Core2EnterpriseUser { Identifier = "u1", Active = true };

        string payload = (string)await sut.MapAndPreparePayloadAsync(
            new List<AttributeSchema>(), resource, appConfig, step);

        Assert.Contains("<accountState>U</accountState>", payload);
    }

    [Fact]
    public async Task MapAndPreparePayloadAsync_ActiveFalse_InjectsAccountStateD()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig();
        var step = CreateActionStep(
            template: "<env><accountState>{{accountState}}</accountState><userId>u1</userId></env>");
        var resource = new Core2EnterpriseUser { Identifier = "u1", Active = false };

        string payload = (string)await sut.MapAndPreparePayloadAsync(
            new List<AttributeSchema>(), resource, appConfig, step);

        Assert.Contains("<accountState>D</accountState>", payload);
    }

    [Fact]
    public async Task MapAndPreparePayloadAsync_TemplateWithoutAccountStatePlaceholder_Unchanged()
    {
        //Backward compatibility: templates with a hardcoded accountState are left as-is.
        var sut = CreateSut();
        var appConfig = CreateAppConfig();
        var step = CreateActionStep(
            template: "<env><accountState>U</accountState><userId>u1</userId></env>");
        var resource = new Core2EnterpriseUser { Identifier = "u1", Active = false };

        string payload = (string)await sut.MapAndPreparePayloadAsync(
            new List<AttributeSchema>(), resource, appConfig, step);

        Assert.Contains("<accountState>U</accountState>", payload);
    }

    #endregion

    #region Delete Verification (Phase 2 — Milestone D, GAP-DEL-02)

    // A positive taskAcknowledgement means Eagle *accepted* the delete, not that it completed.
    // DeleteAsync now confirms absence via the Extract Service before reporting success.

    [Fact]
    public async Task DeleteAsync_WithAckAndUserGone_Succeeds()
    {
        var handler = new TestHttpMessageHandler(req =>
            req.Method == HttpMethod.Post
                ? XmlResponse(EagleAckXml(isNegative: false, correlationId: "c-del-gone"))
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();
        var step = DeleteStep();

        await sut.DeleteAsync("user-gone", "eagle-app", appConfig, step, "corr-p2d-01");

        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal(HttpMethod.Get, handler.Requests[1].Method); // one verification GET, user already gone
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task DeleteAsync_WithAckButUserStillPresent_ThrowsAfterRetries()
    {
        var handler = new TestHttpMessageHandler(req =>
            req.Method == HttpMethod.Post
                ? XmlResponse(EagleAckXml(isNegative: false, correlationId: "c-del-stuck"))
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        EagleRestUserJson("user-stuck", "Stuck User"), Encoding.UTF8, "application/json")
                });
        var sut = CreateSut(handler, fastDeleteVerify: true);
        var appConfig = CreateAppConfig();
        var step = DeleteStep();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.DeleteAsync("user-stuck", "eagle-app", appConfig, step, "corr-p2d-02"));

        Assert.Contains("user-stuck", ex.Message);
        Assert.Contains("still returned", ex.Message);
        // 1 POST + 3 verification GET attempts
        Assert.Equal(1, handler.Requests.Count(r => r.Method == HttpMethod.Post));
        Assert.Equal(3, handler.Requests.Count(r => r.Method == HttpMethod.Get));
    }

    [Fact]
    public async Task DeleteAsync_WhenNoGetEndpointConfigured_SkipsVerificationWithoutError()
    {
        var handler = new TestHttpMessageHandler(req =>
            req.Method == HttpMethod.Post
                ? XmlResponse(EagleAckXml(isNegative: false, correlationId: "c-del-noget"))
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig() with
        {
            UserURIs =
            [
                new()
                {
                    AppId   = "eagle-app",
                    BaseUrl = "https://eagle.test",
                    Post    = new Uri("https://eagle.test/EagleMLWebService20"),
                    Get     = null!
                }
            ]
        };
        var step = DeleteStep();

        await sut.DeleteAsync("user-noget", "eagle-app", appConfig, step, "corr-p2d-03");

        // Only the POST was sent; verification skipped because no GET endpoint is configured.
        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
    }

    private static ActionStep DeleteStep() =>
        CreateActionStep(
            httpVerb: HttpVerbs.DELETE,
            template: "<env><action>DELETE</action><correlationId>{{CorrelationId}}</correlationId><userId>{{Identifier}}</userId></env>",
            attributes:
            [
                new() { DestinationField = "Identifier", SourceValue = "Identifier", MappingType = MappingTypes.Direct, HttpRequestType = HttpRequestTypes.DELETE }
            ]);

    #endregion

    #region V1 Overload Error Messages (Phase 2 — Milestone E, GAP-CODE-03)

    // The V4 pipeline routes SOAPEagle through the ActionStep overloads; the V1 (non-ActionStep)
    // overloads are a legacy fallback. When their endpoint is unset, the error must point at the fix.

    [Fact]
    public async Task ProvisionAsyncV1_WhenNoPostUriConfigured_ThrowsMessageNamingActionStepPath()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig() with
        {
            UserURIs =
            [
                new() { AppId = "eagle-app", BaseUrl = "https://eagle.test", Post = null!, Get = new Uri("https://eagle.test/eagle/v2/users") }
            ]
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ProvisionAsync("<env><userId>u1</userId></env>", appConfig, "corr-p2e-01"));

        Assert.Contains("ActionStep", ex.Message);
        Assert.Contains("SOAPEagle", ex.Message);
    }

    [Fact]
    public async Task GetAsyncV1_WhenNoGetUriConfigured_ThrowsMessageNamingActionStepPath()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig() with
        {
            UserURIs =
            [
                new() { AppId = "eagle-app", BaseUrl = "https://eagle.test", Post = new Uri("https://eagle.test/EagleMLWebService20"), Get = null! }
            ]
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GetAsync("u1", appConfig, "corr-p2e-02"));

        Assert.Contains("ActionStep", ex.Message);
        Assert.Contains("SOAPEagle", ex.Message);
    }

    #endregion

    #region Test Infrastructure

    private static EagleSOAPIntegration CreateSut(TestHttpMessageHandler? handler = null, string token = "test-token", Dictionary<int, string>? tokens = null, bool fastDeleteVerify = false)
    {
        handler ??= new TestHttpMessageHandler(_ =>
            XmlResponse(EagleAckXml(isNegative: false, correlationId: "default-corr")));

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handler));

        var authContextMock = new Mock<IAuthContext>();
        authContextMock
            .Setup(ctx => ctx.GetTokenListAsync(It.IsAny<object>(), It.IsAny<SCIMDirections>()))
            .ReturnsAsync(tokens ?? new Dictionary<int, string> { { 1, token } });

        var options = Options.Create(new AppSettings());
        var configuration = new ConfigurationBuilder().Build();
        var loggerMock = new Mock<IKloudIdentityLogger>();

        return fastDeleteVerify
            ? new NoDelayEagleSOAPIntegration(
                authContextMock.Object, httpClientFactoryMock.Object, configuration, options, loggerMock.Object)
            : new EagleSOAPIntegration(
                authContextMock.Object, httpClientFactoryMock.Object, configuration, options, loggerMock.Object);
    }

    //Zero-delay delete-verification so the retry-exhaustion test does not sleep for real.
    private sealed class NoDelayEagleSOAPIntegration : EagleSOAPIntegration
    {
        public NoDelayEagleSOAPIntegration(
            IAuthContext authContext, IHttpClientFactory httpClientFactory, IConfiguration configuration,
            IOptions<AppSettings> appSettings, IKloudIdentityLogger logger)
            : base(authContext, httpClientFactory, configuration, appSettings, logger) { }

        protected override int DeleteVerifyDelayMs => 0;
    }

    private static AppConfig CreateAppConfig(
        ICollection<AttributeSchema>? schema = null,
        AuthenticationMethods authMethodOutbound = AuthenticationMethods.None,
        dynamic? authDetails = null,
        AuthenticationFlow? authenticationFlow = null)
    {
        return new AppConfig
        {
            AppId = "eagle-app",
            IntegrationMethodOutbound = IntegrationMethods.SOAP,
            AuthenticationMethodOutbound = authMethodOutbound,
            AuthenticationDetails = authDetails ?? new { },
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
            AuthenticationFlow = authenticationFlow
        };
    }

    private static AuthenticationFlowStep CreateSoapFlowStep(
        dynamic authenticationDetails,
        int stepOrder = 1,
        AuthenticationMethods method = AuthenticationMethods.SoapWsSecurity) =>
        new()
        {
            StepTitle = "SOAP Auth",
            StepOrder = stepOrder,
            AuthenticationMethod = method,
            IsRequired = true,
            AuthenticationDetails = authenticationDetails
        };

    private static ActionStep CreateActionStep(
        string endpoint = "https://eagle.test/EagleMLWebService20",
        HttpVerbs httpVerb = HttpVerbs.POST,
        string? template = null,
        ICollection<AttributeSchema>? attributes = null)
    {
        return new ActionStep
        {
            EndPoint = endpoint,
            HttpVerb = httpVerb,
            StepOrder = 1,
            IsMandatory = true,
            Template = template,
            UserAttributeSchemas = attributes
        };
    }

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

    //Mirrors Eagle's real nested REST shape: userAdministrationTransactionMessage.userAdministrationTransaction[0].user
    private static string EagleRestUserJson(string userId, string displayName, string emailAddress = "") =>
        $$$"""{"userAdministrationTransactionMessage":{"userAdministrationTransaction":[{"user":{"userId":"{{{userId}}}","userFullName":"{{{displayName}}}","emailAddress":"{{{emailAddress}}}"}}]}}""";

    private static HttpResponseMessage XmlResponse(string xml) =>
        new(HttpStatusCode.OK) { Content = new StringContent(xml, Encoding.UTF8, "text/xml") };

    private static string LoadFixture(string fileName) =>
        File.ReadAllText(System.IO.Path.Combine(
            AppContext.BaseDirectory, "SOAPIntegration", "EagleInvestment", "Fixtures", fileName));

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

        public sealed record CapturedRequest(HttpMethod Method, Uri? Uri, string Body, Dictionary<string, string> Headers);
        public List<CapturedRequest> Requests { get; } = new();

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
            Requests.Add(new CapturedRequest(request.Method, request.RequestUri, LastRequestBody, LastHeaders));

            return _responseFactory(request);
        }
    }

    #endregion
}
