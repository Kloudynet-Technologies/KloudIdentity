using KN.KloudIdentity.Mapper.Domain;
using Microsoft.Extensions.Options;
using KN.KloudIdentity.Mapper.MapperCore;
using Moq;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using Newtonsoft.Json;
using KN.KloudIdentity.Mapper.Domain.Mapping;

namespace KN.KloudIdentity.MapperTests;

public partial class SQLIntegrationTest
{
    private readonly Mock<IOptions<AppSettings>> _mockAppSettings;
    private readonly SQLIntegration _odbcIntegration;

    public SQLIntegrationTest()
    {
        _mockAppSettings = new Mock<IOptions<AppSettings>>();
        _mockAppSettings.Setup(x => x.Value).Returns(new AppSettings());        
        _odbcIntegration = new SQLIntegration(_mockAppSettings.Object);       
    }

    [Fact]
    public async Task GetAuthenticationAsync_ShouldReturnOdbcConnection_WhenAuthenticationDetailsAreValid()
    {
        // Arrange
        var odbcAuth = new SQLAuthentication
        {
            Driver = "ODBC Driver 17 for SQL Server",
            Server = "localhost",
            Database = "TestDb",
            UID = "TestUser",
            PWD = "TestPassword"
        };
        #pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        var config = new AppConfig
        {
            UserURIs = null,
            UserAttributeSchemas = null,
            IntegrationMethodOutbound = null,
            AuthenticationDetails = odbcAuth
        };
        #pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

        // Mock IOptions<AppSettings>
        var mockAppSettings = new Mock<IOptions<AppSettings>>();
        mockAppSettings.Setup(ap => ap.Value).Returns(new AppSettings());

        var expectedConnectionString = $"Driver={odbcAuth.Driver};Server={odbcAuth.Server};Database={odbcAuth.Database};Uid={odbcAuth.UID};Pwd={odbcAuth.PWD};";

        // Call the GetAuthenticationAsync method to get the connection wrapped in the wrapper
        var connection = await new SQLIntegration(mockAppSettings.Object).GetAuthenticationAsync(config, SCIMDirections.Outbound, CancellationToken.None);

        // Verify that the connection string
        Assert.Equal(expectedConnectionString, connection.ConnectionString);
    }

    [Fact]
    public async Task GetAuthenticationAsync_ShouldThrowException_WhenAuthenticationDetailsAreInvalid()
    {
        // Arrange
        #pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        var config = new AppConfig
        {
            UserURIs = null,
            UserAttributeSchemas = null,
            IntegrationMethodOutbound = null,
            AuthenticationDetails = null
        };
        #pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

        // Act
        async Task Act() => await _odbcIntegration.GetAuthenticationAsync(config, SCIMDirections.Outbound, CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(Act);
    }

    [Fact]
    public async Task GetAuthenticationAsync_ShouldThrowException_WhenAuthenticationDetailsAreMissing()
    {
        // Arrange
        #pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        var config = new AppConfig
        {
            UserURIs = null,
            UserAttributeSchemas = null,
            IntegrationMethodOutbound = null,
            AuthenticationDetails = null
        };
        #pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type

        // Act
        async Task Act() => await _odbcIntegration.GetAuthenticationAsync(config, SCIMDirections.Outbound, CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(Act);
    }
}