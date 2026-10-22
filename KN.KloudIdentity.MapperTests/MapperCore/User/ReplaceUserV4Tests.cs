using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper;
using KN.KloudIdentity.Mapper.Common.Exceptions;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KloudIdentity.Mapper.Infrastructure.Persistence.Abstractions;
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
    private readonly Mock<IAppConfigSnapshotRepository> _getFullAppConfigQueryMock = new();
    private readonly Mock<IKloudIdentityLogger> _loggerMock = new();
    private readonly Mock<IIntegrationBaseFactory> _integrationBaseFactoryMock = new();
    private readonly Mock<IOutboundPayloadProcessor> _outboundPayloadProcessorMock = new();
    private readonly Mock<IIntegrationBaseV2> _integrationBaseMock = new();
    private readonly Mock<ITenantContext> _tenantContextMock = new();

    private ReplaceUserV4 CreateSut(AppConfig? appConfig = null)
    {
        if (appConfig != null)
        {
            _tenantContextMock.Setup(x => x.TenantId).Returns("tenant1");
            _getFullAppConfigQueryMock.Setup(q => q.GetAppConfigByAppIdAsync("tenant1", It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(appConfig);
        }
        return new ReplaceUserV4(
            _getFullAppConfigQueryMock.Object,
            _loggerMock.Object,
            _integrationBaseFactoryMock.Object,
            _outboundPayloadProcessorMock.Object,
            _tenantContextMock.Object
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
        _tenantContextMock.Setup(x => x.TenantId).Returns("tenant1");
        _getFullAppConfigQueryMock.Setup(q => q.GetAppConfigByAppIdAsync("tenant1", "app1", It.IsAny<CancellationToken>())).ReturnsAsync(appConfig);
        var sut = CreateSut();
        var user = new Core2EnterpriseUser { Identifier = "user1" };
        _integrationBaseFactoryMock.Setup(f => f.GetIntegration(It.IsAny<IntegrationMethods>(), It.IsAny<string>()))
            .Returns(_integrationBaseMock.Object);

        // Act & Assert
        await Xunit.Assert.ThrowsAsync<InvalidOperationException>(() =>
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
                    ActionName = ActionNames.EDIT,
                    ActionTarget = ActionTargets.USER,
                    ActionSteps = new List<ActionStep> { new ActionStep { StepOrder = 1 } }
                }
            },
            AuthenticationDetails = default!,
            IntegrationMethodOutbound = (IntegrationMethods)999 // Unknown
        };
        _tenantContextMock.Setup(x => x.TenantId).Returns("tenant1");
        _getFullAppConfigQueryMock.Setup(q => q.GetAppConfigByAppIdAsync("tenant1", "app1", It.IsAny<CancellationToken>())).ReturnsAsync(appConfig);
        var sut = CreateSut();
        var user = new Core2EnterpriseUser { Identifier = "user1" };
        _integrationBaseFactoryMock.Setup(f => f.GetIntegration(It.IsAny<IntegrationMethods>(), It.IsAny<string>()))
            .Returns((IIntegrationBaseV2?)null);

        // Act & Assert
        await Xunit.Assert.ThrowsAsync<NotSupportedException>(() =>
            sut.ReplaceAsync(user, "app1", "corr1"));
    }

    [Fact]
    public async Task ReplaceAsync_ThrowsPayloadValidationException_WhenPayloadValidationFails()
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
                    ActionName = ActionNames.EDIT,
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
        var ex = await Xunit.Assert.ThrowsAsync<PayloadValidationException>(() =>
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
                    ActionName = ActionNames.EDIT,
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
                    ActionName = ActionNames.EDIT,
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
        await Xunit.Assert.ThrowsAsync<ArgumentNullException>(() => sut.ReplaceAsync(user, null!, "corr1"));
        await Xunit.Assert.ThrowsAsync<ArgumentException>(() => sut.ReplaceAsync(user, string.Empty, "corr1"));
    }

    [Fact]
    public async Task ReplaceAsync_ThrowsArgumentException_WhenCorrelationIdIsNullOrEmpty()
    {
        // Arrange
        var sut = CreateSut();
        var user = new Core2EnterpriseUser { Identifier = "user1" };

        // Act & Assert
        await Xunit.Assert.ThrowsAsync<ArgumentNullException>(() => sut.ReplaceAsync(user, "app1", null!));
        await Xunit.Assert.ThrowsAsync<ArgumentException>(() => sut.ReplaceAsync(user, "app1", string.Empty));
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
        _integrationBaseFactoryMock.Setup(f => f.GetIntegration(It.IsAny<IntegrationMethods>(), It.IsAny<string>()))
            .Returns(_integrationBaseMock.Object);

        // Act & Assert
        var ex = await Xunit.Assert.ThrowsAsync<InvalidOperationException>(() => sut.ReplaceAsync(user, "app1", "corr1"));
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
                    ActionName = ActionNames.EDIT,
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

    [Theory]
    [InlineData(false, HttpRequestTypes.PATCH)] // MgtPortal only maps PATCH rows → used for PUT
    [InlineData(true, HttpRequestTypes.PUT)]    // PUT rows win when present
    public async Task ReplaceAsync_GenericPath_UsesPutRowsOrFallsBackToPatchRows(bool hasPutRows, HttpRequestTypes expected)
    {
        // Arrange
        var rows = new List<AttributeSchema>
        {
            new() { HttpRequestType = HttpRequestTypes.POST, SourceValue = "UserName", DestinationField = "@LoginID" },
            new() { HttpRequestType = HttpRequestTypes.PATCH, SourceValue = "DisplayName", DestinationField = "@Name" }
        };
        if (hasPutRows)
            rows.Add(new AttributeSchema { HttpRequestType = HttpRequestTypes.PUT, SourceValue = "DisplayName", DestinationField = "@FullName" });

        var appConfig = new AppConfig
        {
            AppId = "app1",
            AuthenticationDetails = default!,
            UserAttributeSchemas = rows,
            IntegrationMethodOutbound = IntegrationMethods.SQL
        };
        IList<AttributeSchema>? mappedRows = null;
        _integrationBaseFactoryMock.Setup(f => f.GetIntegration(IntegrationMethods.SQL, "app1"))
            .Returns(_integrationBaseMock.Object);
        _integrationBaseMock.Setup(m => m.MapAndPreparePayloadAsync(It.IsAny<IList<AttributeSchema>>(),
                It.IsAny<Core2EnterpriseUser>(), appConfig, It.IsAny<CancellationToken>()))
            .Callback<IList<AttributeSchema>, Core2EnterpriseUser, AppConfig, CancellationToken>((schema, _, _, _) => mappedRows = schema)
            .ReturnsAsync(new object());
        _integrationBaseMock.Setup(m => m.ValidatePayloadAsync(It.IsAny<object>(), appConfig, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, Array.Empty<string>()));
        _integrationBaseMock.Setup(m => m.ReplaceAsync(It.IsAny<object>(), It.IsAny<Core2EnterpriseUser>(), appConfig, It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        var sut = CreateSut(appConfig);

        // Act
        await sut.ReplaceAsync(new Core2EnterpriseUser { Identifier = "user1", UserName = "user1" }, "app1", "corr1");

        // Assert
        Assert.NotNull(mappedRows);
        Assert.NotEmpty(mappedRows!);
        Assert.All(mappedRows!, r => Assert.Equal(expected, r.HttpRequestType));
    }
}
