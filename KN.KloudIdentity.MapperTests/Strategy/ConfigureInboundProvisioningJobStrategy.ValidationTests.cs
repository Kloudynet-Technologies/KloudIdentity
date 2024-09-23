using KN.KI.LogAggregator.Library.Abstractions;
using KN.KI.RabbitMQ.MessageContracts;
using KN.KloudIdentity.Mapper.BackgroundJobs;
using KN.KloudIdentity.Mapper.Domain;
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
        var inboundConfig = new InboundJobSchedulerConfig
        {
            AppId = null,
            IsInboundJobEnabled = true,
            InboundJobFrequency = "0 0 * * *"
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
    public async Task HandleShouldError_WhenIsInboundJobEnabledIsTrueAndInboundJobFrequencyEmpty()
    {
        // Arrange
        // Arrange
        var inboundConfig = new InboundJobSchedulerConfig
        {
            AppId = "test-app-id",
            IsInboundJobEnabled = true,
            InboundJobFrequency = string.Empty
        };
        var message = new Mock<IInterserviceRequestMsg>();
        message.Setup(m => m.Message).Returns(JsonSerializer.Serialize(inboundConfig));

        // Act
        var response = await _strategy.ProcessMessage(message.Object, default);

        // Assert
        Assert.True(response.IsError);
        Assert.Contains("InboundJobFrequency is required when IsInboundJobEnabled is true.", response.ErrorMessage);
        _jobManagementServiceMock.Verify(j => j.AddOrUpdateJobAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
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
}
