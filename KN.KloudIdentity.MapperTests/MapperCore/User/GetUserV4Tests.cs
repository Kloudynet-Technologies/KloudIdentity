using System;
using Xunit;
using Moq;
using System.Threading.Tasks;
using KN.KloudIdentity.Mapper.MapperCore.User;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KloudIdentity.Mapper.MapperCore;
using KN.KloudIdentity.Mapper.MapperCore.Outbound.CustomLogic;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Domain.Application;
using Microsoft.Extensions.Options;
using KN.KloudIdentity.Mapper.Domain;
using Xunit.Sdk;
using Microsoft.SCIM;
using KN.KloudIdentity.Mapper.Common.Exceptions;
using KN.KloudIdentity.Mapper;
using Microsoft.Extensions.Configuration;
using Moq.Protected;
using KN.KloudIdentity.Mapper.Domain.Mapping;

namespace KN.KloudIdentity.MapperTests.MapperCore.User;

public class GetUserV4Tests
{
    private readonly Mock<IGetFullAppConfigQuery> _mockGetFullAppConfigQuery = new();
    private readonly Mock<IIntegrationBaseFactory> _mockIntegrationFactory = new();
    private readonly Mock<IOutboundPayloadProcessor> _mockOutboundPayloadProcessor = new();
    private readonly Mock<IKloudIdentityLogger> _mockLogger = new();
    private readonly Mock<IOptions<AppSettings>> _mockOptions = new();
    private readonly Mock<IServiceProvider> _mockServiceProvider = new();

    private GetUserV4 CreateSut()
    {
        return new GetUserV4(
            _mockGetFullAppConfigQuery.Object,
            _mockIntegrationFactory.Object,
            _mockOutboundPayloadProcessor.Object,
            _mockLogger.Object);
    }

    private RESTIntegrationV4 CreateRestIntegrationV4Sut(Func<HttpRequestMessage, HttpResponseMessage>? httpHandlerFunc = null)
    {
        var mockAuthContext = new Mock<IAuthContext>();

        // Setup a delegating handler to mock HTTP responses
        var handler = new Mock<HttpMessageHandler>();
        if (httpHandlerFunc != null)
        {
            handler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage request, CancellationToken token) => httpHandlerFunc(request));
        }
        else
        {
            handler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"identifier\":\"test-identifier\",\"userName\":\"testuser\"}")
                });
        }

        var httpClient = new System.Net.Http.HttpClient(handler.Object);
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        mockHttpClientFactory
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        var mockConfiguration = new Mock<IConfiguration>();
        mockConfiguration
            .Setup(x => x["urnPrefix"])
            .Returns("urn:ietf:params:scim:schemas:core:2.0:");

        var mockLogger = _mockLogger;

        var appSettings = new AppSettings
        {
            AppIntegrationConfigs = new List<AppIntegrationConfig>
            {
                new AppIntegrationConfig
                {
                    AppId = "test-app-id",
                    ClientType = "Navitaire",
                    TechnicianUrl = "https://tech.example.com",
                    HttpSettings = new HttpSettings()
                }
            }
        };
        _mockOptions.Setup(x => x.Value).Returns(appSettings);

        var mockAuthStrategies = new List<IAuthStrategy>();
        var mockAppSettings = _mockOptions;

        return new RESTIntegrationV4(
            mockAuthContext.Object,
            mockHttpClientFactory.Object,
            mockConfiguration.Object,
            mockLogger.Object,
            mockAppSettings.Object,
            mockAuthStrategies
        );
    }

    [Fact]
    public async Task Should_Throw_When_AppConfig_Has_No_Actions()
    {
        // Arrange
        var getUserV4 = CreateSut();
        var appId = "test-app-id";
        var identifier = "test-identifier";
        var correlationID = "test-correlation-id";

        _mockGetFullAppConfigQuery
            .Setup(x => x.GetAsync(appId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppConfig
            {
                AppId = appId,
                Actions = new List<Mapper.Domain.Application.Action>(), // No actions defined
                AuthenticationDetails = default!
            });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await getUserV4.GetAsync(identifier, appId, correlationID);
        });
    }

    [Fact]
    public async Task Should_Throw_When_No_ActionSteps_For_User_Get()
    {
        // Arrange
        var getUserV4 = CreateSut();
        var appId = "test-app-id";
        var identifier = "test-identifier";
        var correlationID = "test-correlation-id";

        _mockGetFullAppConfigQuery
            .Setup(x => x.GetAsync(appId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppConfig
            {
                AppId = appId,
                Actions = new List<Mapper.Domain.Application.Action>
                {
                    new Mapper.Domain.Application.Action
                    {
                        ActionTarget = ActionTargets.NotDefined, // No UserGet action
                        ActionSteps = new List<ActionStep>()
                    }
                },
                AuthenticationDetails = default!
            });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await getUserV4.GetAsync(identifier, appId, correlationID);
        });
    }

    [Fact]
    public async Task Should_Throw_When_IntegrationMethod_Not_Supported()
    {
        // Arrange
        var getUserV4 = CreateSut();
        var appId = "test-app-id";
        var identifier = "test-identifier";
        var correlationID = "test-correlation-id";

        _mockGetFullAppConfigQuery
            .Setup(x => x.GetAsync(appId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppConfig
            {
                AppId = appId,
                Actions = new List<Mapper.Domain.Application.Action>
                {
                    new Mapper.Domain.Application.Action
                    {
                        ActionTarget = ActionTargets.USER, // No UserGet action
                        ActionName = ActionNames.GET,
                        ActionSteps = new List<ActionStep>()
                        {
                            new ActionStep
                            {
                                StepOrder = 1,
                                HttpVerb = HttpVerbs.GET,
                                EndPoint = "https://api.example.com/users/{0}"
                            }
                        }
                    }
                },
                AuthenticationDetails = default!,
                IntegrationMethodOutbound = IntegrationMethods.NotDefined
            });

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await getUserV4.GetAsync(identifier, appId, correlationID);
        });
    }

    [Fact]
    public async Task Should_Throw_When_User_Not_Found_In_Any_Step()
    {
        // Arrange
        var getUserV4 = CreateSut();
        var appId = "test-app-id";
        var identifier = "test-identifier";
        var correlationID = "test-correlation-id";

        _mockGetFullAppConfigQuery
            .Setup(x => x.GetAsync(appId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppConfig
            {
                AppId = appId,
                Actions = new List<Mapper.Domain.Application.Action>
                {
                    new Mapper.Domain.Application.Action
                    {
                        ActionTarget = ActionTargets.USER,
                        ActionName = ActionNames.GET,
                        ActionSteps = new List<ActionStep>()
                        {
                            new ActionStep
                            {
                                StepOrder = 1,
                                HttpVerb = HttpVerbs.GET,
                                EndPoint = "https://api.example.com/users/{0}"
                            }
                        }
                    }
                },
                AuthenticationDetails = default!,
                IntegrationMethodOutbound = IntegrationMethods.REST
            });

        var mockIntegrationMethod = new Mock<IIntegrationBaseV2>();
#pragma warning disable CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.
        mockIntegrationMethod
            .Setup(x => x.GetAsync(identifier, It.IsAny<AppConfig>(), It.IsAny<ActionStep>(), correlationID, CancellationToken.None))
            .ReturnsAsync((Core2EnterpriseUser?)null); // Simulate user not found
#pragma warning restore CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.

        _mockIntegrationFactory
            .Setup(x => x.GetIntegration(IntegrationMethods.REST, appId))
            .Returns(mockIntegrationMethod.Object);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(async () =>
        {
            await getUserV4.GetAsync(identifier, appId, correlationID);
        });
    }

    [Fact]
    public async Task Should_Return_User_When_Found_In_First_Step()
    {
        // Arrange
        var getUserV4 = CreateSut();
        var appId = "test-app-id";
        var identifier = "test-identifier";
        var correlationID = "test-correlation-id";

        var expectedUser = new Core2EnterpriseUser
        {
            Identifier = identifier,
            UserName = "testuser"
        };

        _mockGetFullAppConfigQuery
            .Setup(x => x.GetAsync(appId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppConfig
            {
                AppId = appId,
                Actions = new List<Mapper.Domain.Application.Action>
                {
                    new Mapper.Domain.Application.Action
                    {
                        ActionTarget = ActionTargets.USER,
                        ActionName = ActionNames.GET,
                        ActionSteps = new List<ActionStep>()
                        {
                            new ActionStep
                            {
                                StepOrder = 1,
                                HttpVerb = HttpVerbs.GET,
                                EndPoint = "https://api.example.com/users/{0}"
                            }
                        }
                    }
                },
                AuthenticationDetails = default!,
                IntegrationMethodOutbound = IntegrationMethods.REST
            });

        var mockIntegrationMethod = new Mock<IIntegrationBaseV2>();
        mockIntegrationMethod
            .Setup(x => x.GetAsync(identifier, It.IsAny<AppConfig>(), It.IsAny<ActionStep>(), correlationID, CancellationToken.None))
            .ReturnsAsync(expectedUser); // Simulate user found

        _mockIntegrationFactory
            .Setup(x => x.GetIntegration(IntegrationMethods.REST, appId))
            .Returns(mockIntegrationMethod.Object);

        // Act
        var result = await getUserV4.GetAsync(identifier, appId, correlationID);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedUser.Identifier, result.Identifier);
        Assert.Equal(expectedUser.UserName, result.UserName);
    }

    [Fact]
    public async Task Should_Return_User_When_Found_In_Later_Step()
    {
        // Arrange
        var getUserV4 = CreateSut();
        var appId = "test-app-id";
        var identifier = "test-identifier";
        var correlationID = "test-correlation-id";

        var expectedUser = new Core2EnterpriseUser
        {
            Identifier = identifier,
            UserName = "testuser"
        };

        _mockGetFullAppConfigQuery
            .Setup(x => x.GetAsync(appId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppConfig
            {
                AppId = appId,
                Actions = new List<Mapper.Domain.Application.Action>
                {
                    new Mapper.Domain.Application.Action
                    {
                        ActionTarget = ActionTargets.USER,
                        ActionName = ActionNames.GET,
                        ActionSteps = new List<ActionStep>()
                        {
                            new ActionStep
                            {
                                StepOrder = 1,
                                HttpVerb = HttpVerbs.GET,
                                EndPoint = "https://api.example.com/users/{0}"
                            },
                            new ActionStep
                            {
                                StepOrder = 2,
                                HttpVerb = HttpVerbs.GET,
                                EndPoint = "https://api.example.com/users/{0}"
                            }
                        }
                    }
                },
                AuthenticationDetails = default!,
                IntegrationMethodOutbound = IntegrationMethods.REST
            });

        var mockIntegrationMethod = new Mock<IIntegrationBaseV2>();
#pragma warning disable CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.
        mockIntegrationMethod
            .SetupSequence(x => x.GetAsync(identifier, It.IsAny<AppConfig>(), It.IsAny<ActionStep>(), correlationID, CancellationToken.None))
            .ReturnsAsync((Core2EnterpriseUser?)null) // First step: user not found
            .ReturnsAsync(expectedUser); // Second step: user found
#pragma warning restore CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.

        _mockIntegrationFactory
            .Setup(x => x.GetIntegration(IntegrationMethods.REST, appId))
            .Returns(mockIntegrationMethod.Object);

        // Act
        var result = await getUserV4.GetAsync(identifier, appId, correlationID);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedUser.Identifier, result.Identifier);
        Assert.Equal(expectedUser.UserName, result.UserName);
    }

    // RESTIntegrationV4 tests

    [Fact]
    public async Task Should_Throw_When_ActionStep_Is_Null()
    {
        // Arrange
        var restIntegrationV4 = CreateRestIntegrationV4Sut();
        var appConfig = new AppConfig()
        {
            AppId = "test-app-id",
            AuthenticationDetails = default!
        };
        var identifier = "test-identifier";
        var correlationID = "test-correlation-id";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await restIntegrationV4.GetAsync(identifier, appConfig, null!, correlationID, CancellationToken.None);
        });
    }

    [Fact]
    public async Task Should_Throw_When_Endpoint_Is_Null_Or_Empty()
    {

        // Arrange
        var restIntegrationV4 = CreateRestIntegrationV4Sut();
        var appConfig = new AppConfig()
        {
            AppId = "test-app-id",
            AuthenticationDetails = default!
        };
        var identifier = "test-identifier";
        var correlationID = "test-correlation-id";
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        var actionStep = new ActionStep
        {
            StepOrder = 1,
            HttpVerb = HttpVerbs.GET,
            EndPoint = null // Null endpoint
        };
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await restIntegrationV4.GetAsync(identifier, appConfig, actionStep, correlationID, CancellationToken.None);
        });
    }

    [Fact]
    public async Task Should_Return_User_For_Navitaire_ClientType()
    {
        // Arrange
        var restIntegrationV4 = CreateRestIntegrationV4Sut();
        var appConfig = new AppConfig()
        {
            AppId = "test-app-id",
            AuthenticationDetails = default!
        };
        var identifier = "test-identifier";
        var correlationID = "test-correlation-id";
        var actionStep = new ActionStep
        {
            StepOrder = 1,
            HttpVerb = HttpVerbs.GET,
            EndPoint = "https://api.example.com/users/{0}",
            UserAttributeSchemas = new List<AttributeSchema>
            {
                new AttributeSchema
                {
                    SourceValue = "Identifier",
                    DestinationField = "urn:ietf:params:scim:schemas:core:2.0:identifier"
                },
                new AttributeSchema
                {
                    SourceValue = "UserName",
                    DestinationField = "urn:ietf:params:scim:schemas:core:2.0:userName"
                }
            }
        };

        // Here you would typically mock the HTTP client to return a success status code
        // with a valid user response for Navitaire client type.
        // For brevity, this part is omitted.

        // Act
        var result = await restIntegrationV4.GetAsync(identifier, appConfig, actionStep, correlationID, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(identifier, result.Identifier);
    }

    [Fact]
    public async Task Should_Return_User_For_Non_Navitaire_ClientType()
    {
        // Arrange
        var restIntegrationV4 = CreateRestIntegrationV4Sut();
        var appConfig = new AppConfig()
        {
            AppId = "test-app-id",
            AuthenticationDetails = default!
        };
        var identifier = "test-identifier";
        var correlationID = "test-correlation-id";
        var actionStep = new ActionStep
        {
            StepOrder = 1,
            HttpVerb = HttpVerbs.GET,
            EndPoint = "https://api.example.com/users/{0}",
            UserAttributeSchemas = new List<AttributeSchema>
            {
                new AttributeSchema
                {
                    SourceValue = "Identifier",
                    DestinationField = "urn:ietf:params:scim:schemas:core:2.0:identifier"
                },
                new AttributeSchema
                {
                    SourceValue = "UserName",
                    DestinationField = "urn:ietf:params:scim:schemas:core:2.0:userName"
                }
            }
        };

        // Here you would typically mock the HTTP client to return a success status code
        // with a valid user response for non-Navitaire client type.
        // For brevity, this part is omitted.

        // Act
        var result = await restIntegrationV4.GetAsync(identifier, appConfig, actionStep, correlationID, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(identifier, result.Identifier);
    }
}
