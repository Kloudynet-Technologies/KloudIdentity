using System.Collections.Generic;
using System.Threading.Tasks;
using KN.KloudIdentity.Mapper;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using Moq;
using Xunit;

namespace KN.KloudIdentity.MapperTests.AuthContext;

public class AuthContextV2Tests
{
    [Fact]
    public async Task GetTokenListAsync_ReturnsTokens_ForValidFlow()
    {
        // Arrange
        var mockStrategy = new Mock<IAuthStrategy>();
        mockStrategy.Setup(s => s.AuthenticationMethod).Returns(AuthenticationMethods.Basic);
        mockStrategy.Setup(s => s.GetTokenAsync(It.IsAny<object>())).ReturnsAsync("token1");

        var strategies = new List<IAuthStrategy> { mockStrategy.Object };

        var flow = new AuthenticationFlow
        {
            Steps =
            [
                new AuthenticationFlowStep
                {
                    StepTitle = "Step 1",
                    StepOrder = 1,
                    AuthenticationMethod = AuthenticationMethods.Basic,
                    AuthenticationDetails = new { }
                }
            ]
        };  

        var appConfig = new AppConfig
        {
            AppId =  "test-app",
            AuthenticationFlow = flow,
            AuthenticationDetails = null
        };

        var context = new AuthContextV2(strategies);

        // Act
        var result = await context.GetTokenListAsync(appConfig, SCIMDirections.Outbound);

        // Assert
        Assert.Single(result);
        Assert.Equal("token1", result[1]);
    }

    [Fact]
    public async Task GetTokenListAsync_Throws_WhenFlowIsNull()
    {
        // Arrange
        var strategies = new List<IAuthStrategy>();
        var  appConfig = new  AppConfig{ AuthenticationFlow = null, AuthenticationDetails = null };
        var context = new AuthContextV2(strategies);

        // Act & Assert
        await Assert.ThrowsAsync<System.Security.Authentication.AuthenticationException>(
            () => context.GetTokenListAsync(appConfig, SCIMDirections.Outbound));
    }

    [Fact]
    public async Task GetTokenListAsync_Throws_WhenTokenIsEmpty()
    {
        // Arrange
        var mockStrategy = new Mock<IAuthStrategy>();
        mockStrategy.Setup(s => s.AuthenticationMethod).Returns(AuthenticationMethods.Basic);
        mockStrategy.Setup(s => s.GetTokenAsync(It.IsAny<object>())).ReturnsAsync(string.Empty);

        var strategies = new List<IAuthStrategy> { mockStrategy.Object };

        var flow = new AuthenticationFlow
        {
            Steps =
            [
                new AuthenticationFlowStep
                {
                    StepTitle = "Step 1",
                    StepOrder = 1,
                    AuthenticationMethod = AuthenticationMethods.Basic,
                    AuthenticationDetails = new { }
                }
            ]
        };

        var appConfig = new AppConfig
        {
            AppId = "test-app",
            AuthenticationFlow = flow,
            AuthenticationDetails = null
        };

        var context = new AuthContextV2(strategies);

        // Act & Assert
        await Assert.ThrowsAsync<System.Security.Authentication.AuthenticationException>(
            () => context.GetTokenListAsync(appConfig, SCIMDirections.Outbound));
    }

    [Fact]
    public async Task GetTokenListAsync_Throws_WhenStrategyNotFound()
    {
        // Arrange
        var strategies = new List<IAuthStrategy>(); // No strategies
        var flow = new AuthenticationFlow
        {
            Steps =
            [
                new AuthenticationFlowStep
                {
                    StepTitle = "Step 1",
                    StepOrder = 1,
                    AuthenticationMethod = AuthenticationMethods.Basic,
                    AuthenticationDetails = new { }
                }
            ]
        };  

        var appConfig = new AppConfig
        {
            AppId =  "test-app",
            AuthenticationFlow = flow,
            AuthenticationDetails = null
        };

        var context = new AuthContextV2(strategies);

        // Act & Assert
        await Assert.ThrowsAsync<System.Security.Authentication.AuthenticationException>(
            () => context.GetTokenListAsync(appConfig, SCIMDirections.Outbound));
    }
}