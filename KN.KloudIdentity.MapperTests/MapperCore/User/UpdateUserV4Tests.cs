using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Common.Exceptions;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Infrastructure.Persistence.Abstractions;
using KN.KloudIdentity.Mapper.MapperCore;
using KN.KloudIdentity.Mapper.MapperCore.Outbound.CustomLogic;
using KN.KloudIdentity.Mapper.MapperCore.User;
using Microsoft.SCIM;
using Moq;

namespace KN.KloudIdentity.MapperTests.MapperCore.User;

public class UpdateUserV4Tests
{
    private readonly Mock<IAppConfigSnapshotRepository> _getFullAppConfigQueryMock = new();
    private readonly Mock<IKloudIdentityLogger> _loggerMock = new();
    private readonly Mock<IIntegrationBaseFactory> _integrationBaseFactoryMock = new();
    private readonly Mock<IOutboundPayloadProcessor> _outboundPayloadProcessorMock = new();
    private readonly Mock<IIntegrationBaseV2> _integrationBaseMock = new();
    private readonly Mock<ITenantContext> _tenantContextMock = new();
    private readonly PatchOperationContext _patchOperationContext = new();
    
    private UpdateUserV4 CreateSut(AppConfig? appConfig = null)
    {

        if (appConfig != null)
        {
            _tenantContextMock.Setup(x => x.TenantId).Returns("tenant1");
            _getFullAppConfigQueryMock.Setup(q => q.GetAppConfigByAppIdAsync("tenant1", It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(appConfig);
        }

        return new UpdateUserV4(
            _getFullAppConfigQueryMock.Object,
            _outboundPayloadProcessorMock.Object,
            _loggerMock.Object,
            _integrationBaseFactoryMock.Object,
            _tenantContextMock.Object,
            _patchOperationContext
        );
    }

    [Fact]
    public async Task UpdateAsync_ThrowsInvalidOperationException_WhenNoActionStepsFound()
    {
        // Arrange
        var appConfig = new AppConfig
        {
            AppId = "app1",
            Actions = new List<Mapper.Domain.Application.Action>(), // No actions
            AuthenticationDetails = null!,
            IntegrationMethodOutbound = IntegrationMethods.REST
        };
        _tenantContextMock.Setup(x => x.TenantId).Returns("tenant1");
        _getFullAppConfigQueryMock.Setup(q => q.GetAppConfigByAppIdAsync("tenant1", "app1", It.IsAny<CancellationToken>())).ReturnsAsync(appConfig);
        var sut = CreateSut();

        // Build PatchOperation2
        var patchOperation = new PatchOperation2Combined
        {
            OperationName = "replace",
            Value = "abc"
        };

        // Build PatchRequest2 and add the operation
        var patchRequest = new PatchRequest2();
        patchRequest.AddOperation(patchOperation);

        // Build Patch object
        var user = new Patch
        {
            ResourceIdentifier = new ResourceIdentifier { Identifier = "user1" },
            PatchRequest = patchRequest
        };

        _integrationBaseFactoryMock.Setup(f => f.GetIntegration(It.IsAny<IntegrationMethods>(), It.IsAny<string>()))
            .Returns(_integrationBaseMock.Object);
        // Act & Assert
        await Xunit.Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.UpdateAsync(user, "app1", "corr1"));
    }

    [Fact]
    public async Task UpdateAsync_ThrowsNotSupportedException_WhenIntegrationMethodNotSupported()
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
        _tenantContextMock.Setup(x => x.TenantId).Returns("tenant1");
        _getFullAppConfigQueryMock.Setup(q => q.GetAppConfigByAppIdAsync("tenant1", "app1", It.IsAny<CancellationToken>())).ReturnsAsync(appConfig);
        var sut = CreateSut();
        // Build PatchOperation2
        var patchOperation = new PatchOperation2Combined
        {
            OperationName = "replace",
            Value = "abc"
        };

        // Build PatchRequest2 and add the operation
        var patchRequest = new PatchRequest2();
        patchRequest.AddOperation(patchOperation);

        // Build Patch object
        var user = new Patch
        {
            ResourceIdentifier = new ResourceIdentifier { Identifier = "user1" },
            PatchRequest = patchRequest
        };

        // Act & Assert
        await Xunit.Assert.ThrowsAsync<NotSupportedException>(() =>
            sut.UpdateAsync(user, "app1", "corr1"));
    }

    [Fact]
    public async Task UpdateAsync_ThrowsPayloadValidationException_WhenPayloadValidationFails()
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
                    ActionName = ActionNames.EDIT,
                    ActionTarget = ActionTargets.USER,
                    ActionSteps = new List<ActionStep> { actionStep }
                }
            },
            IntegrationMethodOutbound = IntegrationMethods.REST
        };
        _integrationBaseFactoryMock.Setup(f => f.GetIntegration(It.IsAny<IntegrationMethods>(), It.IsAny<string>()))
            .Returns(_integrationBaseMock.Object);
        _integrationBaseMock.Setup(m => m.MapAndPreparePayloadAsync(It.IsAny<List<AttributeSchema>>(),
                It.IsAny<Core2EnterpriseUser>(), CancellationToken.None))
            .ReturnsAsync(new object());
        _integrationBaseMock
            .Setup(m => m.ValidatePayloadAsync(It.IsAny<object>(), appConfig, It.IsAny<string>(),
                CancellationToken.None))
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
        // Build PatchOperation2
        var patchOperation = new PatchOperation2Combined
        {
            OperationName = "replace",
            Value = "abc"
        };

        // Build PatchRequest2 and add the operation
        var patchRequest = new PatchRequest2();
        patchRequest.AddOperation(patchOperation);

        // Build Patch object
        var user = new Patch
        {
            ResourceIdentifier = new ResourceIdentifier { Identifier = "user1" },
            PatchRequest = patchRequest
        };

        // Act & Assert
        var ex = await Xunit.Assert.ThrowsAsync<PayloadValidationException>(() =>
            sut.UpdateAsync(user, "app1", "corr1"));
        Assert.Contains("Payload validation failed", ex.Message);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesIdentifierAndCreatesLog_WhenSuccessful()
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
                    ActionName = ActionNames.EDIT,
                    ActionTarget = ActionTargets.USER,
                    ActionSteps = new List<ActionStep> { actionStep }
                }
            },
            IntegrationMethodOutbound = IntegrationMethods.REST
        };
        // Build PatchOperation2
        var patchOperation = new PatchOperation2Combined
        {
            OperationName = "replace",
            Value = "abc"
        };

        // Build PatchRequest2 and add the operation
        var patchRequest = new PatchRequest2();
        patchRequest.AddOperation(patchOperation);

        // Build Patch object
        var user = new Patch
        {
            ResourceIdentifier = new ResourceIdentifier { Identifier = "user1" },
            PatchRequest = patchRequest
        };

        _integrationBaseFactoryMock.Setup(f => f.GetIntegration(It.IsAny<IntegrationMethods>(), It.IsAny<string>()))
            .Returns(_integrationBaseMock.Object);
        _integrationBaseMock.Setup(m => m.MapAndPreparePayloadAsync(It.IsAny<List<AttributeSchema>>(),
                It.IsAny<Core2EnterpriseUser>(), CancellationToken.None))
            .ReturnsAsync(new object());
        _integrationBaseMock.Setup(m =>
                m.ValidatePayloadAsync(It.IsAny<object>(), appConfig, It.IsAny<string>(), CancellationToken.None))
            .Returns((Task.FromResult((true, new string[] { }))));
        _integrationBaseMock.Setup(m => m.UpdateAsync(
                It.IsAny<object>(),
                It.IsAny<Core2EnterpriseUser>(),
                It.IsAny<string>(),
                appConfig,
                actionStep,
                It.IsAny<string>(),
                CancellationToken.None));
        
        _loggerMock.Setup(l => l.CreateLogAsync(It.IsAny<CreateLogEntity>(), CancellationToken.None))
            .Returns(Task.CompletedTask);
        var sut = CreateSut(appConfig);

        // Act
        await sut.UpdateAsync(user, "app1", "corr1");

        // Assert
        _loggerMock.Verify(
            l => l.CreateLogAsync(It.Is<CreateLogEntity>(e => e.AppId == "app1" && e.CorrelationId == "corr1"),
                CancellationToken.None), Times.Once);
    }
    
    [Fact]
    public async Task UpdateAsync_ProcessesMultipleActionSteps()
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
        _integrationBaseMock.Setup(m => m.UpdateAsync(
            It.IsAny<object>(),
            It.IsAny<Core2EnterpriseUser>(),
            It.IsAny<string>(),
            appConfig,
            It.IsAny<ActionStep>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()));
        _loggerMock.Setup(l => l.CreateLogAsync(It.IsAny<CreateLogEntity>(), CancellationToken.None)).Returns(Task.CompletedTask);
        var sut = CreateSut(appConfig);
        // Build PatchOperation2
        var patchOperation = new PatchOperation2Combined
        {
            OperationName = "replace",
            Value = "abc"
        };

        // Build PatchRequest2 and add the operation
        var patchRequest = new PatchRequest2();
        patchRequest.AddOperation(patchOperation);

        // Build Patch object
        var user = new Patch
        {
            ResourceIdentifier = new ResourceIdentifier { Identifier = "user1" },
            PatchRequest = patchRequest
        };


        // Act
        await sut.UpdateAsync(user, "app1", "corr1");

        // Assert
        _loggerMock.Verify(l => l.CreateLogAsync(It.Is<CreateLogEntity>(e => e.CorrelationId == "corr1"), CancellationToken.None), Times.Once);
    }
    
    [Fact]
    public async Task UpdateAsync_CompletesSuccessfully_WhenSetupForSuccess()
    {
        // Arrange
        var actionStep = new ActionStep { StepOrder = 1, UserAttributeSchemas = new List<AttributeSchema>() };
        var appId = "app1";
        var tenantId = "tenant1";
        var appConfig = new AppConfig
        {
            AppId = appId,
            AuthenticationDetails = null!,
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
        _tenantContextMock.Setup(x => x.TenantId).Returns(tenantId);
        _getFullAppConfigQueryMock.Setup(q => q.GetAppConfigByAppIdAsync(tenantId, appId, It.IsAny<CancellationToken>())).ReturnsAsync(appConfig);
        _integrationBaseFactoryMock.Setup(f => f.GetIntegration(It.IsAny<IntegrationMethods>(), It.IsAny<string>()))
            .Returns(_integrationBaseMock.Object);
        _integrationBaseMock.Setup(m => m.MapAndPreparePayloadAsync(It.IsAny<List<AttributeSchema>>(), It.IsAny<Core2EnterpriseUser>(), CancellationToken.None))
            .ReturnsAsync(new object());
        _integrationBaseMock.Setup(m => m.ValidatePayloadAsync(It.IsAny<object>(), appConfig, It.IsAny<string>(), CancellationToken.None))
            .Returns((Task.FromResult((true, new string[] { }))));
        _integrationBaseMock.Setup(m => m.UpdateAsync(
            It.IsAny<object>(),
            It.IsAny<Core2EnterpriseUser>(),
            It.IsAny<string>(),
            appConfig,
            actionStep,
            It.IsAny<string>(),
            CancellationToken.None));
        _loggerMock.Setup(l => l.CreateLogAsync(It.IsAny<CreateLogEntity>(), CancellationToken.None)).Returns(Task.CompletedTask);
        var sut = CreateSut(appConfig);
        // Build PatchOperation2
        var patchOperation = new PatchOperation2Combined
        {
            OperationName = "replace",
            Value = "abc"
        };

        // Build PatchRequest2 and add the operation
        var patchRequest = new PatchRequest2();
        patchRequest.AddOperation(patchOperation);

        // Build Patch object
        var user = new Patch
        {
            ResourceIdentifier = new ResourceIdentifier { Identifier = "user1" },
            PatchRequest = patchRequest
        };

        // Act
        var task = sut.UpdateAsync(user, appId, "corr1");
        await task;

        // Assert
        Assert.True(task.IsCompletedSuccessfully);
    }
    

    [Fact]
    public async Task UpdateAsync_CapturesPatchOperationsBeforeCallingIntegration()
    {
        // Arrange
        var appConfig = new AppConfig
        {
            AppId = "app1",
            AuthenticationDetails = null!,
            UserAttributeSchemas = new List<AttributeSchema>
            {
                new() { HttpRequestType = HttpRequestTypes.PATCH, SourceValue = "DisplayName", DestinationField = "@Name" }
            },
            IntegrationMethodOutbound = IntegrationMethods.SQL
        };
        var patchedDuringUpdate = false;
        _integrationBaseFactoryMock.Setup(f => f.GetIntegration(IntegrationMethods.SQL, "app1"))
            .Returns(_integrationBaseMock.Object);
        _integrationBaseMock.Setup(m => m.MapAndPreparePayloadAsync(It.IsAny<IList<AttributeSchema>>(),
                It.IsAny<Core2EnterpriseUser>(), appConfig, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new object());
        _integrationBaseMock.Setup(m => m.ValidatePayloadAsync(It.IsAny<object>(), appConfig, It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, Array.Empty<string>()));
        _integrationBaseMock.Setup(m => m.UpdateAsync(It.IsAny<object>(), It.IsAny<Core2EnterpriseUser>(), appConfig,
                It.IsAny<string>()))
            .Callback(() => patchedDuringUpdate = _patchOperationContext.IsPatched("DisplayName"))
            .Returns(Task.CompletedTask);
        var sut = CreateSut(appConfig);

        var patchRequest = new PatchRequest2();
        patchRequest.AddOperation(new PatchOperation2Combined(OperationName.Replace, "displayName")
        {
            Value = "[{\"value\":\"New Name\"}]"
        });
        var patch = new Patch
        {
            ResourceIdentifier = new ResourceIdentifier { Identifier = "user1" },
            PatchRequest = patchRequest
        };

        // Act
        await sut.UpdateAsync(patch, "app1", "corr1");

        // Assert: captured while the integration ran, reset afterwards
        Assert.True(patchedDuringUpdate);
        Assert.False(_patchOperationContext.IsCaptured);
    }

    [Fact]
    public async Task UpdateAsync_ResetsPatchContext_WhenIntegrationThrows()
    {
        // Arrange
        var appConfig = new AppConfig
        {
            AppId = "app1",
            AuthenticationDetails = null!,
            UserAttributeSchemas = new List<AttributeSchema>(),
            IntegrationMethodOutbound = IntegrationMethods.SQL
        };
        _integrationBaseFactoryMock.Setup(f => f.GetIntegration(IntegrationMethods.SQL, "app1"))
            .Returns(_integrationBaseMock.Object);
        _integrationBaseMock.Setup(m => m.MapAndPreparePayloadAsync(It.IsAny<IList<AttributeSchema>>(),
                It.IsAny<Core2EnterpriseUser>(), appConfig, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("mapping failed"));
        var sut = CreateSut(appConfig);

        var patchRequest = new PatchRequest2();
        patchRequest.AddOperation(new PatchOperation2Combined(OperationName.Replace, "active") { Value = "[{\"value\":\"False\"}]" });
        var patch = new Patch
        {
            ResourceIdentifier = new ResourceIdentifier { Identifier = "user1" },
            PatchRequest = patchRequest
        };

        // Act & Assert: the original exception surfaces unchanged and the context does not leak
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.UpdateAsync(patch, "app1", "corr1"));
        Assert.Equal("mapping failed", ex.Message);
        Assert.False(_patchOperationContext.IsCaptured);
    }

    [Fact]
    public async Task UpdateAsync_InvalidPath_FailsAsBeforeAndResetsContext()
    {
        // Arrange
        var appConfig = new AppConfig
        {
            AppId = "app1",
            AuthenticationDetails = null!,
            IntegrationMethodOutbound = IntegrationMethods.SQL
        };
        var sut = CreateSut(appConfig);

        var operation = Newtonsoft.Json.JsonConvert.DeserializeObject<PatchOperation2Combined>(
            "{\"op\":\"replace\",\"path\":\"emails[[x]]\",\"value\":\"[]\"}")!;
        var patchRequest = new PatchRequest2();
        patchRequest.AddOperation(operation);
        var patch = new Patch
        {
            ResourceIdentifier = new ResourceIdentifier { Identifier = "user1" },
            PatchRequest = patchRequest
        };

        // Act & Assert: Core2EnterpriseUser.Apply still reports the invalid path (Capture does not throw first)
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.UpdateAsync(patch, "app1", "corr1"));
        Assert.False(_patchOperationContext.IsCaptured);
    }
}
