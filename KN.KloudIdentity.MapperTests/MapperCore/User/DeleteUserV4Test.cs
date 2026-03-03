using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KloudIdentity.Mapper.Infrastructure.Persistence.Abstractions;
using KN.KloudIdentity.Mapper.MapperCore;
using KN.KloudIdentity.Mapper.MapperCore.Outbound.CustomLogic;
using KN.KloudIdentity.Mapper.MapperCore.User;
using Microsoft.SCIM;
using Moq;

namespace KN.KloudIdentity.MapperTests.MapperCore.User;

public class DeleteUserV4Test
{
    private readonly Mock<IAppConfigSnapshotRepository> _mockGetFullAppConfigQuery = new();
    private readonly Mock<IIntegrationBaseFactory> _integrationBaseFactoryMock = new();
    private readonly Mock<IOutboundPayloadProcessor> _mockOutboundPayloadProcessor = new();
    private readonly Mock<IKloudIdentityLogger> _mockLogger = new();
    
    private DeleteUserV4 CreateSut(AppConfig? appConfig = null)
    {
        if (appConfig != null)
        {
            _mockGetFullAppConfigQuery.Setup(q => q.GetAppConfigByAppIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(appConfig);
        }

        return new DeleteUserV4(
            _mockGetFullAppConfigQuery.Object,
            _mockOutboundPayloadProcessor.Object,
            _integrationBaseFactoryMock.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task DeleteAsync_ThrowsInvalidOperationException_WhenNoActionStepsFound()
    {
        // Arrange
        var appConfig = new AppConfig
        {
            AppId = "app1",
            Actions = new List<Mapper.Domain.Application.Action>(), // No actions
            AuthenticationDetails = null!,
            IntegrationMethodOutbound = IntegrationMethods.REST
        };

        var resourceIdentifier = new ResourceIdentifier { Identifier = "user1" };
        var sut = CreateSut(appConfig);
        _integrationBaseFactoryMock.Setup(f => f.GetIntegration(It.IsAny<IntegrationMethods>(), It.IsAny<string>()))
            .Returns((IntegrationMethods method, string param) => Mock.Of<IIntegrationBaseV2>());

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.DeleteAsync(resourceIdentifier, "app1", "correlationId1"));
    }

    [Fact]
    public async Task DeleteAsync_ThrowsNotSupportedException_WhenIntegrationMethodNotSupported()
    {
        // Arrange
        var appConfig = new AppConfig
        {
            AppId = "app1",
            Actions = new List<Mapper.Domain.Application.Action>()
            {
                new Mapper.Domain.Application.Action
                {
                    ActionName = ActionNames.PATCH,
                    ActionTarget = ActionTargets.USER,
                    ActionSteps = new List<ActionStep> { new ActionStep { StepOrder = 1 } }
                }
            },
            AuthenticationDetails = null!,
            IntegrationMethodOutbound = (IntegrationMethods)999 // Unknown
        };
        _integrationBaseFactoryMock.Setup(f => f.GetIntegration(It.IsAny<IntegrationMethods>(), It.IsAny<string>()))
            .Returns((IIntegrationBaseV2?)null);
        var sut = CreateSut(appConfig);
        var user = new ResourceIdentifier { Identifier = "user1" };

        // Act & Assert
        await Xunit.Assert.ThrowsAsync<NotSupportedException>(() =>
            sut.DeleteAsync(user, "app1", "corr1"));
    }

    [Fact]
    public async Task DeleteAsync_ProcessesMultipleActionSteps()
    {
        // Arrange
        var actionStep1 = new ActionStep { StepOrder = 1, UserAttributeSchemas = new List<AttributeSchema>() };
        var actionStep2 = new ActionStep { StepOrder = 2, UserAttributeSchemas = new List<AttributeSchema>() };
        var appConfig = new AppConfig
        {
            AppId = "app1",
            AuthenticationDetails = null!,
            Actions = new List<Mapper.Domain.Application.Action>
            {
                new Mapper.Domain.Application.Action
                {
                    ActionName = ActionNames.DELETE,
                    ActionTarget = ActionTargets.USER,
                    ActionSteps = new List<ActionStep> { actionStep1, actionStep2 }
                }
            },
            IntegrationMethodOutbound = IntegrationMethods.REST
        };
        _integrationBaseFactoryMock.Setup(f => f.GetIntegration(It.IsAny<IntegrationMethods>(), It.IsAny<string>()))
            .Returns((IntegrationMethods method, string param) => Mock.Of<IIntegrationBaseV2>());

        _mockLogger.Setup(l => l.CreateLogAsync(It.IsAny<CreateLogEntity>(), CancellationToken.None))
            .Returns(Task.CompletedTask);
        var sut = CreateSut(appConfig);
        var user = new ResourceIdentifier { Identifier = "user1" };

        // Act
        await sut.DeleteAsync(user, "app1", "corr1");

        // Assert
        _mockLogger.Verify(
            l => l.CreateLogAsync(It.Is<CreateLogEntity>(e => e.CorrelationId == "corr1"), CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_CompletesSuccessfully_WhenSetupForSuccess()
    {
        // Arrange
        var actionStep = new ActionStep { StepOrder = 1, UserAttributeSchemas = new List<AttributeSchema>() };
        var appConfig = new AppConfig
        {
            AppId = "app1",
            AuthenticationDetails = null!,
            Actions = new List<Mapper.Domain.Application.Action>
            {
                new Mapper.Domain.Application.Action
                {
                    ActionName = ActionNames.DELETE,
                    ActionTarget = ActionTargets.USER,
                    ActionSteps = new List<ActionStep> { actionStep }
                }
            },
            IntegrationMethodOutbound = IntegrationMethods.REST
        };
        _integrationBaseFactoryMock.Setup(f => f.GetIntegration(It.IsAny<IntegrationMethods>(), It.IsAny<string>()))
            .Returns((IntegrationMethods method, string param) => Mock.Of<IIntegrationBaseV2>());

        _mockLogger.Setup(l => l.CreateLogAsync(It.IsAny<CreateLogEntity>(), CancellationToken.None))
            .Returns(Task.CompletedTask);
        var sut = CreateSut(appConfig);
        var resourceIdentifier = new ResourceIdentifier { Identifier = "user1" };

        // Act
        var task = sut.DeleteAsync(resourceIdentifier, "app1", "corr1");
        await task;

        // Assert
        Assert.True(task.IsCompletedSuccessfully);
    }
}