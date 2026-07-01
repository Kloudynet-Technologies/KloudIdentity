using System.Net;
using System.Security.Authentication;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper;
using KN.KloudIdentity.Mapper.Common.Encryption;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using KN.KloudIdentity.Mapper.MapperCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.MapperTests.MapperCore.PNB;

public class ASNBKioskIntegrationTests
{
    private const string TestAppId = "test-asnb-app-id";
    private const string TestGetEndpoint = "https://kiosk-dev.myasnb.com.my/ASNBAPI4/api/services/app/KMSUser/GetAllKMSUser";
    private const string TestEncryptionKey = "TestEncryptKey00"; // 16 bytes — AES-128
    private const string TestSecretRef = "test-secret-ref";
    private const string TestUsername = "admin";
    private const string TestPassword = "123qwe";

    private readonly Mock<IAuthContext> _mockAuthContext = new();
    private readonly Mock<IKloudIdentityLogger> _mockLogger = new();
    private readonly Mock<IConfiguration> _mockConfiguration = new();
    private readonly Mock<ISecretManager> _mockSecretManager = new();

    // Produces an encrypted password + IV pair so tests can wire up matching AppConfig + SecretManager
    private (string encryptedPassword, string iv) EncryptTestPassword()
    {
        var iv = Convert.ToBase64String(new byte[16]);
        var encrypted = EncryptionHelper.Encrypt(TestPassword, TestEncryptionKey, iv);
        return (encrypted, iv);
    }

    private AppConfig BuildValidAppConfig(string getEndpoint = TestGetEndpoint)
    {
        var (_, iv) = EncryptTestPassword();

        return new AppConfig
        {
            AppId = TestAppId,
            AuthenticationDetails = default!,
            AuthenticationFlow = new AuthenticationFlow
            {
                AppId = TestAppId,
                Steps = new List<AuthenticationFlowStep>
                {
                    new AuthenticationFlowStep
                    {
                        StepTitle = "ASNB Basic Auth",
                        StepOrder = 1,
                        AuthenticationMethod = AuthenticationMethods.Basic,
                        IsRequired = true,
                        OnFailureAction = AuthOnFailureAction.None,
                        AuthenticationDetails = JObject.FromObject(new BasicAuthentication
                        {
                            Username = TestUsername,
                            AuthHeaderName = "Bearer",
                            KeyVaultReference = TestSecretRef,
                            EncryptedData = new EncryptedData { IV = iv }
                        })
                    }
                }
            },
            Actions = new List<Mapper.Domain.Application.Action>
            {
                new Mapper.Domain.Application.Action
                {
                    AppId = TestAppId,
                    ActionName = ActionNames.GET,
                    ActionTarget = ActionTargets.USER,
                    ActionSteps = new List<ActionStep>
                    {
                        new ActionStep
                        {
                            StepOrder = 1,
                            HttpVerb = HttpVerbs.GET,
                            EndPoint = getEndpoint
                        }
                    }
                }
            }
        };
    }

    private ASNBKioskIntegration CreateSut(HttpResponseMessage asnbAuthResponse)
    {
        var (encryptedPassword, _) = EncryptTestPassword();

        _mockSecretManager
            .Setup(x => x.GetSecretAsync(TestSecretRef))
            .ReturnsAsync(encryptedPassword);

        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(asnbAuthResponse);

        var httpClient = new HttpClient(handler.Object);
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        mockHttpClientFactory
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        var mockOptions = new Mock<IOptions<AppSettings>>();
        mockOptions.Setup(x => x.Value).Returns(new AppSettings { EncryptionKey = TestEncryptionKey });

        return new ASNBKioskIntegration(
            _mockAuthContext.Object,
            mockHttpClientFactory.Object,
            _mockConfiguration.Object,
            _mockLogger.Object,
            mockOptions.Object,
            _mockSecretManager.Object);
    }

    private static HttpResponseMessage AsnbSuccessResponse(string accessToken = "test-jwt-token") =>
        new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $"{{\"result\":{{\"accessToken\":\"{accessToken}\",\"expireInSeconds\":86400,\"userId\":1}}," +
                $"\"targetUrl\":null,\"success\":true,\"error\":null,\"__abp\":true}}")
        };

    // Happy path — valid credentials produce a JWT dictionary entry
    [Fact]
    public async Task GetAuthenticationAsync_ReturnsJwt_WhenCredentialsAreValid()
    {
        // Arrange
        var sut = CreateSut(AsnbSuccessResponse("test-jwt-token"));
        var appConfig = BuildValidAppConfig();

        // Act
        Dictionary<int, string> result = await sut.GetAuthenticationAsync(appConfig);

        // Assert
        Assert.True(result.ContainsKey(1));
        Assert.Equal("test-jwt-token", result[1]);
    }

    // ASNB returns success:false — must throw with the API error message
    [Fact]
    public async Task GetAuthenticationAsync_Throws_WhenSuccessIsFalse()
    {
        // Arrange
        var failResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"success\":false,\"error\":\"Invalid credentials\",\"result\":null}")
        };
        var sut = CreateSut(failResponse);
        var appConfig = BuildValidAppConfig();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AuthenticationException>(
            () => sut.GetAuthenticationAsync(appConfig));
        Assert.Contains("ASNB Kiosk authentication failed", ex.Message);
    }

    // ASNB returns success:true but no accessToken — must throw
    [Fact]
    public async Task GetAuthenticationAsync_Throws_WhenAccessTokenMissing()
    {
        // Arrange
        var noTokenResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"success\":true,\"result\":{}}")
        };
        var sut = CreateSut(noTokenResponse);
        var appConfig = BuildValidAppConfig();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AuthenticationException>(
            () => sut.GetAuthenticationAsync(appConfig));
        Assert.Contains("missing accessToken", ex.Message);
    }

    // No GET User action in appConfig.Actions — cannot derive auth URL
    [Fact]
    public async Task GetAuthenticationAsync_Throws_WhenNoGetActionConfigured()
    {
        // Arrange
        var sut = CreateSut(AsnbSuccessResponse());
        var appConfig = BuildValidAppConfig() with
        {
            Actions = new List<Mapper.Domain.Application.Action>()
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.GetAuthenticationAsync(appConfig));
        Assert.Contains("No User action step endpoint", ex.Message);
    }

    //AuthenticationFlow has no Basic step — must throw before any HTTP call
    [Fact]
    public async Task GetAuthenticationAsync_Throws_WhenNoBasicStepInFlow()
    {
        // Arrange
        var sut = CreateSut(AsnbSuccessResponse());
        var appConfig = BuildValidAppConfig() with
        {
            AuthenticationFlow = new AuthenticationFlow
            {
                Steps = new List<AuthenticationFlowStep>
                {
                    new AuthenticationFlowStep
                    {
                        StepTitle = "Bearer Only",
                        StepOrder = 1,
                        AuthenticationMethod = AuthenticationMethods.Bearer,
                        IsRequired = true,
                        OnFailureAction = AuthOnFailureAction.None,
                        AuthenticationDetails = JObject.FromObject(new { token = "irrelevant" })
                    }
                }
            }
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AuthenticationException>(
            () => sut.GetAuthenticationAsync(appConfig));
        Assert.Contains("No Basic authentication step", ex.Message);
    }

    // Verifies BuildAuthUrl produces the correct URL from the GET endpoint
    [Fact]
    public async Task GetAuthenticationAsync_CallsCorrectAuthUrl()
    {
        // Arrange
        string? capturedUrl = null;

        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(
                (req, _) => capturedUrl = req.RequestUri?.ToString())
            .ReturnsAsync(AsnbSuccessResponse());

        var httpClient = new HttpClient(handler.Object);
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var (encryptedPassword, _) = EncryptTestPassword();
        _mockSecretManager.Setup(x => x.GetSecretAsync(TestSecretRef)).ReturnsAsync(encryptedPassword);

        var mockOptions = new Mock<IOptions<AppSettings>>();
        mockOptions.Setup(x => x.Value).Returns(new AppSettings { EncryptionKey = TestEncryptionKey });

        var sut = new ASNBKioskIntegration(
            _mockAuthContext.Object,
            mockHttpClientFactory.Object,
            _mockConfiguration.Object,
            _mockLogger.Object,
            mockOptions.Object,
            _mockSecretManager.Object);

        var appConfig = BuildValidAppConfig();

        // Act
        await sut.GetAuthenticationAsync(appConfig);

        // Assert
        Assert.NotNull(capturedUrl);
        Assert.Equal(
            "https://kiosk-dev.myasnb.com.my/ASNBAPI4/api/TokenAuth/Authenticate",
            capturedUrl);
    }

    // GET endpoint missing '/api' segment — BuildAuthUrl must throw clearly
    [Fact]
    public async Task GetAuthenticationAsync_Throws_WhenGetEndpointHasNoApiSegment()
    {
        // Arrange
        var sut = CreateSut(AsnbSuccessResponse());
        var appConfig = BuildValidAppConfig(
            getEndpoint: "https://kiosk-dev.myasnb.com.my/ASNBAPI4/services/users");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.GetAuthenticationAsync(appConfig));
        Assert.Contains("'/api' segment not found", ex.Message);
    }

    // BasicAuthentication missing KeyVaultReference — must throw clear AuthenticationException
    [Fact]
    public async Task GetAuthenticationAsync_Throws_WhenKeyVaultReferenceIsMissing()
    {
        // Arrange
        var sut = CreateSut(AsnbSuccessResponse());
        var iv = Convert.ToBase64String(new byte[16]);
        var appConfig = BuildValidAppConfig() with
        {
            AuthenticationFlow = new AuthenticationFlow
            {
                AppId = TestAppId,
                Steps = new List<AuthenticationFlowStep>
                {
                    new AuthenticationFlowStep
                    {
                        StepTitle = "ASNB Basic Auth",
                        StepOrder = 1,
                        AuthenticationMethod = AuthenticationMethods.Basic,
                        IsRequired = true,
                        OnFailureAction = AuthOnFailureAction.None,
                        AuthenticationDetails = JObject.FromObject(new BasicAuthentication
                        {
                            Username = TestUsername,
                            AuthHeaderName = "Bearer",
                            KeyVaultReference = null,
                            EncryptedData = new EncryptedData { IV = iv }
                        })
                    }
                }
            }
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AuthenticationException>(
            () => sut.GetAuthenticationAsync(appConfig));
        Assert.Contains("KeyVaultReference is required", ex.Message);
    }

    // BasicAuthentication missing EncryptedData.IV — must throw clear AuthenticationException
    [Fact]
    public async Task GetAuthenticationAsync_Throws_WhenEncryptedDataIVIsMissing()
    {
        // Arrange
        var sut = CreateSut(AsnbSuccessResponse());
        var appConfig = BuildValidAppConfig() with
        {
            AuthenticationFlow = new AuthenticationFlow
            {
                AppId = TestAppId,
                Steps = new List<AuthenticationFlowStep>
                {
                    new AuthenticationFlowStep
                    {
                        StepTitle = "ASNB Basic Auth",
                        StepOrder = 1,
                        AuthenticationMethod = AuthenticationMethods.Basic,
                        IsRequired = true,
                        OnFailureAction = AuthOnFailureAction.None,
                        AuthenticationDetails = JObject.FromObject(new BasicAuthentication
                        {
                            Username = TestUsername,
                            AuthHeaderName = "Bearer",
                            KeyVaultReference = TestSecretRef,
                            EncryptedData = null
                        })
                    }
                }
            }
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AuthenticationException>(
            () => sut.GetAuthenticationAsync(appConfig));
        Assert.Contains("EncryptedData.IV is required", ex.Message);
    }

    // Auth endpoint returns non-2xx — must throw AuthenticationException with status code and body
    [Fact]
    public async Task GetAuthenticationAsync_Throws_WhenAuthEndpointReturnsNonSuccess()
    {
        // Arrange
        var unauthorizedResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"message\":\"Invalid credentials\"}")
        };
        var sut = CreateSut(unauthorizedResponse);
        var appConfig = BuildValidAppConfig();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AuthenticationException>(
            () => sut.GetAuthenticationAsync(appConfig));
        Assert.Contains("ASNB Kiosk auth HTTP call failed", ex.Message);
        Assert.Contains("Unauthorized", ex.Message);
    }
}
