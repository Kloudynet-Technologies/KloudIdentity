using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.MapperCore;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

namespace KN.KloudIdentity.MapperTests.MapperCore;

/// <summary>
/// Tests for <see cref="RESTIntegrationV4.DeleteAsync(string, string, AppConfig, ActionStep, string, CancellationToken)"/>:
/// plain HTTP DELETE is unchanged, and POST/PUT/PATCH steps send the step's configured body
/// (e.g. a soft-delete "status change" endpoint).
/// </summary>
public class RESTIntegrationV4DeleteTests
{
    private const string AppId = "test-app-id";

    private static AppConfig MakeAppConfig() => new()
    {
        AppId = AppId,
        AuthenticationDetails = default!,
        IntegrationMethodOutbound = IntegrationMethods.REST
    };

    private static RESTIntegrationV4 CreateSut(
        Func<HttpRequestMessage, string?, HttpResponseMessage> respond)
    {
        var mockAuthContext = new Mock<IAuthContext>();
        mockAuthContext
            .Setup(x => x.GetTokenListAsync(It.IsAny<object>(), It.IsAny<SCIMDirections>()))
            .ReturnsAsync(new Dictionary<int, string>());

        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
                respond(request, request.Content?.ReadAsStringAsync().GetAwaiter().GetResult()));

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler.Object));

        var options = new Mock<IOptions<AppSettings>>();
        options.Setup(x => x.Value).Returns(new AppSettings());

        return new RESTIntegrationV4(
            mockAuthContext.Object,
            factory.Object,
            new Mock<IConfiguration>().Object,
            new Mock<IKloudIdentityLogger>().Object,
            options.Object);
    }

    private static HttpResponseMessage Ok() =>
        new(System.Net.HttpStatusCode.OK) { Content = new StringContent("{}") };

    private static AttributeSchema ConstantStatus(string value) => new()
    {
        AppId = AppId,
        DestinationField = "urn:kn:ki:schema:status",
        MappingType = MappingTypes.Constant,
        SourceValue = value
    };

    [Fact]
    public async Task DeleteAsync_DeleteVerb_SendsBodylessDelete()
    {
        HttpRequestMessage? captured = null;
        string? body = "unset";
        var sut = CreateSut((r, b) => { captured = r; body = b; return Ok(); });
        var step = new ActionStep { StepOrder = 1, HttpVerb = HttpVerbs.DELETE, EndPoint = "https://api.test/users/{0}" };

        await sut.DeleteAsync("u1", AppId, MakeAppConfig(), step, "corr-1");

        Assert.Equal(HttpMethod.Delete, captured!.Method);
        Assert.Equal("https://api.test/users/u1", captured.RequestUri!.ToString());
        Assert.Null(body);
    }

    [Fact]
    public async Task DeleteAsync_PostVerb_SendsConfiguredConstantBody_ToFormattedEndpoint()
    {
        HttpRequestMessage? captured = null;
        string? body = null;
        var sut = CreateSut((r, b) => { captured = r; body = b; return Ok(); });
        var step = new ActionStep
        {
            StepOrder = 1,
            HttpVerb = HttpVerbs.POST,
            EndPoint = "https://api.test/management/accounts/{0}/statusChange",
            UserAttributeSchemas = new List<AttributeSchema> { ConstantStatus("DEACTIVATED") }
        };

        await sut.DeleteAsync("jane@example.com", AppId, MakeAppConfig(), step, "corr-1");

        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Equal("https://api.test/management/accounts/jane@example.com/statusChange", captured.RequestUri!.ToString());
        Assert.Equal("{\"status\":\"DEACTIVATED\"}", body);
    }

    [Theory]
    [InlineData(HttpVerbs.PUT, "PUT")]
    [InlineData(HttpVerbs.PATCH, "PATCH")]
    public async Task DeleteAsync_PutAndPatchVerbs_AreSentWithBody(HttpVerbs verb, string expectedMethod)
    {
        HttpRequestMessage? captured = null;
        string? body = null;
        var sut = CreateSut((r, b) => { captured = r; body = b; return Ok(); });
        var step = new ActionStep
        {
            StepOrder = 1,
            HttpVerb = verb,
            EndPoint = "https://api.test/users/{0}",
            UserAttributeSchemas = new List<AttributeSchema> { ConstantStatus("DISABLED") }
        };

        await sut.DeleteAsync("u1", AppId, MakeAppConfig(), step, "corr-1");

        Assert.Equal(expectedMethod, captured!.Method.Method);
        Assert.Equal("{\"status\":\"DISABLED\"}", body);
    }

    [Fact]
    public async Task DeleteAsync_PostVerb_NonSuccessResponse_Throws()
    {
        var sut = CreateSut((_, _) => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound) { Content = new StringContent("{\"message\":\"nope\"}") });
        var step = new ActionStep
        {
            StepOrder = 1,
            HttpVerb = HttpVerbs.POST,
            EndPoint = "https://api.test/accounts/{0}/statusChange",
            UserAttributeSchemas = new List<AttributeSchema> { ConstantStatus("DEACTIVATED") }
        };

        await Assert.ThrowsAsync<HttpRequestException>(
            () => sut.DeleteAsync("u1", AppId, MakeAppConfig(), step, "corr-1"));
    }

    [Fact]
    public async Task DeleteAsync_UnsupportedVerb_Throws()
    {
        var sut = CreateSut((_, _) => Ok());
        var step = new ActionStep { StepOrder = 1, HttpVerb = HttpVerbs.GET, EndPoint = "https://api.test/users/{0}" };

        await Assert.ThrowsAsync<NotSupportedException>(
            () => sut.DeleteAsync("u1", AppId, MakeAppConfig(), step, "corr-1"));
    }
}
