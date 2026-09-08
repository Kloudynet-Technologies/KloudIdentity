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

namespace KN.KloudIdentity.MapperTests.MapperCore.PNB;

/// <summary>
/// Tests for <see cref="ASNBBoIntegration"/> — verifies the appRoleAssignments split into
/// <c>roles</c>/<c>reports</c>/<c>isChecker</c>, and the <c>hqorbranch</c>/<c>branchid</c>
/// resolution (fixed HQ id vs. branch reference API lookup).
/// </summary>
public class ASNBBoIntegrationTests
{
    private const string FormDataResponse = """
        {
            "status": 200,
            "message": "Success",
            "data": {
                "branches": [
                    { "name": "ASNBJO001-PEJABAT ASNB JOHOR BAHRU JOHOR", "code": "ASNBJO001" },
                    { "name": "ASNBKD001-PEJABAT ASNB ALOR SETAR KEDAH", "code": "ASNBKD001" },
                    { "name": "ASNBJO004 - ASNB SEGAMAT", "code": "ASNBJO004" }
                ]
            }
        }
        """;

    private static Core2EnterpriseUser MakeResource(string? hqOrBranch, string? branchKeyword, params string[] roles)
    {
        // Active defaults true here to model a normal, in-flight update — Core2EnterpriseUser.Active
        // itself defaults to false (CLR default for bool) unless a test opts out explicitly, which
        // would otherwise make every "normal update" test look like a deprovision trigger.
        var resource = new Core2EnterpriseUser { Identifier = "u1", Active = true };
        resource.KIExtension.ExtensionAttribute2 = hqOrBranch;
        resource.KIExtension.ExtensionAttribute5 = branchKeyword;
        resource.Roles = roles.Select(v => new Role { Value = v }).ToList();
        return resource;
    }

    private static AppConfig MakeAppConfig(string? createEndpoint)
    {
        var actions = createEndpoint == null
            ? new List<Mapper.Domain.Application.Action>()
            : new List<Mapper.Domain.Application.Action>
            {
                new()
                {
                    AppId = "test-app-id",
                    ActionName = ActionNames.CREATE,
                    ActionTarget = ActionTargets.USER,
                    ActionSteps = new List<ActionStep>
                    {
                        new() { StepOrder = 1, HttpVerb = HttpVerbs.POST, EndPoint = createEndpoint }
                    }
                }
            };

        return new AppConfig
        {
            AppId = "test-app-id",
            AuthenticationDetails = default!,
            IntegrationMethodOutbound = IntegrationMethods.REST,
            Actions = actions
        };
    }

    private static AppConfig MakeAppConfigWithDeleteSteps(params string[] deleteEndpoints)
    {
        var actions = new List<Mapper.Domain.Application.Action>
        {
            new()
            {
                AppId = "test-app-id",
                ActionName = ActionNames.DELETE,
                ActionTarget = ActionTargets.USER,
                ActionSteps = deleteEndpoints
                    .Select((endpoint, i) => new ActionStep
                    {
                        StepOrder = i + 1,
                        HttpVerb = HttpVerbs.DELETE,
                        EndPoint = endpoint
                    })
                    .ToList()
            }
        };

        return new AppConfig
        {
            AppId = "test-app-id",
            AuthenticationDetails = default!,
            IntegrationMethodOutbound = IntegrationMethods.REST,
            Actions = actions
        };
    }

    private static ActionStep MakeEditActionStep(int stepOrder = 1) =>
        new() { StepOrder = stepOrder, HttpVerb = HttpVerbs.PUT, EndPoint = "https://testbo.myasnb.com.my/api/v1/users/manageBo/u1" };

    private static ASNBBoIntegration CreateSut(Func<HttpRequestMessage, HttpResponseMessage>? httpHandlerFunc = null)
    {
        var mockAuthContext = new Mock<IAuthContext>();
        mockAuthContext
            .Setup(x => x.GetTokenListAsync(It.IsAny<object>(), It.IsAny<SCIMDirections>()))
            .ReturnsAsync(new Dictionary<int, string>()); // no auth steps configured — CreateHttpClientAsync adds no headers

        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken _) => httpHandlerFunc != null
                ? httpHandlerFunc(request)
                : new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(FormDataResponse) });

        var httpClient = new HttpClient(handler.Object);
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var mockConfiguration = new Mock<IConfiguration>();
        var mockLogger = new Mock<IKloudIdentityLogger>();
        var mockOptions = new Mock<IOptions<AppSettings>>();
        mockOptions.Setup(x => x.Value).Returns(new AppSettings());

        return new ASNBBoIntegration(
            mockAuthContext.Object,
            mockHttpClientFactory.Object,
            mockConfiguration.Object,
            mockLogger.Object,
            mockOptions.Object);
    }

    private static string?[] StringArray(JObject payload, string field) =>
        (payload[field] as JArray)?.Select(t => t.Value<string>()).ToArray()
        ?? throw new Xunit.Sdk.XunitException($"'{field}' was not a JArray.");

    // 1 - ROLE_-prefixed values go to "roles", everything else goes to "reports".
    [Fact]
    public async Task MapAndPreparePayload_SplitsRolesAndReports_ByRolePrefix()
    {
        var sut = CreateSut();
        var resource = MakeResource(null, null, "ROLE_PORTALADMIN_BO", "PAC01A", "PAC01R");

        var result = await sut.MapAndPreparePayloadAsync(new List<AttributeSchema>(), resource);
        var payload = (JObject)result;

        Assert.Equal(new[] { "ROLE_PORTALADMIN_BO" }, StringArray(payload, "roles"));
        Assert.Equal(new[] { "PAC01A", "PAC01R" }, StringArray(payload, "reports"));
    }

    // 1b - Duplicate report codes are preserved (not deduped/reordered by set semantics).
    [Fact]
    public async Task MapAndPreparePayload_PreservesDuplicateAndOrder_InReports()
    {
        var sut = CreateSut();
        var resource = MakeResource(null, null, "PAC01A", "PAC01A", "PAC01R", "PAC01A");

        var result = await sut.MapAndPreparePayloadAsync(new List<AttributeSchema>(), resource);
        var payload = (JObject)result;

        Assert.Equal(new[] { "PAC01A", "PAC01A", "PAC01R", "PAC01A" }, StringArray(payload, "reports"));
    }

    // 2 - Presence of ROLE_REFUND_BO flags isChecker true.
    [Fact]
    public async Task MapAndPreparePayload_IsCheckerTrue_WhenRefundBoRolePresent()
    {
        var sut = CreateSut();
        var resource = MakeResource(null, null, "ROLE_PORTALADMIN_BO", "ROLE_REFUND_BO");

        var result = await sut.MapAndPreparePayloadAsync(new List<AttributeSchema>(), resource);
        var payload = (JObject)result;

        Assert.True(payload["isChecker"]!.Value<bool>());
    }

    // 3 - Absence of ROLE_REFUND_BO flags isChecker false.
    [Fact]
    public async Task MapAndPreparePayload_IsCheckerFalse_WhenRefundBoRoleAbsent()
    {
        var sut = CreateSut();
        var resource = MakeResource(null, null, "ROLE_PORTALADMIN_BO");

        var result = await sut.MapAndPreparePayloadAsync(new List<AttributeSchema>(), resource);
        var payload = (JObject)result;

        Assert.False(payload["isChecker"]!.Value<bool>());
    }

    // 4 - No roles at all: both arrays empty, isChecker false.
    [Fact]
    public async Task MapAndPreparePayload_NoRoles_ProducesEmptyArraysAndFalseChecker()
    {
        var sut = CreateSut();
        var resource = MakeResource(null, null);

        var result = await sut.MapAndPreparePayloadAsync(new List<AttributeSchema>(), resource);
        var payload = (JObject)result;

        Assert.Empty(StringArray(payload, "roles"));
        Assert.Empty(StringArray(payload, "reports"));
        Assert.False(payload["isChecker"]!.Value<bool>());
    }

    // 4b - ExtensionAttribute4 has a value: isExpire is true.
    [Fact]
    public async Task MapAndPreparePayload_IsExpireTrue_WhenExtensionAttribute4HasValue()
    {
        var sut = CreateSut();
        var resource = MakeResource(null, null, "ROLE_PORTALADMIN_BO");
        resource.KIExtension.ExtensionAttribute4 = "2026-01-01";

        var result = await sut.MapAndPreparePayloadAsync(new List<AttributeSchema>(), resource);
        var payload = (JObject)result;

        Assert.True(payload["isExpire"]!.Value<bool>());
    }

    // 4c - ExtensionAttribute4 is empty/absent: isExpire is false.
    [Fact]
    public async Task MapAndPreparePayload_IsExpireFalse_WhenExtensionAttribute4Empty()
    {
        var sut = CreateSut();
        var resource = MakeResource(null, null, "ROLE_PORTALADMIN_BO");

        var result = await sut.MapAndPreparePayloadAsync(new List<AttributeSchema>(), resource);
        var payload = (JObject)result;

        Assert.False(payload["isExpire"]!.Value<bool>());
    }

    // 5 - HQ users get the fixed branchid without any HTTP call.
    [Fact]
    public async Task MapAndPreparePayload_HqUser_ResolvesFixedBranchId_WithoutHttpCall()
    {
        var httpCalled = false;
        var sut = CreateSut(_ => { httpCalled = true; return new HttpResponseMessage(System.Net.HttpStatusCode.OK); });
        var resource = MakeResource("HQ", null, "ROLE_PORTALADMIN_BO");
        var appConfig = MakeAppConfig(createEndpoint: null); // no CREATE action configured — must not be needed for HQ

        var result = await sut.MapAndPreparePayloadAsync(new List<AttributeSchema>(), resource, appConfig);
        var payload = (JObject)result;

        Assert.Equal("HQ", payload["hqorbranch"]!.Value<string>());
        Assert.Equal("ASNBJO001", payload["branchid"]!.Value<string>());
        Assert.False(httpCalled);
    }

    // 6 - Branch users are resolved via the reference API, matching ExtensionAttribute5 against branch name.
    [Fact]
    public async Task MapAndPreparePayload_BranchUser_ResolvesBranchId_FromReferenceApi()
    {
        var sut = CreateSut();
        var resource = MakeResource("BRANCH", "Cawangan ASNB Johor Bahru", "ROLE_PORTALADMIN_BO");
        var appConfig = MakeAppConfig("https://testbo.myasnb.com.my/api/v1/users/manageBo");

        var result = await sut.MapAndPreparePayloadAsync(new List<AttributeSchema>(), resource, appConfig);
        var payload = (JObject)result;

        Assert.Equal("BRANCH", payload["hqorbranch"]!.Value<string>());
        Assert.Equal("ASNBJO001", payload["branchid"]!.Value<string>());
    }

    // 7 - Marker is present but no branch name contains the keyword: branchid falls back to "" (no hard failure).
    [Fact]
    public async Task MapAndPreparePayload_BranchUser_EmptyBranchId_WhenNoBranchMatches()
    {
        var sut = CreateSut();
        var resource = MakeResource("BRANCH", "Cawangan ASNB Nowhereville", "ROLE_PORTALADMIN_BO");
        var appConfig = MakeAppConfig("https://testbo.myasnb.com.my/api/v1/users/manageBo");

        var result = await sut.MapAndPreparePayloadAsync(new List<AttributeSchema>(), resource, appConfig);
        var payload = (JObject)result;

        Assert.Equal(string.Empty, payload["branchid"]!.Value<string>());
    }

    // 8 - No CREATE action step configured: cannot derive the reference API base URL — still a hard failure.
    [Fact]
    public async Task MapAndPreparePayload_BranchUser_Throws_WhenNoCreateActionConfigured()
    {
        var sut = CreateSut();
        var resource = MakeResource("BRANCH", "Cawangan ASNB Johor Bahru", "ROLE_PORTALADMIN_BO");
        var appConfig = MakeAppConfig(createEndpoint: null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.MapAndPreparePayloadAsync(new List<AttributeSchema>(), resource, appConfig));
    }

    // 8b - CREATE action step endpoint is malformed (not an absolute URI): a clear
    // InvalidOperationException, not a raw UriFormatException.
    [Fact]
    public async Task MapAndPreparePayload_BranchUser_ThrowsInvalidOperationException_WhenCreateEndpointMalformed()
    {
        var sut = CreateSut();
        var resource = MakeResource("BRANCH", "Cawangan ASNB Johor Bahru", "ROLE_PORTALADMIN_BO");
        var appConfig = MakeAppConfig("not-a-valid-url");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.MapAndPreparePayloadAsync(new List<AttributeSchema>(), resource, appConfig));
    }

    // 9 - Missing branch keyword (ExtensionAttribute5) for a non-HQ user: branchid falls back to "".
    [Fact]
    public async Task MapAndPreparePayload_BranchUser_EmptyBranchId_WhenBranchKeywordMissing()
    {
        var sut = CreateSut();
        var resource = MakeResource("BRANCH", null, "ROLE_PORTALADMIN_BO");
        var appConfig = MakeAppConfig("https://testbo.myasnb.com.my/api/v1/users/manageBo");

        var result = await sut.MapAndPreparePayloadAsync(new List<AttributeSchema>(), resource, appConfig);
        var payload = (JObject)result;

        Assert.Equal(string.Empty, payload["branchid"]!.Value<string>());
    }

    // 11 - Whatever precedes "ASNB" is discarded; matching starts at the marker word.
    [Theory]
    [InlineData("Cawangan ASNB Segamat")]
    [InlineData("Branch ASNB Segamat")]
    [InlineData("Some Other Prefix ASNB Segamat")]
    public async Task MapAndPreparePayload_BranchUser_MatchesFromAsnbMarker_RegardlessOfPrefix(string branchValue)
    {
        var sut = CreateSut();
        var resource = MakeResource("BRANCH", branchValue, "ROLE_PORTALADMIN_BO");
        var appConfig = MakeAppConfig("https://testbo.myasnb.com.my/api/v1/users/manageBo");

        var result = await sut.MapAndPreparePayloadAsync(new List<AttributeSchema>(), resource, appConfig);
        var payload = (JObject)result;

        Assert.Equal("ASNBJO004", payload["branchid"]!.Value<string>());
    }

    // 12 - No "ASNB" marker present at all: nothing to match, branchid falls back to "".
    [Fact]
    public async Task MapAndPreparePayload_BranchUser_EmptyBranchId_WhenNoAsnbMarkerPresent()
    {
        var sut = CreateSut();
        var resource = MakeResource("BRANCH", "Segamat", "ROLE_PORTALADMIN_BO");
        var appConfig = MakeAppConfig("https://testbo.myasnb.com.my/api/v1/users/manageBo");

        var result = await sut.MapAndPreparePayloadAsync(new List<AttributeSchema>(), resource, appConfig);
        var payload = (JObject)result;

        Assert.Equal(string.Empty, payload["branchid"]!.Value<string>());
    }

    // 10 - Dispatch sanity check: CreateUserV4/ReplaceUserV4 hold the integration as IIntegrationBaseV2
    // and call the (schema, resource, appConfig) overload through that interface reference. Confirms
    // ASNBBoIntegration's own implementation is invoked instead of silently falling back to
    // IIntegrationBase's default interface method (which would ignore appConfig and skip branchid).
    [Fact]
    public async Task MapAndPreparePayload_ThroughInterfaceReference_ResolvesBranchId()
    {
        IIntegrationBaseV2 sut = CreateSut();
        var resource = MakeResource("HQ", null, "ROLE_PORTALADMIN_BO");
        var appConfig = MakeAppConfig(createEndpoint: null);

        var result = await sut.MapAndPreparePayloadAsync(new List<AttributeSchema>(), resource, appConfig);
        var payload = (JObject)result;

        Assert.Equal("ASNBJO001", payload["branchid"]?.Value<string>());
    }

    private static ASNBBoIntegration CreateSutCapturing(List<HttpRequestMessage> capturedRequests) =>
        CreateSut(request =>
        {
            capturedRequests.Add(request);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("{}") };
        });

    // 13 - Leave date (ExtensionAttribute4, dd/MM/yyyy) strictly in the past: update is skipped,
    // the configured DELETE action step is invoked instead.
    [Fact]
    public async Task UpdateAsync_CallsDelete_WhenLeaveDateInPast()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var sut = CreateSut(request =>
        {
            capturedRequest = request;
            capturedBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("{}") };
        });
        var resource = MakeResource(null, null, "ROLE_PORTALADMIN_BO");
        resource.KIExtension.ExtensionAttribute4 = "01/01/2020";
        var appConfig = MakeAppConfigWithDeleteSteps("https://testbo.myasnb.com.my/api/v1/users/manageBo");

        await sut.UpdateAsync(new JObject(), resource, "test-app-id", appConfig, MakeEditActionStep(), "corr-1");

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Delete, capturedRequest!.Method);
        Assert.Equal("https://testbo.myasnb.com.my/api/v1/users/manageBo", capturedRequest.RequestUri!.ToString());

        var bodyJson = JObject.Parse(capturedBody!);
        Assert.Equal("u1", bodyJson["id"]!.Value<string>());
    }

    // 13b - resource.Active is false (no leave date set): update is skipped, DELETE is invoked instead.
    [Fact]
    public async Task UpdateAsync_CallsDelete_WhenActiveIsFalse()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var sut = CreateSut(request =>
        {
            capturedRequest = request;
            capturedBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("{}") };
        });
        var resource = MakeResource(null, null, "ROLE_PORTALADMIN_BO");
        resource.Active = false;
        var appConfig = MakeAppConfigWithDeleteSteps("https://testbo.myasnb.com.my/api/v1/users/manageBo");

        await sut.UpdateAsync(new JObject(), resource, "test-app-id", appConfig, MakeEditActionStep(), "corr-1");

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Delete, capturedRequest!.Method);

        var bodyJson = JObject.Parse(capturedBody!);
        Assert.Equal("u1", bodyJson["id"]!.Value<string>());
    }

    // 14 - Leave date today: not strictly in the past, normal update proceeds.
    [Fact]
    public async Task UpdateAsync_RunsNormalUpdate_WhenLeaveDateIsToday()
    {
        var requests = new List<HttpRequestMessage>();
        var sut = CreateSutCapturing(requests);
        var resource = MakeResource(null, null, "ROLE_PORTALADMIN_BO");
        resource.KIExtension.ExtensionAttribute4 = DateTime.UtcNow.ToString(AppConstant.AsnbBoLeaveDateFormat);
        var editStep = MakeEditActionStep();

        await sut.UpdateAsync(new JObject(), resource, "test-app-id", MakeAppConfig(null), editStep, "corr-1");

        var request = Assert.Single(requests);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal(editStep.EndPoint, request.RequestUri!.ToString());
    }

    // 15 - Leave date in the future: normal update proceeds.
    [Fact]
    public async Task UpdateAsync_RunsNormalUpdate_WhenLeaveDateInFuture()
    {
        var requests = new List<HttpRequestMessage>();
        var sut = CreateSutCapturing(requests);
        var resource = MakeResource(null, null, "ROLE_PORTALADMIN_BO");
        resource.KIExtension.ExtensionAttribute4 = DateTime.UtcNow.AddDays(30).ToString(AppConstant.AsnbBoLeaveDateFormat);
        var editStep = MakeEditActionStep();

        await sut.UpdateAsync(new JObject(), resource, "test-app-id", MakeAppConfig(null), editStep, "corr-1");

        var request = Assert.Single(requests);
        Assert.Equal(HttpMethod.Put, request.Method);
    }

    // 16 - No leave date set at all: normal update proceeds.
    [Fact]
    public async Task UpdateAsync_RunsNormalUpdate_WhenExtensionAttribute4Empty()
    {
        var requests = new List<HttpRequestMessage>();
        var sut = CreateSutCapturing(requests);
        var resource = MakeResource(null, null, "ROLE_PORTALADMIN_BO");
        var editStep = MakeEditActionStep();

        await sut.UpdateAsync(new JObject(), resource, "test-app-id", MakeAppConfig(null), editStep, "corr-1");

        var request = Assert.Single(requests);
        Assert.Equal(HttpMethod.Put, request.Method);
    }

    // 17 - Leave date present but not in the configured dd/MM/yyyy format: treated as no signal,
    // normal update proceeds rather than throwing.
    [Fact]
    public async Task UpdateAsync_RunsNormalUpdate_WhenExtensionAttribute4Unparsable()
    {
        var requests = new List<HttpRequestMessage>();
        var sut = CreateSutCapturing(requests);
        var resource = MakeResource(null, null, "ROLE_PORTALADMIN_BO");
        resource.KIExtension.ExtensionAttribute4 = "2020-01-01"; // wrong format (yyyy-MM-dd, not dd/MM/yyyy)
        var editStep = MakeEditActionStep();

        await sut.UpdateAsync(new JObject(), resource, "test-app-id", MakeAppConfig(null), editStep, "corr-1");

        var request = Assert.Single(requests);
        Assert.Equal(HttpMethod.Put, request.Method);
    }

    // 18 - Multiple EDIT action steps configured for the app: once the first invocation deprovisions
    // the user, a later invocation for a subsequent step must not call DELETE again or fall through
    // to a normal update.
    [Fact]
    public async Task UpdateAsync_DoesNotRepeatDelete_WhenCalledAgainForALaterEditStep()
    {
        var requests = new List<HttpRequestMessage>();
        var sut = CreateSutCapturing(requests);
        var resource = MakeResource(null, null, "ROLE_PORTALADMIN_BO");
        resource.KIExtension.ExtensionAttribute4 = "01/01/2020";
        var appConfig = MakeAppConfigWithDeleteSteps("https://testbo.myasnb.com.my/api/v1/users/manageBo");

        await sut.UpdateAsync(new JObject(), resource, "test-app-id", appConfig, MakeEditActionStep(1), "corr-1");
        await sut.UpdateAsync(new JObject(), resource, "test-app-id", appConfig, MakeEditActionStep(2), "corr-1");

        var request = Assert.Single(requests);
        Assert.Equal(HttpMethod.Delete, request.Method);
    }

    // 19 - Leave date in the past but the app has no DELETE/USER action step configured: a clear
    // configuration-error failure rather than silently updating or silently doing nothing.
    [Fact]
    public async Task UpdateAsync_Throws_WhenLeaveDateInPastButNoDeleteStepConfigured()
    {
        var sut = CreateSut();
        var resource = MakeResource(null, null, "ROLE_PORTALADMIN_BO");
        resource.KIExtension.ExtensionAttribute4 = "01/01/2020";
        var appConfig = MakeAppConfig(createEndpoint: null); // no Actions at all

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.UpdateAsync(new JObject(), resource, "test-app-id", appConfig, MakeEditActionStep(), "corr-1"));
    }

    // 20 - DeleteAsync itself: the ASNB Bo API is a single fixed URL with no identifier in the path;
    // the identifier is sent as an { "id": "..." } JSON body on an HTTP DELETE.
    [Fact]
    public async Task DeleteAsync_SendsIdInBody_ToFixedEndpoint()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var sut = CreateSut(request =>
        {
            capturedRequest = request;
            capturedBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("{}") };
        });
        var deleteStep = new ActionStep { StepOrder = 1, HttpVerb = HttpVerbs.DELETE, EndPoint = "https://testbo.myasnb.com.my/api/v1/users/manageBo" };
        var appConfig = MakeAppConfigWithDeleteSteps("https://testbo.myasnb.com.my/api/v1/users/manageBo");

        await sut.DeleteAsync("328069", "test-app-id", appConfig, deleteStep, "corr-1");

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Delete, capturedRequest!.Method);
        Assert.Equal("https://testbo.myasnb.com.my/api/v1/users/manageBo", capturedRequest.RequestUri!.ToString());
        Assert.Equal("{\"id\":\"328069\"}", capturedBody);
    }

    // 21 - Non-DELETE HttpVerb on the action step is rejected, same guard as the base implementation.
    [Fact]
    public async Task DeleteAsync_Throws_WhenActionStepVerbIsNotDelete()
    {
        var sut = CreateSut();
        var deleteStep = new ActionStep { StepOrder = 1, HttpVerb = HttpVerbs.POST, EndPoint = "https://testbo.myasnb.com.my/api/v1/users/manageBo" };
        var appConfig = MakeAppConfigWithDeleteSteps("https://testbo.myasnb.com.my/api/v1/users/manageBo");

        await Assert.ThrowsAsync<NotSupportedException>(
            () => sut.DeleteAsync("328069", "test-app-id", appConfig, deleteStep, "corr-1"));
    }
}
