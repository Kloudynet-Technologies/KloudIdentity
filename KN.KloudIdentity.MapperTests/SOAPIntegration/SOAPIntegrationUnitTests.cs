using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.SCIM;
using Moq;
using System.Net;
using System.Net.Http;
using System.Text;

namespace KN.KloudIdentity.MapperTests.SOAPIntegration;

public class SOAPIntegrationUnitTests
{
    [Fact]
    public async Task MapAndPreparePayloadAsync_WithoutAppConfig_ThrowsNotSupportedException()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            sut.MapAndPreparePayloadAsync(new List<AttributeSchema>(), new Core2EnterpriseUser()));
    }

    [Fact]
    public async Task MapAndPreparePayloadAsync_WithAppConfigAndNoTemplate_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig(templates: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.MapAndPreparePayloadAsync(new List<AttributeSchema>(), new Core2EnterpriseUser(), appConfig));
    }

    [Fact]
    public async Task MapAndPreparePayloadAsync_WithValidTemplate_ReturnsMappedSoapPayload()
    {
        var sut = CreateSut();
        var schema = new List<AttributeSchema>
        {
            new() { DestinationField = "Identifier", SourceValue = "Identifier", MappingType = MappingTypes.Direct },
            new() { DestinationField = "UserName", SourceValue = "UserName", MappingType = MappingTypes.Direct }
        };

        var appConfig = CreateAppConfig(
            templates: new List<SOAPTemplate>
            {
                new("<Envelope><Identifier>{{Identifier}}</Identifier><UserName>{{UserName}}</UserName></Envelope>", SOAPActions.Create)
            });

        var resource = new Core2EnterpriseUser { Identifier = "ID-123", UserName = "soap.user" };

        var payload = await sut.MapAndPreparePayloadAsync(schema, resource, appConfig);

        string payloadText = Assert.IsType<string>(payload);
        Assert.Contains("<Identifier>ID-123</Identifier>", payloadText);
        Assert.Contains("<UserName>soap.user</UserName>", payloadText);
    }

    [Fact]
    public void ParseSoapUserResponse_ValidResponse_MapsCoreFields()
    {
        var sut = CreateSut();
        var responseBody = """
			<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
			  <soap:Body>
				<GetUserResponse>
				  <Identifier>U-001</Identifier>
				  <UserName>alice</UserName>
				  <DisplayName>Alice Doe</DisplayName>
				</GetUserResponse>
			  </soap:Body>
			</soap:Envelope>
			""";

        var user = sut.ParseSoapUserResponse(responseBody);

        Assert.Equal("U-001", user.Identifier);
        Assert.Equal("alice", user.UserName);
        Assert.Equal("Alice Doe", user.DisplayName);
    }

    [Fact]
    public void ParseSoapUserResponse_EmptyResponse_ThrowsArgumentException()
    {
        var sut = CreateSut();
        Assert.Throws<ArgumentException>(() => sut.ParseSoapUserResponse(""));
    }

    [Fact]
    public void ParseSoapUserResponse_InvalidXml_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        Assert.Throws<InvalidOperationException>(() => sut.ParseSoapUserResponse("<not-valid-xml"));
    }

    [Fact]
    public void ParseSoapUserResponse_NoSoapBody_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        var responseBody = "<root><body/></root>";

        Assert.Throws<InvalidOperationException>(() => sut.ParseSoapUserResponse(responseBody));
    }

    [Fact]
    public void ExtractIdentifierFromSoapResponse_WhenIdentifierMissing_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        var responseBody = """
			<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
			  <soap:Body>
				<GetUserResponse>
				  <UserName>alice</UserName>
				</GetUserResponse>
			  </soap:Body>
			</soap:Envelope>
			""";

        var appConfig = CreateAppConfig();
        Assert.Throws<InvalidOperationException>(() => sut.ExtractIdentifierFromSoapResponse(responseBody, appConfig));
    }

    [Fact]
    public void ExtractIdentifierFromSoapResponse_WhenIdentifierPresent_ReturnsIdentifier()
    {
        var sut = CreateSut();
        var responseBody = """
			<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
			  <soap:Body>
				<GetUserResponse>
				  <Identifier>U-901</Identifier>
				</GetUserResponse>
			  </soap:Body>
			</soap:Envelope>
			""";

        var appConfig = CreateAppConfig();
        var result = sut.ExtractIdentifierFromSoapResponse(responseBody, appConfig);

        Assert.Equal("U-901", result);
    }

    [Fact]
    public async Task GetAsync_WhenTemplateMissingForGet_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig(
            templates: new List<SOAPTemplate>
            {
                new("<Delete>{{Identifier}}</Delete>", SOAPActions.Delete)
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GetAsync("U-100", appConfig, "corr-1"));
    }

    [Fact]
    public async Task GetAsync_WithValidTemplateAndResponse_ReturnsMappedUser()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
					<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
					  <soap:Body>
						<GetUserResponse>
						  <Identifier>U-555</Identifier>
						  <UserName>test.soap</UserName>
						  <DisplayName>Test Soap</DisplayName>
						</GetUserResponse>
					  </soap:Body>
					</soap:Envelope>
					""", Encoding.UTF8, "text/xml")
            });

        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig(
            templates: new List<SOAPTemplate>
            {
                new("<GetUser><Identifier>{{Identifier}}</Identifier></GetUser>", SOAPActions.Get)
            },
            schema: new List<AttributeSchema>
            {
                new()
                {
                    DestinationField = "Identifier",
                    SourceValue = "Identifier",
                    MappingType = MappingTypes.Direct,
                    HttpRequestType = HttpRequestTypes.GET
                }
            });

        var result = await sut.GetAsync("U-555", appConfig, "corr-2");

        Assert.Equal("U-555", result.Identifier);
        Assert.Equal("test.soap", result.UserName);
        Assert.Equal("Test Soap", result.DisplayName);
        Assert.Contains("U-555", handler.LastRequestBody);
    }

    [Fact]
    public async Task DeleteAsync_WhenTemplateMissingForDelete_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig(
            templates: new List<SOAPTemplate>
            {
                new("<GetUser/>", SOAPActions.Get)
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.DeleteAsync("U-777", appConfig, "corr-3"));
    }

    [Fact]
    public async Task DeleteAsync_WithSoapFaultResponse_ThrowsHttpRequestException()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
					<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
					  <soap:Body>
						<soap:Fault>
						  <faultcode>soap:Server</faultcode>
						  <faultstring>Failure</faultstring>
						</soap:Fault>
					  </soap:Body>
					</soap:Envelope>
					""", Encoding.UTF8, "text/xml")
            });

        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig(
            templates: new List<SOAPTemplate>
            {
                new("<DeleteUser><Identifier>{{Identifier}}</Identifier></DeleteUser>", SOAPActions.Delete)
            },
            schema: new List<AttributeSchema>
            {
                new()
                {
                    DestinationField = "Identifier",
                    SourceValue = "Identifier",
                    MappingType = MappingTypes.Direct,
                    HttpRequestType = HttpRequestTypes.DELETE
                }
            });

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            sut.DeleteAsync("U-777", appConfig, "corr-4"));
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
            sut.ProvisionAsync("<CreateUser />", appConfig, "corr-5"));
    }

    [Fact]
    public async Task ProvisionAsync_WithSuccessfulResponse_ReturnsIdentifier()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
					<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
					  <soap:Body>
						<CreateUserResponse>
						  <Identifier>U-909</Identifier>
						</CreateUserResponse>
					  </soap:Body>
					</soap:Envelope>
					""", Encoding.UTF8, "text/xml")
            });

        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();

        var result = await sut.ProvisionAsync("<CreateUser />", appConfig, "corr-6");

        Assert.NotNull(result);
        Assert.Equal("U-909", result!.Identifier);
    }

    private static global::KN.KloudIdentity.Mapper.MapperCore.SOAPIntegration CreateSut(TestHttpMessageHandler? handler = null)
    {
        handler ??= new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
					<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
					  <soap:Body>
						<Result/>
					  </soap:Body>
					</soap:Envelope>
					""", Encoding.UTF8, "text/xml")
            });

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock
            .Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handler));

        var authContextMock = new Mock<IAuthContext>();
        authContextMock
            .Setup(context => context.GetTokenAsync(It.IsAny<object>(), It.IsAny<SCIMDirections>()))
            .Returns(Task.FromResult("test-token"));

        var options = Options.Create(new AppSettings());
        var configuration = new ConfigurationBuilder().Build();
        var loggerMock = new Mock<IKloudIdentityLogger>();

        return new global::KN.KloudIdentity.Mapper.MapperCore.SOAPIntegration(
            authContextMock.Object,
            httpClientFactoryMock.Object,
            configuration,
            options,
            loggerMock.Object);
    }

    private static AppConfig CreateAppConfig(ICollection<SOAPTemplate>? templates = null,
        ICollection<AttributeSchema>? schema = null)
    {
        return new AppConfig
        {
            AppId = "soap-app",
            IntegrationMethodOutbound = IntegrationMethods.SOAP,
            AuthenticationDetails = new { },
            UserAttributeSchemas = schema ?? new List<AttributeSchema>(),
            UserURIs = new List<UserURIs>
            {
                new()
                {
                    AppId = "soap-app",
                    BaseUrl = "https://soap.example.test",
                    Post = new Uri("https://soap.example.test/users"),
                    Get = new Uri("https://soap.example.test/users/get"),
                    Put = new Uri("https://soap.example.test/users/put"),
                    Patch = new Uri("https://soap.example.test/users/patch"),
                    Delete = new Uri("https://soap.example.test/users/delete")
                }
            },
            SOAPTemplates = templates
        };
    }

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public TestHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public string LastRequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestBody = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return _responseFactory(request);
        }
    }
}
