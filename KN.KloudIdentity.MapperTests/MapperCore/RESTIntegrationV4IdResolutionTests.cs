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
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.MapperTests.MapperCore;

/// <summary>
/// How <see cref="RESTIntegrationV4"/> decides the SCIM identifier after a create call:
///   1. the field named by the "Identifier" mapping row (any request type - the action step carries the verb);
///      if that field is missing the create fails - a mapping is never second-guessed,
///   2. with no Identifier row, a well-known id key (login, id, identifier, key, userKey, user_id, userId),
///      top level first, then at any depth,
///   3. otherwise the create fails.
/// The cases below model real applications: some map the id, some only return it, some return none.
/// </summary>
public class RESTIntegrationV4IdResolutionTests
{
    private const string AppId = "app-1";
    private const string Prefix = "urn:kn:ki:schema:";
    private const string Endpoint = "https://api.test/users";

    private static AppConfig MakeAppConfig() => new()
    {
        AppId = AppId,
        AuthenticationDetails = default!,
        IntegrationMethodOutbound = IntegrationMethods.REST
    };

    private static AttributeSchema Row(string destination, string source,
        HttpRequestTypes requestType = HttpRequestTypes.NotDefined) => new()
    {
        AppId = AppId,
        DestinationField = Prefix + destination,
        DestinationType = AttributeDataTypes.String,
        MappingType = MappingTypes.Direct,
        SourceValue = source,
        HttpRequestType = requestType
    };

    private static ActionStep Step(string endpoint, HttpVerbs verb, params AttributeSchema[] rows) => new()
    {
        StepOrder = 1, HttpVerb = verb, EndPoint = endpoint, UserAttributeSchemas = rows.ToList()
    };

    private static ActionStep CreateStep(params AttributeSchema[] rows) => Step(Endpoint, HttpVerbs.POST, rows);

    private static RESTIntegrationV4 CreateSut(string responseBody, Action<HttpRequestMessage>? capture = null)
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
                capture?.Invoke(request);
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(responseBody) };
            });

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler.Object));

        var configuration = new Mock<IConfiguration>();
        configuration.Setup(c => c["urnPrefix"]).Returns(Prefix);

        var options = new Mock<IOptions<AppSettings>>();
        options.Setup(x => x.Value).Returns(new AppSettings());

        return new RESTIntegrationV4(authContext.Object, factory.Object, configuration.Object,
            new Mock<IKloudIdentityLogger>().Object, options.Object);
    }

    private static async Task<string?> ResolveIdAsync(string response, ActionStep step, JObject? payload = null)
    {
        var sut = CreateSut(response);
        var user = await sut.ProvisionAsync(payload ?? new JObject(), AppId, MakeAppConfig(), step, "corr-1");
        return user!.Identifier;
    }

    // ---------- 1. the application maps the Identifier ----------

    [Theory]
    [InlineData(HttpRequestTypes.NotDefined)] // portal multistep rows: no request type
    [InlineData(HttpRequestTypes.POST)]       // classic rows labelled POST
    [InlineData(HttpRequestTypes.PUT)]
    [InlineData(HttpRequestTypes.PATCH)]
    public async Task Mapped_IdentifierRow_IsUsed_WhateverItsRequestType(HttpRequestTypes requestType)
    {
        var step = CreateStep(Row("email", "Identifier", requestType));

        Assert.Equal("jane@x.com", await ResolveIdAsync("""{"email":"jane@x.com","name":"Jane"}""", step));
    }

    [Fact]
    public async Task Mapped_Flagright_ReplyHasNoIdField_IdentifierIsTheEmail()
    {
        var step = CreateStep(Row("email", "Identifier"), Row("email", "UserName"), Row("name", "DisplayName"));
        var reply = """{"name":"Jane Doe","email":"jane@x.com","role":"Admin","status":"INVITED","createdTimestamp":1744416000000,"lastLoginTimestamp":null}""";

        Assert.Equal("jane@x.com", await ResolveIdAsync(reply, step));
    }

    [Fact]
    public async Task Mapped_NestedResponseField_IsResolvedByPath()
    {
        var step = CreateStep(Row("data.account.code", "Identifier"));

        Assert.Equal("ACC-7", await ResolveIdAsync("""{"status":"ok","data":{"account":{"code":"ACC-7"}}}""", step));
    }

    [Fact]
    public async Task Mapped_NumericField_IsReturnedAsText()
    {
        var step = CreateStep(Row("accountNumber", "Identifier"));

        Assert.Equal("100234", await ResolveIdAsync("""{"accountNumber":100234}""", step));
    }

    [Fact]
    public async Task Mapped_IdentifierRowInsideChildSchemas_IsFound()
    {
        var parent = Row("profile", "Profile") with { ChildSchemas = new List<AttributeSchema> { Row("employeeCode", "Identifier") } };
        var step = CreateStep(parent);

        Assert.Equal("E-55", await ResolveIdAsync("""{"employeeCode":"E-55"}""", step));
    }

    [Fact]
    public async Task Mapped_Field_WinsOver_IdLikeKeysInTheReply()
    {
        var step = CreateStep(Row("email", "Identifier"));

        Assert.Equal("jane@x.com", await ResolveIdAsync("""{"id":99,"login":"jdoe","email":"jane@x.com"}""", step));
    }

    [Fact]
    public async Task Mapped_ButFieldMissingFromReply_Throws_EvenWhenAnIdLikeKeyExists()
    {
        // A wrong mapping must fail loudly, not store some other id (here a transaction id) as the user's.
        var step = CreateStep(Row("data.userId", "Identifier"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ResolveIdAsync("""{"id":"txn-1","data":{"user_id":"u-9"}}""", step));
        Assert.Contains("'data.userId' not found in the response", ex.Message);
    }

    [Fact]
    public async Task Mapped_FieldIsNullInReply_Throws()
    {
        var step = CreateStep(Row("email", "Identifier"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ResolveIdAsync("""{"email":null,"id":"acc-42"}""", step));
    }

    [Fact]
    public async Task Mapped_ButFieldMissingFromReply_AndNoIdLikeKey_Throws()
    {
        var step = CreateStep(Row("email", "Identifier"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ResolveIdAsync("""{"name":"Jane"}""", step));
        Assert.Contains("not found in the response", ex.Message);
    }

    // ---------- 2. the application only returns an id (no Identifier row) ----------

    [Theory]
    [InlineData("""{"id":"u-1","name":"Jane"}""", "u-1")]                                  // generic REST: id
    [InlineData("""{"id":12345}""", "12345")]                                              // numeric id
    [InlineData("""{"login":"octocat","name":"Octo"}""", "octocat")]                       // login
    [InlineData("""{"identifier":"IDN-9"}""", "IDN-9")]                                    // identifier
    [InlineData("""{"key":"JIRAUSER10000","name":"Jane"}""", "JIRAUSER10000")]             // Jira-style key
    [InlineData("""{"userKey":"K-77","userName":"jane"}""", "K-77")]                       // userKey
    [InlineData("""{"user_id":"snake-5"}""", "snake-5")]                                   // user_id
    [InlineData("""{"userId":"camel-6"}""", "camel-6")]                                    // userId
    public async Task ResponseOnly_WellKnownKey_IsUsedAsIdentifier(string reply, string expected)
    {
        var step = CreateStep(Row("userName", "UserName"));

        Assert.Equal(expected, await ResolveIdAsync(reply, step));
    }

    [Fact]
    public async Task ResponseOnly_IdIsNestedInDataArray_IsFound()
    {
        // Navitaire-like envelope: {"data":[{"userKey":"...", "username":"..."}]}
        var step = CreateStep(Row("username", "UserName"));
        var reply = """{"data":[{"userKey":"NAV-001","username":"jane"}],"errors":[]}""";

        Assert.Equal("NAV-001", await ResolveIdAsync(reply, step));
    }

    [Fact]
    public async Task ResponseOnly_Login_IsPreferredOverId_ByKeyOrder()
    {
        var step = CreateStep(Row("userName", "UserName"));

        Assert.Equal("octocat", await ResolveIdAsync("""{"id":1,"login":"octocat"}""", step));
    }

    [Fact]
    public async Task ResponseOnly_TopLevelId_WinsOverNestedIds()
    {
        var step = CreateStep(Row("userName", "UserName"));

        Assert.Equal("u-1", await ResolveIdAsync("""{"organization":{"id":"org-5"},"id":"u-1"}""", step));
    }

    [Fact]
    public async Task ResponseOnly_KeyOccursSeveralTimes_DoesNotThrow()
    {
        var step = CreateStep(Row("userName", "UserName"));

        Assert.Equal("1", await ResolveIdAsync("""{"id":"1","roles":[{"id":"r"}]}""", step));
        Assert.Equal("a", await ResolveIdAsync("""{"items":[{"id":"a"},{"id":"b"}]}""", step));
    }

    [Fact]
    public async Task ResponseOnly_NoRowAtAll_StillResolves()
    {
        var step = CreateStep(); // a step with no mapping rows

        Assert.Equal("u-1", await ResolveIdAsync("""{"id":"u-1"}""", step));
    }

    // ---------- 3. the application returns no usable id ----------

    [Fact]
    public async Task NoRow_AndNoKnownKey_Throws()
    {
        var step = CreateStep(Row("userName", "UserName"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ResolveIdAsync("""{"name":"Jane","email":"jane@x.com"}""", step));
        Assert.Contains("no fallback found", ex.Message);
    }

    [Fact]
    public async Task NoRow_KeyNamesAreCaseSensitive_UpperCaseIdIsNotMatched()
    {
        var step = CreateStep(Row("userName", "UserName"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ResolveIdAsync("""{"ID":"u-1","UserId":"u-2"}""", step));
    }

    [Fact]
    public async Task NoRow_ButMappingTheEmailFixesIt()
    {
        var reply = """{"role":{"id":7},"email":"jane@x.com"}""";

        Assert.Equal("jane@x.com", await ResolveIdAsync(reply, CreateStep(Row("email", "Identifier"))));
    }

    [Theory]
    [InlineData("")]               // 201 with an empty body
    [InlineData("created")]        // plain-text body
    public async Task ReplyThatIsNotJson_FailsTheCreate(string reply)
    {
        var step = CreateStep(Row("email", "Identifier"));

        await Assert.ThrowsAnyAsync<Exception>(() => ResolveIdAsync(reply, step));
    }

    // ---------- 4. the id is needed before the call (endpoint contains {0}) ----------

    [Fact]
    public async Task PutEndpointWithPlaceholder_UsesIdFromThePayload()
    {
        HttpRequestMessage? sent = null;
        var sut = CreateSut("""{"userName":"jane","status":"created"}""", r => sent = r);
        var step = Step("https://api.test/users/{0}", HttpVerbs.PUT, Row("userName", "Identifier"));

        var user = await sut.ProvisionAsync(new JObject { ["userName"] = "jane" }, AppId, MakeAppConfig(), step, "corr-1");

        Assert.Equal("https://api.test/users/jane", sent!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Put, sent.Method);
        Assert.Equal("jane", user!.Identifier);
    }

    [Fact]
    public async Task PutEndpointWithPlaceholder_NoIdentifierInPayload_FailsBeforeSending()
    {
        var calls = 0;
        var sut = CreateSut("{}", _ => calls++);
        var step = Step("https://api.test/users/{0}", HttpVerbs.PUT, Row("userName", "UserName"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ProvisionAsync(new JObject { ["userName"] = "jane" }, AppId, MakeAppConfig(), step, "corr-1"));
        Assert.Equal(0, calls);
    }
}
