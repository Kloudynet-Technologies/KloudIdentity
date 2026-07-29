//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using KN.KloudIdentity.Mapper.Domain.ExternalEndpoint;
using KN.KloudIdentity.Mapper.MapperCore.Outbound.CustomLogic;
using Moq;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Text;
using Xunit;

namespace KN.KloudIdentity.MapperTests.Outbound.CustomLogic;


// Implementation plan: plan-131-outboundPayloadProcessor-xml-support.
 // Covers JSON (including default), XML, None, and non-success status handling to protect backward compatibility.
 // These tests verify request content-type/body and response handling for each RequestBodyType.
public class OutboundPayloadProcessorTests
{
    private const string Url = "https://logic.test/enrich";

    #region JSON path (locks current behaviour — green)

    [Fact]
    public async Task ProcessAsync_JsonBodyType_PostsJson_AndDeserializesResponse()
    {
        var handler = new CapturingHandler(_ => Json("""{"result":"ok"}"""));
        var sut = CreateSut(handler);
        var endpoint = Endpoint(RequestBodyType.Json);

        var result = await sut.ProcessAsync(JObject.Parse("""{"userId":"u1"}"""), endpoint, "corr-json-01", CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal(Url, handler.LastUri?.ToString());
        Assert.Equal("application/json", handler.LastMediaType);
        Assert.True(handler.LastHeaders.ContainsKey("X-Correlation-ID"));
        Assert.Equal("ok", (string)result.result);
    }

    [Fact]
    public async Task ProcessAsync_DefaultBodyType_BehavesAsJson()
    {
        var handler = new CapturingHandler(_ => Json("""{"result":"ok"}"""));
        var sut = CreateSut(handler);
        // RequestBodyType omitted → record default is Json
        var endpoint = new ExternalEndpointInfo
        {
            Id = Guid.NewGuid(),
            AppId = "app1",
            EndpointUrl = Url,
            AuthenticationMethod = AuthenticationMethods.None
        };

        var result = await sut.ProcessAsync(JObject.Parse("""{"userId":"u1"}"""), endpoint, "corr-json-default", CancellationToken.None);

        Assert.Equal("application/json", handler.LastMediaType);
        Assert.Equal("ok", (string)result.result);
    }

    [Fact]
    public async Task ProcessAsync_NonSuccessStatus_ThrowsHttpRequestException()
    {
        var handler = new CapturingHandler(_ => Json("bad", HttpStatusCode.BadRequest));
        var sut = CreateSut(handler);
        var endpoint = Endpoint(RequestBodyType.Json);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            (Task)sut.ProcessAsync(JObject.Parse("""{"userId":"u1"}"""), endpoint, "corr-json-fail", CancellationToken.None));
    }

    #endregion

    #region XML path (red until Milestone C)

    [Fact]
    public async Task ProcessAsync_XmlBodyType_PostsTextXml_AndReturnsRawXmlString()
    {
        const string requestXml = "<Envelope><Body><user>u1</user></Body></Envelope>";
        const string responseXml = "<Envelope><Body><enriched>true</enriched></Body></Envelope>";
        var handler = new CapturingHandler(_ => Xml(responseXml));
        var sut = CreateSut(handler);
        var endpoint = Endpoint(RequestBodyType.Xml);

        var result = await sut.ProcessAsync(requestXml, endpoint, "corr-xml-01", CancellationToken.None);

        Assert.Equal("text/xml", handler.LastMediaType);
        Assert.Equal(requestXml, handler.LastBody);
        Assert.Equal(responseXml, (string)result); // raw XML string, NOT JSON-deserialized
    }

    [Fact]
    public async Task ProcessAsync_XmlBodyType_WithNonStringPayload_Throws()
    {
        var handler = new CapturingHandler(_ => Xml("<ok/>"));
        var sut = CreateSut(handler);
        var endpoint = Endpoint(RequestBodyType.Xml);

        // XML path requires a string payload; a JObject must be rejected before any HTTP call.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            (Task)sut.ProcessAsync(JObject.Parse("""{"userId":"u1"}"""), endpoint, "corr-xml-guard", CancellationToken.None));
        Assert.Equal(0, handler.CallCount);
    }

    #endregion

    #region None body type (red until Milestone C)

    [Fact]
    public async Task ProcessAsync_NoneBodyType_ThrowsNotSupported()
    {
        var handler = new CapturingHandler(_ => Json("""{"result":"ok"}"""));
        var sut = CreateSut(handler);
        var endpoint = Endpoint(RequestBodyType.None);

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            (Task)sut.ProcessAsync(JObject.Parse("""{"userId":"u1"}"""), endpoint, "corr-none", CancellationToken.None));
        Assert.Equal(0, handler.CallCount);
    }

    #endregion

    #region Test Infrastructure

    private static OutboundPayloadProcessor CreateSut(CapturingHandler handler)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));

        var logger = new Mock<IKloudIdentityLogger>();
        return new OutboundPayloadProcessor(factory.Object, logger.Object);
    }

    private static ExternalEndpointInfo Endpoint(RequestBodyType bodyType) => new()
    {
        Id = Guid.NewGuid(),
        AppId = "app1",
        EndpointUrl = Url,
        AuthenticationMethod = AuthenticationMethods.None,
        RequestBodyType = bodyType
    };

    private static HttpResponseMessage Json(string content, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(content, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Xml(string content, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(content, Encoding.UTF8, "text/xml") };

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _factory;
        public CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> factory) => _factory = factory;

        public int CallCount { get; private set; }
        public HttpMethod? LastMethod { get; private set; }
        public Uri? LastUri { get; private set; }
        public string? LastMediaType { get; private set; }
        public string LastBody { get; private set; } = string.Empty;
        public Dictionary<string, string> LastHeaders { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastMethod = request.Method;
            LastUri = request.RequestUri;
            LastMediaType = request.Content?.Headers?.ContentType?.MediaType;
            LastBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            LastHeaders = request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);
            return _factory(request);
        }
    }

    #endregion
}
