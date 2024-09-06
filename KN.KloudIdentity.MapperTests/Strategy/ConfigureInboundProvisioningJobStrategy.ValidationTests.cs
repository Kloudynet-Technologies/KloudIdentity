using KN.KI.LogAggregator.Library.Abstractions;
using KN.KI.RabbitMQ.MessageContracts;
using KN.KloudIdentity.Mapper.BackgroundJobs;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using KN.KloudIdentity.Mapper.Masstransit;
using Moq;
using System.Text.Json;

namespace KN.KloudIdentity.MapperTests.MessagingStrategy;

public partial class ConfigureInboundProvisioningJobStrategyTests
{
    private readonly Mock<IJobManagementService> _jobManagementServiceMock;
    private readonly Mock<IKloudIdentityLogger> _loggerMock;
    private readonly ConfigureInboundProvisioningJobStrategy _strategy;

    public ConfigureInboundProvisioningJobStrategyTests()
    {
        _jobManagementServiceMock = new Mock<IJobManagementService>();
        _loggerMock = new Mock<IKloudIdentityLogger>();
        _strategy = new ConfigureInboundProvisioningJobStrategy(_jobManagementServiceMock.Object, _loggerMock.Object);
    }
    [Fact]
    public async Task ProcessMessageShouldValidationFailed_WhenAppIdIsNull()
    {
        // Arrange
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        var inboundConfig = new InboundAppConfig
        {
            AppId = null,
            IsInboundJobEnabled = true,
            InboundJobScheduler = new InboundJobScheduler { InboundJobFrequency = "0 0 * * *" },
            InboundAuthConfig = new InboundAuthConfig
            {
                AuthenticationMethod = AuthenticationMethods.APIKey,
                APIKeyAuthentication = new APIKeyAuthentication
                {
                    APIKey = "test-api-key",
                    AuthHeaderName = "username",
                    ExpirationDate = DateTime.UtcNow.AddDays(1)
                }
            },

            InboundIntegrationMethod = new InboundIntegrationMethod
            {
                IntegrationMethod = IntegrationMethods.REST,
                RESTIntegrationMethod = new InboundRESTIntegrationMethod
                {
                    Id = Guid.NewGuid(),
                    AppId = "test-app-id",
                    CreationTriggerOffsetDays = 1,
                    JoiningDateProperty = "joiningDate",
                    ProvisioningEndpoint = "https://test.com/provision",
                    UsersEndpoint = "https://test.com/users",
                    CreationTrigger = TriggerType.Before
                }
            }
        };
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        var message = new Mock<IInterserviceRequestMsg>();
        message.Setup(m => m.Message).Returns(JsonSerializer.Serialize(inboundConfig));

        // Act
        var result = await _strategy.ProcessMessage(message.Object, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Contains("The AppId field is required", result.ErrorMessage);
    }

    [Fact]
    public async Task ProcessMessage_ShouldReturnErrorResponse_WhenValidationFails()
    {
        // Arrange
        // Arrange
        var inboundConfig = new InboundAppConfig
        {
            AppId = "test-app-id",
            IsInboundJobEnabled = true,
            InboundJobScheduler = null,
            InboundAuthConfig = new InboundAuthConfig
            {
                AuthenticationMethod = AuthenticationMethods.APIKey,
                APIKeyAuthentication = new APIKeyAuthentication
                {
                    APIKey = "test-api-key",
                    AuthHeaderName = "username",
                    ExpirationDate = DateTime.UtcNow.AddDays(1)
                }
            },

            InboundIntegrationMethod = new InboundIntegrationMethod
            {
                IntegrationMethod = IntegrationMethods.REST,
                RESTIntegrationMethod = new InboundRESTIntegrationMethod
                {
                    Id = Guid.NewGuid(),
                    AppId = "test-app-id",
                    CreationTriggerOffsetDays = 1,
                    JoiningDateProperty = "joiningDate",
                    ProvisioningEndpoint = "https://test.com/provision",
                    UsersEndpoint = "https://test.com/users",
                    CreationTrigger = TriggerType.Before
                }
            }
        };
        var message = new Mock<IInterserviceRequestMsg>();
        message.Setup(m => m.Message).Returns(JsonSerializer.Serialize(inboundConfig));

        // Act
        var response = await _strategy.ProcessMessage(message.Object, default);

        // Assert
        Assert.True(response.IsError);
        Assert.Contains("InboundJobScheduler is required when IsInboundJobEnabled is true.", response.ErrorMessage);
        _jobManagementServiceMock.Verify(j => j.AddOrUpdateJobAsync(It.IsAny<InboundAppConfig>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ProcessMessageShouldValidationFailed_WhenInboundAuthConfigIsNull()
    {
        // Arrange
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        var inboundConfig = new InboundAppConfig
        {
            AppId = "test-app-id",
            IsInboundJobEnabled = true,
            InboundJobScheduler = new InboundJobScheduler { InboundJobFrequency = "0 0 * * *" },
            InboundAuthConfig = null,

            InboundIntegrationMethod = new InboundIntegrationMethod
            {
                IntegrationMethod = IntegrationMethods.REST,
                RESTIntegrationMethod = new InboundRESTIntegrationMethod
                {
                    Id = Guid.NewGuid(),
                    AppId = "test-app-id",
                    CreationTriggerOffsetDays = 1,
                    JoiningDateProperty = "joiningDate",
                    ProvisioningEndpoint = "https://test.com/provision",
                    UsersEndpoint = "https://test.com/users",
                    CreationTrigger = TriggerType.Before
                }
            }
        };
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        var message = new Mock<IInterserviceRequestMsg>();
        message.Setup(m => m.Message).Returns(JsonSerializer.Serialize(inboundConfig));

        // Act
        var result = await _strategy.ProcessMessage(message.Object, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Contains("The InboundAuthConfig field is required", result.ErrorMessage);
    }

    [Fact]
    public async Task ProcessMessageShouldValidationFailed_WhenInboundIntegrationMethodIsNull()
    {
        // Arrange
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        var inboundConfig = new InboundAppConfig
        {
            AppId = "test-app-id",
            IsInboundJobEnabled = true,
            InboundJobScheduler = new InboundJobScheduler { InboundJobFrequency = "0 0 * * *" },
            InboundAuthConfig = new InboundAuthConfig
            {
                AuthenticationMethod = AuthenticationMethods.APIKey,
                APIKeyAuthentication = new APIKeyAuthentication
                {
                    APIKey = "test-api-key",
                    AuthHeaderName = "username",
                    ExpirationDate = DateTime.UtcNow.AddDays(1)
                }
            },

            InboundIntegrationMethod = null
        };
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        var message = new Mock<IInterserviceRequestMsg>();
        message.Setup(m => m.Message).Returns(JsonSerializer.Serialize(inboundConfig));

        // Act
        var result = await _strategy.ProcessMessage(message.Object, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Contains("The InboundIntegrationMethod field is required", result.ErrorMessage);
    }

    [Fact]
    public async Task ProcessMessage_ShouldReturnErrorResponse_WhenDeserializationFails()
    {
        // Arrange
        var message = new Mock<IInterserviceRequestMsg>();
        message.Setup(m => m.Message).Returns("invalid-json");

        // Act
        var exception = await Record.ExceptionAsync(
            () => _strategy.ProcessMessage(message.Object, CancellationToken.None)
        );

        // Assert
        Assert.NotNull(exception);
    }

    [Fact]
    public async Task ProcessMessageShouldValidationFailed_WhenAuthMethodIsAPIKeyButAPIKeyAuthIsNull()
    {
        // Arrange
        var inboundConfig = new InboundAppConfig
        {
            AppId = "test-app-id",
            IsInboundJobEnabled = true,
            InboundJobScheduler = new InboundJobScheduler { InboundJobFrequency = "0 0 * * *" },
            InboundAuthConfig = new InboundAuthConfig
            {
                AuthenticationMethod = AuthenticationMethods.APIKey,
                APIKeyAuthentication = null
            },

            InboundIntegrationMethod = new InboundIntegrationMethod
            {
                IntegrationMethod = IntegrationMethods.REST,
                RESTIntegrationMethod = new InboundRESTIntegrationMethod
                {
                    Id = Guid.NewGuid(),
                    AppId = "test-app-id",
                    CreationTriggerOffsetDays = 1,
                    JoiningDateProperty = "joiningDate",
                    ProvisioningEndpoint = "https://test.com/provision",
                    UsersEndpoint = "https://test.com/users",
                    CreationTrigger = TriggerType.Before
                }
            }
        };
        var message = new Mock<IInterserviceRequestMsg>();
        message.Setup(m => m.Message).Returns(JsonSerializer.Serialize(inboundConfig));

        // Act
        var result = await _strategy.ProcessMessage(message.Object, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Contains("APIKeyAuthentication is required when AuthenticationMethod is APIKey.", result.ErrorMessage);
    }

    [Fact]
    public async Task ProcessMessageShouldValidationFailed_WhenIntegrationMethodIsRESTButRESTIntegrationMethodIsNull()
    {
        // Arrange
        var inboundConfig = new InboundAppConfig
        {
            AppId = "test-app-id",
            IsInboundJobEnabled = true,
            InboundJobScheduler = new InboundJobScheduler { InboundJobFrequency = "0 0 * * *" },
            InboundAuthConfig = new InboundAuthConfig
            {
                AuthenticationMethod = AuthenticationMethods.APIKey,
                APIKeyAuthentication = new APIKeyAuthentication
                {
                    APIKey = "test-api-key",
                    AuthHeaderName = "username",
                    ExpirationDate = DateTime.UtcNow.AddDays(1)
                }
            },

            InboundIntegrationMethod = new InboundIntegrationMethod
            {
                IntegrationMethod = IntegrationMethods.REST,
                RESTIntegrationMethod = null
            }
        };
        var message = new Mock<IInterserviceRequestMsg>();
        message.Setup(m => m.Message).Returns(JsonSerializer.Serialize(inboundConfig));

        // Act
        var result = await _strategy.ProcessMessage(message.Object, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Contains("RESTIntegrationMethod is required when IntegrationMethod is REST.", result.ErrorMessage);
    }

    [Fact]
    public async Task ProcessMessageShouldValidationFailed_WhenAuthMethodIsBearerButBearerAuthIsNull()
    {
        // Arrange
        var inboundConfig = new InboundAppConfig
        {
            AppId = "test-app-id",
            IsInboundJobEnabled = true,
            InboundJobScheduler = new InboundJobScheduler { InboundJobFrequency = "0 0 * * *" },
            InboundAuthConfig = new InboundAuthConfig
            {
                AuthenticationMethod = AuthenticationMethods.Bearer,
                BearerAuthentication = null
            },

            InboundIntegrationMethod = new InboundIntegrationMethod
            {
                IntegrationMethod = IntegrationMethods.REST,
                RESTIntegrationMethod = new InboundRESTIntegrationMethod
                {
                    Id = Guid.NewGuid(),
                    AppId = "test-app-id",
                    CreationTriggerOffsetDays = 1,
                    JoiningDateProperty = "joiningDate",
                    ProvisioningEndpoint = "https://test.com/provision",
                    UsersEndpoint = "https://test.com/users",
                    CreationTrigger = TriggerType.Before
                }
            }
        };
        var message = new Mock<IInterserviceRequestMsg>();
        message.Setup(m => m.Message).Returns(JsonSerializer.Serialize(inboundConfig));

        // Act
        var result = await _strategy.ProcessMessage(message.Object, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Contains("BearerAuthentication is required when AuthenticationMethod is Bearer.", result.ErrorMessage);
    }

    [Fact]
    public async Task PrcoessMessageShoudValidationFailed_WhenAuthMethodIsBasicButBasicAuthIsNull()
    {
        // Arrange
        var inboundConfig = new InboundAppConfig
        {
            AppId = "test-app-id",
            IsInboundJobEnabled = true,
            InboundJobScheduler = new InboundJobScheduler { InboundJobFrequency = "0 0 * * *" },
            InboundAuthConfig = new InboundAuthConfig
            {
                AuthenticationMethod = AuthenticationMethods.Basic,
                BasicAuthentication = null
            },

            InboundIntegrationMethod = new InboundIntegrationMethod
            {
                IntegrationMethod = IntegrationMethods.REST,
                RESTIntegrationMethod = new InboundRESTIntegrationMethod
                {
                    Id = Guid.NewGuid(),
                    AppId = "test-app-id",
                    CreationTriggerOffsetDays = 1,
                    JoiningDateProperty = "joiningDate",
                    ProvisioningEndpoint = "https://test.com/provision",
                    UsersEndpoint = "https://test.com/users",
                    CreationTrigger = TriggerType.Before
                }
            }
        };
        var message = new Mock<IInterserviceRequestMsg>();
        message.Setup(m => m.Message).Returns(JsonSerializer.Serialize(inboundConfig));

        // Act
        var result = await _strategy.ProcessMessage(message.Object, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Contains("BasicAuthentication is required when AuthenticationMethod is Basic.", result.ErrorMessage);
    }

    [Fact]
    public async Task ProcessMessageShouldValidationFailed_WhenAuthMethodIsOIDCClientCrdButOAuth2AuthIsNull()
    {
        // Arrange
        var inboundConfig = new InboundAppConfig
        {
            AppId = "test-app-id",
            IsInboundJobEnabled = true,
            InboundJobScheduler = new InboundJobScheduler { InboundJobFrequency = "0 0 * * *" },
            InboundAuthConfig = new InboundAuthConfig
            {
                AuthenticationMethod = AuthenticationMethods.OIDC_ClientCrd,
                OAuth2Authentication = null
            },

            InboundIntegrationMethod = new InboundIntegrationMethod
            {
                IntegrationMethod = IntegrationMethods.REST,
                RESTIntegrationMethod = new InboundRESTIntegrationMethod
                {
                    Id = Guid.NewGuid(),
                    AppId = "test-app-id",
                    CreationTriggerOffsetDays = 1,
                    JoiningDateProperty = "joiningDate",
                    ProvisioningEndpoint = "https://test.com/provision",
                    UsersEndpoint = "https://test.com/users",
                    CreationTrigger = TriggerType.Before
                }
            }
        };
        var message = new Mock<IInterserviceRequestMsg>();
        message.Setup(m => m.Message).Returns(JsonSerializer.Serialize(inboundConfig));

        // Act
        var result = await _strategy.ProcessMessage(message.Object, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Contains("OAuth2Authentication is required when AuthenticationMethod is OIDC_ClientCrd.", result.ErrorMessage);
    }

    [Fact]
    public async Task ProcessMessageShouldValidationFailed_WhenUsersEndpointIsNul()
    {
        // Arrange
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        var inboundConfig = new InboundAppConfig
        {
            AppId = "test-app-id",
            IsInboundJobEnabled = true,
            InboundJobScheduler = new InboundJobScheduler { InboundJobFrequency = "0 0 * * *" },
            InboundAuthConfig = new InboundAuthConfig
            {
                AuthenticationMethod = AuthenticationMethods.APIKey,
                APIKeyAuthentication = new APIKeyAuthentication
                {
                    APIKey = "test-api-key",
                    AuthHeaderName = "username",
                    ExpirationDate = DateTime.UtcNow.AddDays(1)
                }
            },

            InboundIntegrationMethod = new InboundIntegrationMethod
            {
                IntegrationMethod = IntegrationMethods.REST,
                RESTIntegrationMethod = new InboundRESTIntegrationMethod
                {
                    Id = Guid.NewGuid(),
                    AppId = "test-app-id",
                    CreationTriggerOffsetDays = 1,
                    JoiningDateProperty = "joiningDate",
                    ProvisioningEndpoint = "https://test.com/provision",
                    UsersEndpoint = null,
                    CreationTrigger = TriggerType.Before
                }
            }
        };
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        var message = new Mock<IInterserviceRequestMsg>();
        message.Setup(m => m.Message).Returns(JsonSerializer.Serialize(inboundConfig));

        // Act
        var result = await _strategy.ProcessMessage(message.Object, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Contains("The UsersEndpoint field is required", result.ErrorMessage);

    }

    [Fact]

    public async Task ProcessMessageShouldValidationFailed_WhenProvisioningEndpointIsNull()
    {
        // Arrange
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        var inboundConfig = new InboundAppConfig
        {
            AppId = "test-app-id",
            IsInboundJobEnabled = true,
            InboundJobScheduler = new InboundJobScheduler { InboundJobFrequency = "0 0 * * *" },
            InboundAuthConfig = new InboundAuthConfig
            {
                AuthenticationMethod = AuthenticationMethods.APIKey,
                APIKeyAuthentication = new APIKeyAuthentication
                {
                    APIKey = "test-api-key",
                    AuthHeaderName = "username",
                    ExpirationDate = DateTime.UtcNow.AddDays(1)
                }
            },

            InboundIntegrationMethod = new InboundIntegrationMethod
            {
                IntegrationMethod = IntegrationMethods.REST,
                RESTIntegrationMethod = new InboundRESTIntegrationMethod
                {
                    Id = Guid.NewGuid(),
                    AppId = "test-app-id",
                    CreationTriggerOffsetDays = 1,
                    JoiningDateProperty = "joiningDate",
                    ProvisioningEndpoint = null,
                    UsersEndpoint = "https://test.com/users",
                    CreationTrigger = TriggerType.Before
                }
            }
        };
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.

        var message = new Mock<IInterserviceRequestMsg>();
        message.Setup(m => m.Message).Returns(JsonSerializer.Serialize(inboundConfig));

        // Act
        var result = await _strategy.ProcessMessage(message.Object, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Contains("The ProvisioningEndpoint field is required", result.ErrorMessage);
    }
}
