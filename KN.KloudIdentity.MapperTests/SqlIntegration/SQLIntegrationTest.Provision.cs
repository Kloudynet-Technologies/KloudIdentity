using System.Data.Odbc;
using System.Data;
using KN.KloudIdentity.Mapper.Domain.Application;
using Newtonsoft.Json;
using KN.KloudIdentity.Mapper.Domain.SQL;

namespace KN.KloudIdentity.MapperTests;

public partial class SQLIntegrationTest
{

    [Fact]
    public async Task ProvisionAsync_ShouldThrowException_WhenPayloadIsNull()
    {
        // Arrange
        #pragma warning disable CS8600 // Possible null reference argument.
        object payload = null;
        #pragma warning disable CS8600 // Possible null reference argument.

        var integrationDetails = new
        {
            PostSpName = "SP_CreateUser",
            GetSpName = "SP_GetUser",
            PatchSpName = "SP_UpdateUser",
        };

        var appConfig = new AppConfig
        {
            #pragma warning disable CS8625
            UserURIs = null,
            UserAttributeSchemas = null,
            IntegrationMethodOutbound = null,
            AuthenticationDetails = null,
            IntegrationDetails = integrationDetails
            #pragma warning disable CS8625
        };

        var correlationId = Guid.NewGuid().ToString();

        // Act & Assert
        #pragma warning disable CS8604 // Possible null reference argument.
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _odbcIntegration.ProvisionAsync(payload, appConfig, correlationId));
        #pragma warning restore CS8604 // Possible null reference argument.
    }

    [Fact]
    public async Task ProvisionAsync_ShouldThrowException_WhenPayloadIsInvalid()
    {
        // Arrange   

        var payload = new
        {
            TestPayload = "TestPayload"
        };

        var integrationDetails = new
        {
            PostSpName = "SP_CreateUser",
            GetSpName = "SP_GetUser",
            PatchSpName = "SP_UpdateUser",
        };

        var appConfig = new AppConfig
        {
            UserURIs = null,
            UserAttributeSchemas = null,
            IntegrationMethodOutbound = null,
            AuthenticationDetails = null,
            IntegrationDetails = integrationDetails
        };

        var correlationId = Guid.NewGuid().ToString();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _odbcIntegration.ProvisionAsync(payload, appConfig, correlationId));
    }

    [Fact]
    public async Task ProvisionAsync_ShouldThrowException_WhenPostSpNameIsnull()
    {
        // Arrange
        var payload = new List<OdbcParameter>
        {
            new OdbcParameter("@FirstName", DbType.String) { Value = "John" },
            new OdbcParameter("@LastName", DbType.String) { Value = "Doe" },
            new OdbcParameter("@Age", DbType.Int32) { Value = 30 },
            new OdbcParameter("@IsActive", DbType.Boolean) { Value = true },
            new OdbcParameter("@JoinDate", DbType.DateTime) { Value = DateTime.UtcNow }
        };

        // Use dynamic for IntegrationDetails

        var integrationDetails = JsonConvert.SerializeObject(new SQLIntegrationDetails
        {
            Id = Guid.NewGuid(),
            AppId = "TestApp",
            PostSpName = null,
            GetSpName = "SP_GetUser",
            PatchSpName = "SP_UpdateUser",
            DeleteSpName = "SP_DeleteUser"
        });

        var appConfig = new AppConfig
        {
            UserURIs = null,
            UserAttributeSchemas = null,
            IntegrationMethodOutbound = null,
            AuthenticationDetails = null,
            IntegrationDetails = integrationDetails
        };

        var correlationId = Guid.NewGuid().ToString();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _odbcIntegration.ProvisionAsync(payload, appConfig, correlationId));
    }   
}