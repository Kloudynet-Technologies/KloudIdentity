using KN.KI.RabbitMQ.MessageContracts;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using KN.KloudIdentity.Mapper.Domain;
using Moq;
using System.Text.Json;

namespace KN.KloudIdentity.MapperTests.MessagingStrategy;

public partial class ConfigureInboundProvisioningJobStrategyTests
{
    [Fact]
    public async Task ProcessMessageShouldReturnSuccessResponse_WhenJobConfiguredSuccessfully()
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
        Assert.Equal("Job added or updated successfully.", response.Message);
        _jobManagementServiceMock.Verify(j => j.AddOrUpdateJobAsync(inboundConfig, inboundConfig.InboundJobScheduler!.InboundJobFrequency), Times.Once);
    }

    [Fact]
    public async Task ProcessMessageShouldReturnSuccessResponse_WhenIsInboundJobEnabledIsFalseANdRemoveJob()
    {
        // Arrange
        var inboundConfig = new InboundAppConfig
        {
            AppId = "test-app-id",
            IsInboundJobEnabled = false,
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
        var message = new Mock<IInterserviceRequestMsg>();
        message.Setup(m => m.Message).Returns(JsonSerializer.Serialize(inboundConfig));

        // Act
        var response = await _strategy.ProcessMessage(message.Object, default);

        // Assert
        Assert.Equal("Removed job successfully.", response.Message);
        _jobManagementServiceMock.Verify(j => j.RemoveJob(inboundConfig.AppId), Times.Once);
    }
}
