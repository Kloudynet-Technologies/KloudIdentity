using KN.KI.RabbitMQ.MessageContracts;
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
        var inboundConfig = new InboundJobSchedulerConfig
        {
            AppId = "test-app-id",
            IsInboundJobEnabled = true,
            InboundJobFrequency = "0 0 * * *" 
        };
        var message = new Mock<IInterserviceRequestMsg>();
        message.Setup(m => m.Message).Returns(JsonSerializer.Serialize(inboundConfig));

        // Act
        var response = await _strategy.ProcessMessage(message.Object, default);

        // Assert
        Assert.Equal("Job added or updated successfully.", response.Message);
        _jobManagementServiceMock.Verify(j => j.AddOrUpdateJobAsync(inboundConfig.AppId, inboundConfig.InboundJobFrequency), Times.Once);
    }

    [Fact]
    public async Task ProcessMessageShouldReturnSuccessResponse_WhenIsInboundJobEnabledIsFalseANdRemoveJob()
    {
        // Arrange
        var inboundConfig = new InboundJobSchedulerConfig
        {
            AppId = "test-app-id",
            IsInboundJobEnabled = false,
            InboundJobFrequency = string.Empty
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
