using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.MapperCore;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.SCIM;
using Moq;
using Moq.Protected;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.MapperTests.MapperCore;

/// <summary>
/// Create flow for an API that returns no id field (Flagright accounts): the SCIM identifier is taken from the
/// response field named by the "Identifier" mapping row (here: email).
/// </summary>
public class RESTIntegrationV4ProvisionTests
{
    private const string AppId = "test-app-id";
    private const string Prefix = "urn:kn:ki:schema:";
    private const string Endpoint = "https://api.test/management/accounts";

    private static AppConfig MakeAppConfig() => new()
    {
        AppId = AppId,
        AuthenticationDetails = default!,
        IntegrationMethodOutbound = IntegrationMethods.REST
    };

    private static AttributeSchema Row(string destination, string source,
        HttpRequestTypes requestType = HttpRequestTypes.POST) => new()
    {
        AppId = AppId,
        DestinationField = Prefix + destination,
        DestinationType = AttributeDataTypes.String,
        MappingType = MappingTypes.Direct,
        SourceValue = source,
        HttpRequestType = requestType
    };

    // Mirrors the Flagright create step: Identifier marker first, real email row after it.
    private static ActionStep FlagrightCreateStep(bool withIdentifierRow = true,
        HttpRequestTypes requestType = HttpRequestTypes.POST)
    {
        var rows = new List<AttributeSchema>();
        if (withIdentifierRow) rows.Add(Row("email", "Identifier", requestType));
        rows.Add(Row("email", "UserName", requestType));
        rows.Add(Row("name", "DisplayName", requestType));
        rows.Add(Row("role", "KIExtension:ExtensionAttribute1", requestType));
        return new ActionStep { StepOrder = 1, HttpVerb = HttpVerbs.POST, EndPoint = Endpoint, UserAttributeSchemas = rows };
    }

    private static RESTIntegrationV4 CreateSut(string responseBody, Action<HttpRequestMessage, string?>? capture = null)
    {
        var authContext = new Mock<IAuthContext>();
        authContext
            .Setup(x => x.GetTokenListAsync(It.IsAny<object>(), It.IsAny<SCIMDirections>()))
            .ReturnsAsync(new Dictionary<int, string>());

        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
            {
                capture?.Invoke(request, request.Content?.ReadAsStringAsync().GetAwaiter().GetResult());
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(responseBody) };
            });

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler.Object));

        var configuration = new Mock<IConfiguration>();
        configuration.Setup(c => c["urnPrefix"]).Returns(Prefix);

        var options = new Mock<IOptions<AppSettings>>();
        options.Setup(x => x.Value).Returns(new AppSettings());

        return new RESTIntegrationV4(
            authContext.Object, factory.Object, configuration.Object,
            new Mock<IKloudIdentityLogger>().Object, options.Object);
    }

    private const string FlagrightResponse =
        """{"name":"Jane Doe","email":"jane@x.com","role":"Admin","status":"INVITED","createdTimestamp":1744416000000,"lastLoginTimestamp":null}""";

    [Fact]
    public async Task ProvisionAsync_ReturnsEmailFromResponse_AsIdentifier()
    {
        var sut = CreateSut(FlagrightResponse);

        var result = await sut.ProvisionAsync(new JObject(), AppId, MakeAppConfig(), FlagrightCreateStep(), "corr-1");

        Assert.Equal("jane@x.com", result!.Identifier);
    }

    [Fact]
    public async Task ProvisionAsync_Throws_WhenNoIdentifierRow_AndResponseHasNoIdLikeField()
    {
        var sut = CreateSut(FlagrightResponse);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ProvisionAsync(new JObject(), AppId, MakeAppConfig(), FlagrightCreateStep(withIdentifierRow: false), "corr-1"));
    }

    [Fact]
    public async Task ProvisionAsync_Throws_WhenResponseLacksTheIdentifierField()
    {
        var sut = CreateSut("""{"name":"Jane Doe","role":"Admin"}""");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ProvisionAsync(new JObject(), AppId, MakeAppConfig(), FlagrightCreateStep(), "corr-1"));
    }

    [Fact]
    public async Task CreateFlow_SendsRealEmailInBody_AndReturnsEmailAsIdentifier()
    {
        string? sentBody = null;
        var sut = CreateSut(FlagrightResponse, (_, body) => sentBody = body);
        var step = FlagrightCreateStep();
        var user = new Core2EnterpriseUser { Identifier = Guid.NewGuid().ToString(), UserName = "jane@x.com", DisplayName = "Jane Doe" };
        user.KIExtension.ExtensionAttribute1 = "Admin";

        // Same sequence CreateUserV4 runs: map the step's attributes, then provision.
        var payload = await sut.MapAndPreparePayloadAsync(step.UserAttributeSchemas!.ToList(), user, MakeAppConfig());
        var result = await sut.ProvisionAsync(payload, AppId, MakeAppConfig(), step, "corr-1");

        // The Identifier row comes first, so the real email row overwrites the GUID in the body.
        var body = JObject.Parse(sentBody!);
        Assert.Equal("jane@x.com", body["email"]!.Value<string>());
        Assert.Equal("Jane Doe", body["name"]!.Value<string>());
        Assert.Equal("Admin", body["role"]!.Value<string>());
        Assert.Equal("jane@x.com", result!.Identifier);
    }

    // The portal's multistep editor sets the HTTP verb on the action step, so mapping rows are saved with no
    // request type (NotDefined). The Identifier row must still be found.
    [Fact]
    public async Task ProvisionAsync_ReturnsEmailAsIdentifier_WhenRowsHaveNoRequestType()
    {
        var sut = CreateSut(FlagrightResponse);
        var step = FlagrightCreateStep(requestType: HttpRequestTypes.NotDefined);

        var result = await sut.ProvisionAsync(new JObject(), AppId, MakeAppConfig(), step, "corr-1");

        Assert.Equal("jane@x.com", result!.Identifier);
    }

    [Fact]
    public async Task ProvisionAsync_Throws_WhenUnlabelledIdentifierFieldIsMissingFromResponse()
    {
        var sut = CreateSut("""{"name":"Jane Doe","role":"Admin"}""");
        var step = FlagrightCreateStep(requestType: HttpRequestTypes.NotDefined);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ProvisionAsync(new JObject(), AppId, MakeAppConfig(), step, "corr-1"));
    }

    // The request type on a row is irrelevant for the last-resort lookup: the step carries the verb.
    [Fact]
    public async Task ProvisionAsync_FindsIdentifierRowByName_RegardlessOfItsRequestType()
    {
        var sut = CreateSut(FlagrightResponse);
        var step = FlagrightCreateStep(requestType: HttpRequestTypes.PATCH);

        var result = await sut.ProvisionAsync(new JObject(), AppId, MakeAppConfig(), step, "corr-1");

        Assert.Equal("jane@x.com", result!.Identifier);
    }

    // The mapped Identifier field wins over id-like keys in the reply (e.g. "userId").
    [Fact]
    public async Task ProvisionAsync_MappedIdentifierField_WinsOverIdLikeKeys()
    {
        var sut = CreateSut("""{"email":"jane@x.com","userId":"u-123"}""");
        var step = FlagrightCreateStep(requestType: HttpRequestTypes.NotDefined);

        var result = await sut.ProvisionAsync(new JObject(), AppId, MakeAppConfig(), step, "corr-1");

        Assert.Equal("jane@x.com", result!.Identifier);
    }

    // ---------- multistep create: only the first step's reply carries the id ----------

    private static (AppConfig Config, ActionStep Step1, ActionStep Step2) TwoStepCreate()
    {
        var step1 = FlagrightCreateStep(requestType: HttpRequestTypes.NotDefined);
        var step2 = new ActionStep
        {
            StepOrder = 2, HttpVerb = HttpVerbs.POST, EndPoint = Endpoint + "/{0}/role",
            UserAttributeSchemas = new List<AttributeSchema>
            {
                Row("email", "Identifier", HttpRequestTypes.NotDefined),
                Row("role", "KIExtension:ExtensionAttribute1", HttpRequestTypes.NotDefined)
            }
        };
        var config = MakeAppConfig();
        config.Actions = new List<KN.KloudIdentity.Mapper.Domain.Application.Action>
        {
            new() { AppId = AppId, ActionName = ActionNames.CREATE, ActionTarget = ActionTargets.USER, ActionSteps = new List<ActionStep> { step2, step1 } }
        };
        return (config, step1, step2);
    }

    [Fact]
    public async Task ProvisionAsync_FirstCreateStep_ReadsIdFromReply()
    {
        var (config, step1, _) = TwoStepCreate();
        var sut = CreateSut(FlagrightResponse);

        var result = await sut.ProvisionAsync(new JObject(), AppId, config, step1, "corr-1");

        Assert.Equal("jane@x.com", result!.Identifier);
    }

    [Theory]
    [InlineData("""{"ok":true}""")] // reply without the id field
    [InlineData("{}")]
    [InlineData("")]                // 204-style empty body
    public async Task ProvisionAsync_LaterCreateStep_ReturnsNull_SoTheFirstStepIdIsKept(string reply)
    {
        var (config, _, step2) = TwoStepCreate();
        string? url = null;
        var sut = CreateSut(reply, (r, _) => url = r.RequestUri!.ToString());

        var result = await sut.ProvisionAsync(JObject.Parse("""{"email":"jane@x.com","role":"Admin"}"""), AppId, config, step2, "corr-1");

        Assert.Null(result);
        Assert.Equal(Endpoint + "/jane@x.com/role", url); // {0} still filled from the step's payload
    }

    [Fact]
    public async Task ProvisionAsync_FirstCreateStep_EmptyReply_ThrowsClearError()
    {
        var (config, step1, _) = TwoStepCreate();
        var sut = CreateSut("");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ProvisionAsync(new JObject(), AppId, config, step1, "corr-1"));
        Assert.Contains("response is empty", ex.Message);
    }

    [Fact]
    public async Task ProvisionAsync_StepNotInConfigActions_BehavesAsSingleStep()
    {
        // App config without Actions (as in the other tests): the step is treated as the first one.
        var sut = CreateSut(FlagrightResponse);
        var lone = FlagrightCreateStep(requestType: HttpRequestTypes.NotDefined);

        var result = await sut.ProvisionAsync(new JObject(), AppId, MakeAppConfig(), lone, "corr-1");

        Assert.Equal("jane@x.com", result!.Identifier);
    }

    // A mapped Identifier missing from the reply fails the create; an id-like key is not used in its place,
    // or a wrong id would be stored and targeted by every later PATCH/DELETE.
    [Fact]
    public async Task ProvisionAsync_Throws_WhenMappedIdentifierFieldIsMissing_EvenIfReplyHasAnId()
    {
        var sut = CreateSut("""{"name":"Jane Doe","id":"acc-42"}""");
        var step = FlagrightCreateStep(requestType: HttpRequestTypes.NotDefined);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ProvisionAsync(new JObject(), AppId, MakeAppConfig(), step, "corr-1"));
    }
}
