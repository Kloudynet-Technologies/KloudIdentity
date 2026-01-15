using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KloudIdentity.Mapper.MapperCore;
using KN.KloudIdentity.Mapper.MapperCore.Outbound.CustomLogic;
using KN.KloudIdentity.Mapper.MapperCore.User;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.SCIM;
using Moq;
using Serilog;
using Xunit;

namespace KN.KloudIdentity.MapperTests.MapperCore.User;

public class ReplaceUserV4Tests
{
    private readonly Mock<IAuthContext> _authContextMock = new();
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock = new();
    private readonly Mock<IGetFullAppConfigQuery> _getFullAppConfigQueryMock = new();
    private readonly Mock<IKloudIdentityLogger> _loggerMock = new();
    private readonly Mock<IIntegrationBaseFactory> _integrationBaseFactoryMock = new();
    private readonly Mock<IOutboundPayloadProcessor> _outboundPayloadProcessorMock = new();
    private readonly Mock<IIntegrationBaseV2> _integrationBaseMock = new();

    private ReplaceUserV4 CreateSut(AppConfig? appConfig = null)
    {
        if (appConfig != null)
        {
            _getFullAppConfigQueryMock.Setup(q => q.GetAsync(It.IsAny<string>(), CancellationToken.None))
                .ReturnsAsync(appConfig);
        }
        return new ReplaceUserV4(
            _authContextMock.Object,
            _httpClientFactoryMock.Object,
            _getFullAppConfigQueryMock.Object,
            _loggerMock.Object,
            _integrationBaseFactoryMock.Object,
            _outboundPayloadProcessorMock.Object
        );
    }

    [Fact]
    public async Task ReplaceAsync_ThrowsInvalidOperationException_WhenNoActionStepsFound()
    {
        // Arrange
        var appConfig = new AppConfig
        {
            AppId = "app1",
            Actions = new List<Mapper.Domain.Application.Action>(), // No actions
            AuthenticationDetails = default!,
            IntegrationMethodOutbound = IntegrationMethods.REST
        };
        var sut = CreateSut(appConfig);
        var user = new Core2EnterpriseUser { Identifier = "user1" };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ReplaceAsync(user, "app1", "corr1"));
    }

    [Fact]
    public async Task ReplaceAsync_ThrowsNotSupportedException_WhenIntegrationMethodNotSupported()
    {
        // Arrange
        var appConfig = new AppConfig
        {
            AppId = "app1",
            Actions = new List<Mapper.Domain.Application.Action>()
            {
                new  Mapper.Domain.Application.Action
                {
                    ActionName = ActionNames.PATCH,
                    ActionTarget = ActionTargets.USER,
                    ActionSteps = new List<ActionStep> { new ActionStep { StepOrder = 1 } }
                }
            },
            AuthenticationDetails = default!,
            IntegrationMethodOutbound = (IntegrationMethods)999 // Unknown
        };
        _integrationBaseFactoryMock.Setup(f => f.GetIntegration(It.IsAny<IntegrationMethods>(), It.IsAny<string>()))
            .Returns((IIntegrationBaseV2?)null);
        var sut = CreateSut(appConfig);
        var user = new Core2EnterpriseUser { Identifier = "user1" };

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            sut.ReplaceAsync(user, "app1", "corr1"));
    }

    [Fact]
    public async Task ReplaceAsync_ThrowsInvalidOperationException_WhenPayloadValidationFails()
    {
        // Arrange
        var actionStep = new ActionStep { StepOrder = 1, UserAttributeSchemas = new List<AttributeSchema>() };
        var appConfig = new AppConfig
        {
            AppId = "app1",
            AuthenticationDetails = default!,
            Actions = new List<Mapper.Domain.Application.Action>
            {
                new  Mapper.Domain.Application.Action
                {
                    ActionName = ActionNames.PATCH,
                    ActionTarget = ActionTargets.USER,
                    ActionSteps = new List<ActionStep> { actionStep }
                }
            },
            IntegrationMethodOutbound = IntegrationMethods.REST
        };
        _integrationBaseFactoryMock.Setup(f => f.GetIntegration(It.IsAny<IntegrationMethods>(), It.IsAny<string>()))
            .Returns(_integrationBaseMock.Object);
        _integrationBaseMock.Setup(m => m.MapAndPreparePayloadAsync(It.IsAny<List<AttributeSchema>>(), It.IsAny<Core2EnterpriseUser>(), CancellationToken.None))
            .ReturnsAsync(new object());
        _integrationBaseMock
            .Setup(m => m.ValidatePayloadAsync(It.IsAny<object>(), appConfig, It.IsAny<string>(), CancellationToken.None))
            .Returns(Task.FromResult((false, new string[] { "error1", "error2" })));
        _integrationBaseMock.Setup(m => m.ReplaceAsync(
            It.IsAny<object>(),
            It.IsAny<Core2EnterpriseUser>(),
            It.IsAny<string>(),
            appConfig,
            actionStep,
            It.IsAny<string>(),
            CancellationToken.None))
            .ReturnsAsync(new Core2EnterpriseUser { Identifier = "newId" });
        var sut = CreateSut(appConfig);
        var user = new Core2EnterpriseUser { Identifier = "user1" };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ReplaceAsync(user, "app1", "corr1"));
        Assert.Contains("Payload validation failed", ex.Message);
    }

    [Fact]
    public async Task ReplaceAsync_UpdatesIdentifierAndCreatesLog_WhenSuccessful()
    {
        // Arrange
        var actionStep = new ActionStep { StepOrder = 1, UserAttributeSchemas = new List<AttributeSchema>() };
        var appConfig = new AppConfig
        {
            AppId = "app1",
            AuthenticationDetails = default!,
            Actions = new List<Mapper.Domain.Application.Action>
            {
                new  Mapper.Domain.Application.Action
                {
                    ActionName = ActionNames.PATCH,
                    ActionTarget = ActionTargets.USER,
                    ActionSteps = new List<ActionStep> { actionStep }
                }
            },
            IntegrationMethodOutbound = IntegrationMethods.REST
        };
        _integrationBaseFactoryMock.Setup(f => f.GetIntegration(It.IsAny<IntegrationMethods>(), It.IsAny<string>()))
            .Returns(_integrationBaseMock.Object);
        _integrationBaseMock.Setup(m => m.MapAndPreparePayloadAsync(It.IsAny<List<AttributeSchema>>(), It.IsAny<Core2EnterpriseUser>(), CancellationToken.None))
            .ReturnsAsync(new object());
        _integrationBaseMock.Setup(m => m.ValidatePayloadAsync(It.IsAny<object>(), appConfig, It.IsAny<string>(), CancellationToken.None))
            .Returns((Task.FromResult((true, new string[] { }))));
        _integrationBaseMock.Setup(m => m.ReplaceAsync(
            It.IsAny<object>(),
            It.IsAny<Core2EnterpriseUser>(),
            It.IsAny<string>(),
            appConfig,
            actionStep,
            It.IsAny<string>(),
            CancellationToken.None))
            .ReturnsAsync(new Core2EnterpriseUser { Identifier = "newId" });
        _loggerMock.Setup(l => l.CreateLogAsync(It.IsAny<CreateLogEntity>(), CancellationToken.None)).Returns(Task.CompletedTask);
        var sut = CreateSut(appConfig);
        var user = new Core2EnterpriseUser { Identifier = "user1" };

        // Act
        await sut.ReplaceAsync(user, "app1", "corr1");

        // Assert
        Assert.Equal("newId", user.Identifier);
        _loggerMock.Verify(l => l.CreateLogAsync(It.Is<CreateLogEntity>(e => e.AppId == "app1" && e.CorrelationId == "corr1"), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task ReplaceAsync_ProcessesMultipleActionSteps()
    {
        // Arrange
        var actionStep1 = new ActionStep { StepOrder = 1, UserAttributeSchemas = new List<AttributeSchema>() };
        var actionStep2 = new ActionStep { StepOrder = 2, UserAttributeSchemas = new List<AttributeSchema>() };
        var appConfig = new AppConfig
        {
            AppId = "app1",
            AuthenticationDetails = default!,
            Actions = new List<Mapper.Domain.Application.Action>
            {
                new  Mapper.Domain.Application.Action
                {
                    ActionName = ActionNames.PATCH,
                    ActionTarget = ActionTargets.USER,
                    ActionSteps = new List<ActionStep> { actionStep1, actionStep2 }
                }
            },
            IntegrationMethodOutbound = IntegrationMethods.REST
        };
        _integrationBaseFactoryMock.Setup(f => f.GetIntegration(It.IsAny<IntegrationMethods>(), It.IsAny<string>()))
            .Returns(_integrationBaseMock.Object);
        _integrationBaseMock.Setup(m => m.MapAndPreparePayloadAsync(It.IsAny<List<AttributeSchema>>(), It.IsAny<Core2EnterpriseUser>(), CancellationToken.None))
            .ReturnsAsync(new object());
        _integrationBaseMock.Setup(m => m.ValidatePayloadAsync(It.IsAny<object>(), appConfig, It.IsAny<string>(), CancellationToken.None))
            .Returns((Task.FromResult((true, new string[] { }))));
        _integrationBaseMock.Setup(m => m.ReplaceAsync(
            It.IsAny<object>(),
            It.IsAny<Core2EnterpriseUser>(),
            It.IsAny<string>(),
            appConfig,
            It.IsAny<ActionStep>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((object payload, Core2EnterpriseUser user, string appId, AppConfig config, ActionStep step, string corrId, CancellationToken ct) =>
                new Core2EnterpriseUser { Identifier = user.Identifier + "_step" + step.StepOrder });
        _loggerMock.Setup(l => l.CreateLogAsync(It.IsAny<CreateLogEntity>(), CancellationToken.None)).Returns(Task.CompletedTask);
        var sut = CreateSut(appConfig);
        var user = new Core2EnterpriseUser { Identifier = "user1" };

        // Act
        await sut.ReplaceAsync(user, "app1", "corr1");

        // Assert
        Assert.Equal("user1_step1_step2", user.Identifier); // Last step's identifier
        _loggerMock.Verify(l => l.CreateLogAsync(It.Is<CreateLogEntity>(e => e.CorrelationId == "corr1"), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task ReplaceAsync_ThrowsArgumentException_WhenAppIdIsNullOrEmpty()
    {
        // Arrange
        var sut = CreateSut();
        var user = new Core2EnterpriseUser { Identifier = "user1" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.ReplaceAsync(user, null!, "corr1"));
        await Assert.ThrowsAsync<ArgumentException>(() => sut.ReplaceAsync(user, string.Empty, "corr1"));
    }

    [Fact]
    public async Task ReplaceAsync_ThrowsArgumentException_WhenCorrelationIdIsNullOrEmpty()
    {
        // Arrange
        var sut = CreateSut();
        var user = new Core2EnterpriseUser { Identifier = "user1" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.ReplaceAsync(user, "app1", null!));
        await Assert.ThrowsAsync<ArgumentException>(() => sut.ReplaceAsync(user, "app1", string.Empty));
    }

    [Fact]
    public void ReplaceAsync_ReturnsTask()
    {
        // Arrange
        var sut = CreateSut();
        var user = new Core2EnterpriseUser { Identifier = "user1" };

        // Act
        var task = sut.ReplaceAsync(user, "app1", "corr1");

        // Assert
        Assert.IsAssignableFrom<Task>(task);
    }

    [Fact]
    public async Task ReplaceAsync_ExceptionInsideTask_IsPropagated()
    {
        // Arrange
        var appConfig = new AppConfig
        {
            AppId = "app1",
            Actions = new List<Mapper.Domain.Application.Action>(), // No actions
            AuthenticationDetails = default!,
            IntegrationMethodOutbound = IntegrationMethods.REST
        };
        var sut = CreateSut(appConfig);
        var user = new Core2EnterpriseUser { Identifier = "user1" };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ReplaceAsync(user, "app1", "corr1"));
        Assert.Contains("No", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReplaceAsync_CompletesSuccessfully_WhenSetupForSuccess()
    {
        // Arrange
        var actionStep = new ActionStep { StepOrder = 1, UserAttributeSchemas = new List<AttributeSchema>() };
        var appConfig = new AppConfig
        {
            AppId = "app1",
            AuthenticationDetails = default!,
            Actions = new List<Mapper.Domain.Application.Action>
            {
                new  Mapper.Domain.Application.Action
                {
                    ActionName = ActionNames.PATCH,
                    ActionTarget = ActionTargets.USER,
                    ActionSteps = new List<ActionStep> { actionStep }
                }
            },
            IntegrationMethodOutbound = IntegrationMethods.REST
        };
        _integrationBaseFactoryMock.Setup(f => f.GetIntegration(It.IsAny<IntegrationMethods>(), It.IsAny<string>()))
            .Returns(_integrationBaseMock.Object);
        _integrationBaseMock.Setup(m => m.MapAndPreparePayloadAsync(It.IsAny<List<AttributeSchema>>(), It.IsAny<Core2EnterpriseUser>(), CancellationToken.None))
            .ReturnsAsync(new object());
        _integrationBaseMock.Setup(m => m.ValidatePayloadAsync(It.IsAny<object>(), appConfig, It.IsAny<string>(), CancellationToken.None))
            .Returns((Task.FromResult((true, new string[] { }))));
        _integrationBaseMock.Setup(m => m.ReplaceAsync(
            It.IsAny<object>(),
            It.IsAny<Core2EnterpriseUser>(),
            It.IsAny<string>(),
            appConfig,
            actionStep,
            It.IsAny<string>(),
            CancellationToken.None))
            .ReturnsAsync(new Core2EnterpriseUser { Identifier = "newId" });
        _loggerMock.Setup(l => l.CreateLogAsync(It.IsAny<CreateLogEntity>(), CancellationToken.None)).Returns(Task.CompletedTask);
        var sut = CreateSut(appConfig);
        var user = new Core2EnterpriseUser { Identifier = "user1" };

        // Act
        var task = sut.ReplaceAsync(user, "app1", "corr1");
        await task;

        // Assert
        Assert.True(task.IsCompletedSuccessfully);
        Assert.Equal("newId", user.Identifier);
    }
}
