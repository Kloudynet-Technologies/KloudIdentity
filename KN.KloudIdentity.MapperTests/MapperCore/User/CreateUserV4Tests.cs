

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KN.KloudIdentity.Mapper.MapperCore.User;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.MapperCore;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using KN.KloudIdentity.Mapper.MapperCore.Outbound.CustomLogic;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SCIM;
using Moq;
using Xunit;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Domain;

namespace KN.KloudIdentity.MapperTests.MapperCore.User;

public class CreateUserV4Tests
{
    private readonly Mock<IGetFullAppConfigQuery> _mockGetFullAppConfigQuery = new();
    private readonly Mock<IIntegrationBaseFactory> _mockIntegrationFactory = new();
    private readonly Mock<IOutboundPayloadProcessor> _mockOutboundPayloadProcessor = new();
    private readonly Mock<IKloudIdentityLogger> _mockLogger = new();
    private readonly Mock<IOptions<AppSettings>> _mockOptions = new();
    private readonly Mock<IReplaceResourceV2> _mockReplaceResourceV2 = new();
    private readonly Mock<IServiceProvider> _mockServiceProvider = new();
    private readonly Mock<IAzureStorageManager> _mockAzureStorageManager = new();

    private readonly AppSettings _appSettings = new AppSettings
    {
        UserMigration = new UserMigrationOptions
        {
            AppFeatureEnabledMap = new Dictionary<string, bool> { { "app1", true }, { "app2", false } },
            AppCorrelationPropertyMap = new Dictionary<string, string> { { "app1", "UserName" } }
        }
    };

    private CreateUserV4 CreateSut(bool withAzureStorageManager = false)
    {
        _mockOptions.Setup(x => x.Value).Returns(_appSettings);
        _mockAzureStorageManager.Setup(x => x.GetUserMigrationDataAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(new UserMigrationData("partition", "migratedId", "testuser"));
        if (withAzureStorageManager)
        {
            _mockServiceProvider.Setup(x => x.GetService(typeof(IAzureStorageManager))).Returns(_mockAzureStorageManager.Object);
        }
        else
        {
            _mockServiceProvider.Setup(x => x.GetService(typeof(IAzureStorageManager))).Returns(null);
        }

        var mockIntegration = new Mock<IIntegrationBaseV2>();
        mockIntegration
            .Setup(x => x.ValidatePayloadAsync(It.IsAny<object>(), It.IsAny<AppConfig>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((object obj, AppConfig config, string s, CancellationToken ct) => Task.FromResult<(bool, string[])>((true, Array.Empty<string>())));

        _mockIntegrationFactory
            .Setup(x => x.GetIntegration(It.IsAny<IntegrationMethods>(), It.IsAny<string>()))
            .Returns(mockIntegration.Object);

        return new CreateUserV4(
            _mockGetFullAppConfigQuery.Object,
            _mockIntegrationFactory.Object,
            _mockOutboundPayloadProcessor.Object,
            _mockLogger.Object,
            _mockOptions.Object,
            _mockReplaceResourceV2.Object,
            _mockServiceProvider.Object
        );
    }

    [Fact]
    public async Task ExecuteAsync_REST_Flow_UserMigrationDisabled_CallsMultistep()
    {
        // Arrange
        var appId = "app1";
        var correlationId = "corr-123";
        var user = new Core2EnterpriseUser() { UserName = "testuser" };
        var appConfig = new AppConfig
        {
            AppId = appId,
            AppName = "TestApp",
            IsEnabled = true,
            IntegrationMethodOutbound = IntegrationMethods.REST,
            Actions = new List<Mapper.Domain.Application.Action>
            {
                new Mapper.Domain.Application.Action
                {
                    AppId = appId,
                    ActionName = ActionNames.CREATE,
                    ActionTarget = ActionTargets.USER,
                    ActionSteps = new List<ActionStep>
                    {
                        new ActionStep
                        {
                            ActionId = 1,
                            StepOrder = 1,
                            HttpVerb = HttpVerbs.POST,
                            EndPoint = "/users",
                            IsMandatory = true
                        },
                        new ActionStep
                        {
                            ActionId = 1,
                            StepOrder = 2,
                            HttpVerb = HttpVerbs.PUT,
                            EndPoint = "/users/{0}/confirm",
                            IsMandatory = true
                        }
                    }
                }
            },
            AuthenticationDetails = new { }
        };

        _mockGetFullAppConfigQuery.Setup(x => x.GetAsync(appId, It.IsAny<System.Threading.CancellationToken>())).ReturnsAsync(appConfig);
        _mockOptions.Setup(x => x.Value).Returns(new AppSettings
        {
            UserMigration = new UserMigrationOptions
            {
                AppFeatureEnabledMap = new Dictionary<string, bool> { { appId, false } }
            }
        });
        var sut = CreateSut(true);
        // Set _appConfig field via reflection
        var appConfigField = typeof(CreateUserV4).GetField("_appConfig", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (appConfigField != null) appConfigField.SetValue(sut, appConfig);
        // Act
        var result = await sut.ExecuteAsync(user, appId, correlationId);
        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.UserName, result.UserName);
        Assert.Equal(user.Identifier, result.Identifier);
    }

    [Fact]
    public async Task ExecuteAsync_NonREST_Flow_UserMigrationDisabled_CallsGenericLogic()
    {
        // Arrange
        var appId = "app2";
        var correlationId = "corr-123";
        var user = new Core2EnterpriseUser() { UserName = "testuser" };
        var appConfig = new AppConfig
        {
            AppId = appId,
            AppName = "TestApp",
            IsEnabled = true,
            IntegrationMethodOutbound = IntegrationMethods.SQL,
            UserURIs = new List<UserURIs> { new UserURIs { AppId = appId, BaseUrl = "http://localhost", Post = new Uri("http://localhost/post"), Get = new Uri("http://localhost/get") } },
            AuthenticationDetails = new { },
            UserAttributeSchemas = new List<KN.KloudIdentity.Mapper.Domain.Mapping.AttributeSchema>()
        };
        _mockGetFullAppConfigQuery.Setup(x => x.GetAsync(appId, It.IsAny<System.Threading.CancellationToken>())).ReturnsAsync(appConfig);
        _mockOptions.Setup(x => x.Value).Returns(new AppSettings
        {
            UserMigration = new UserMigrationOptions
            {
                AppFeatureEnabledMap = new Dictionary<string, bool> { { appId, false } }
            }
        });
        var sut = CreateSut();
        var appConfigField = typeof(CreateUserV4).GetField("_appConfig", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        appConfigField.SetValue(sut, appConfig);
        // Act
        var result = await sut.ExecuteAsync(user, appId, correlationId);
        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.UserName, result.UserName);
        Assert.Equal(user.Identifier, result.Identifier);
    }

    [Fact]
    public async Task ExecuteAsync_UserMigrationEnabled_NoMigrationData_ProceedsWithCreation()
    {
        // Arrange
        var appId = "app1";
        var correlationId = "corr-123";
        var user = new Core2EnterpriseUser() { UserName = "testuser" };
        var appConfig = new AppConfig
        {
            AppId = appId,
            AppName = "TestApp",
            IsEnabled = true,
            IntegrationMethodOutbound = IntegrationMethods.SQL,
            UserURIs = new List<UserURIs> { new UserURIs { AppId = appId, BaseUrl = "http://localhost", Post = new Uri("http://localhost/post"), Get = new Uri("http://localhost/get") } },
            AuthenticationDetails = new { },
            UserAttributeSchemas = new List<KN.KloudIdentity.Mapper.Domain.Mapping.AttributeSchema>()
        };
        _mockGetFullAppConfigQuery.Setup(x => x.GetAsync(appId, It.IsAny<System.Threading.CancellationToken>())).ReturnsAsync(appConfig);
        _mockOptions.Setup(x => x.Value).Returns(_appSettings);
        _mockAzureStorageManager.Setup(x => x.GetUserMigrationDataAsync(appId, It.IsAny<string>())).ReturnsAsync((UserMigrationData)null);
        var sut = CreateSut(withAzureStorageManager: true);
        var appConfigField = typeof(CreateUserV4).GetField("_appConfig", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        appConfigField.SetValue(sut, appConfig);
        // Act
        var result = await sut.ExecuteAsync(user, appId, correlationId);
        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteAsync_UserMigrationEnabled_MigrationDataFound_ReplacesUser()
    {
        // Arrange
        var appId = "app1";
        var correlationId = "corr-123";
        var user = new Core2EnterpriseUser() { UserName = "testuser" };
        var appConfig = new AppConfig
        {
            AppId = appId,
            AppName = "TestApp",
            IsEnabled = true,
            IntegrationMethodOutbound = IntegrationMethods.SQL,
            UserURIs = new List<UserURIs> { new UserURIs { AppId = appId, BaseUrl = "http://localhost", Post = new Uri("http://localhost/post"), Get = new Uri("http://localhost/get") } },
            AuthenticationDetails = new { },
            UserAttributeSchemas = new List<KN.KloudIdentity.Mapper.Domain.Mapping.AttributeSchema>()
        };
        _mockGetFullAppConfigQuery.Setup(x => x.GetAsync(appId, It.IsAny<System.Threading.CancellationToken>())).ReturnsAsync(appConfig);
        _mockOptions.Setup(x => x.Value).Returns(_appSettings);
        var migrationData = new UserMigrationData("partition", "migratedId", "corr");
        _mockAzureStorageManager.Setup(x => x.GetUserMigrationDataAsync(appId, It.IsAny<string>())).ReturnsAsync(migrationData);
        _mockReplaceResourceV2.Setup(x => x.ReplaceAsync(It.IsAny<Core2EnterpriseUser>(), appId, correlationId)).Returns(Task.CompletedTask);
        var sut = CreateSut(withAzureStorageManager: true);
        var appConfigField = typeof(CreateUserV4).GetField("_appConfig", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        appConfigField.SetValue(sut, appConfig);
        // Act
        var result = await sut.ExecuteAsync(user, appId, correlationId);
        // Assert
        Assert.NotNull(result);
        Assert.Equal("migratedId", result.Identifier);
    }

    [Fact]
    public async Task ExecuteAsync_UserMigrationEnabled_AzureStorageManagerNull_ProceedsWithCreation()
    {
        // Arrange
        var appId = "app1";
        var correlationId = "corr-123";
        var user = new Core2EnterpriseUser() { UserName = "testuser" };
        var appConfig = new AppConfig
        {
            AppId = appId,
            AppName = "TestApp",
            IsEnabled = true,
            IntegrationMethodOutbound = IntegrationMethods.SQL,
            UserURIs = new List<UserURIs> { new UserURIs { AppId = appId, BaseUrl = "http://localhost", Post = new Uri("http://localhost/post"), Get = new Uri("http://localhost/get") } },
            AuthenticationDetails = new { },
            UserAttributeSchemas = new List<KN.KloudIdentity.Mapper.Domain.Mapping.AttributeSchema>()
        };
        _mockGetFullAppConfigQuery.Setup(x => x.GetAsync(appId, It.IsAny<System.Threading.CancellationToken>())).ReturnsAsync(appConfig);
        _mockOptions.Setup(x => x.Value).Returns(_appSettings);
        var sut = CreateSut(withAzureStorageManager: false);
        var appConfigField = typeof(CreateUserV4).GetField("_appConfig", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        appConfigField.SetValue(sut, appConfig);
        // Act
        var result = await sut.ExecuteAsync(user, appId, correlationId);
        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteAsync_UserMigrationEnabled_CorrelationPropertyValueNull_ThrowsArgumentException()
    {
        // Arrange
        var appId = "app1";
        var correlationId = "corr-123";
        var user = new Core2EnterpriseUser() { };
        var appConfig = new AppConfig
        {
            AppId = appId,
            AppName = "TestApp",
            IsEnabled = true,
            IntegrationMethodOutbound = IntegrationMethods.SQL,
            UserURIs = new List<UserURIs> { new UserURIs { AppId = appId, BaseUrl = "http://localhost", Post = new Uri("http://localhost/post"), Get = new Uri("http://localhost/get") } },
            AuthenticationDetails = new { },
            UserAttributeSchemas = new List<KN.KloudIdentity.Mapper.Domain.Mapping.AttributeSchema>()
        };
        _mockGetFullAppConfigQuery.Setup(x => x.GetAsync(appId, It.IsAny<System.Threading.CancellationToken>())).ReturnsAsync(appConfig);
        _mockOptions.Setup(x => x.Value).Returns(_appSettings);
        var sut = CreateSut(withAzureStorageManager: true);
        var appConfigField = typeof(CreateUserV4).GetField("_appConfig", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        appConfigField.SetValue(sut, appConfig);
        // Simulate PropertyAccessorCacheUtil.GetPropertyValue returns null by not setting the property on user
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => sut.ExecuteAsync(user, appId, correlationId));
    }
}
