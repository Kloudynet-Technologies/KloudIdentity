using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Domain.SQL;
using Newtonsoft.Json;

namespace KN.KloudIdentity.MapperTests;

public partial class SQLIntegrationTest
{
    [Fact]
    public async Task DeleteAsync_ShouldThrowHttpRequestException_WhenIdentifierIsNull()
    {
        // Arrange
        var identifier = string.Empty;

        var integrationDetails = JsonConvert.SerializeObject(new SQLIntegrationDetails
        {
            Id = Guid.NewGuid(),
            AppId = "TestApp",
            PostSpName = "SP_CreateUser",
            GetSpName = "SP_GetUser",
            PatchSpName = "SP_UpdateUser",
            DeleteSpName = "SP_DeleteUser"
        });

        #pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        var appConfig = new AppConfig
        {
            UserURIs = null,
            UserAttributeSchemas = null,
            IntegrationMethodOutbound = null,
            AuthenticationDetails = null,
            IntegrationDetails = integrationDetails
        };
        #pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.

        var correlationId = Guid.NewGuid().ToString();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _odbcIntegration.DeleteAsync(identifier, appConfig, correlationId));
    }

    [Fact]
    public async Task DeleteAsync_ShouldThrowHttpRequestException_WhenODBCConnectionIsNull()
    {
        // Arrange
        var identifier = "test-identifier";

        // Arrange
        var attributeSchemas = new List<AttributeSchema>
        {
            new AttributeSchema { DestinationField = "UserId", SourceValue = "Identifier" },
            new AttributeSchema { DestinationField = "Username", SourceValue = "UserName" },
            new AttributeSchema { DestinationField = "Email", SourceValue = "ElectronicMailAddresses[0]:Value" },
            new AttributeSchema { DestinationField = "FirstName", SourceValue = "Name:GivenName" }
        };

        var integrationDetails = JsonConvert.SerializeObject(new SQLIntegrationDetails
        {
            Id = Guid.NewGuid(),
            AppId = "TestApp",
            PostSpName = "SP_CreateUser",
            GetSpName = "SP_GetUser",
            PatchSpName = "SP_UpdateUser",
            DeleteSpName = "SP_DeleteUser"
        });

        #pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        var appConfig = new AppConfig
        {
            UserURIs = null,
            UserAttributeSchemas = attributeSchemas,
            IntegrationMethodOutbound = null,
            AuthenticationDetails = null,
            IntegrationDetails = integrationDetails
        };
        #pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.

        var correlationId = Guid.NewGuid().ToString();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _odbcIntegration.DeleteAsync(identifier, appConfig, correlationId));
    }

    [Fact]
    public async Task DeleteAsync_ShouldThrowHttpRequestException_WhenStoredProcedureNameIsMissing()
    {
        // Arrange
        var identifier = "test-identifier";

        // Arrange
        var attributeSchemas = new List<AttributeSchema>
        {
            new AttributeSchema { DestinationField = "UserId", SourceValue = "Identifier" },
            new AttributeSchema { DestinationField = "Username", SourceValue = "UserName" },
            new AttributeSchema { DestinationField = "Email", SourceValue = "ElectronicMailAddresses[0]:Value" },
            new AttributeSchema { DestinationField = "FirstName", SourceValue = "Name:GivenName" }
        };

        var integrationDetails = JsonConvert.SerializeObject(new SQLIntegrationDetails
        {
            Id = Guid.NewGuid(),
            AppId = "TestApp",
            PostSpName = "SP_CreateUser",
            GetSpName = "SP_GetUser",
            PatchSpName = "SP_UpdateUser",
            DeleteSpName = "SP_DeleteUser"
        });

        #pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        var appConfig = new AppConfig
        {
            UserURIs = null,
            UserAttributeSchemas = attributeSchemas,
            IntegrationMethodOutbound = null,
            AuthenticationDetails = null,
            IntegrationDetails = integrationDetails
        };
        #pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

        var correlationId = Guid.NewGuid().ToString();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _odbcIntegration.DeleteAsync(identifier, appConfig, correlationId));
    }

    [Fact]
    public async Task DeleteAsync_ShouldThrowHttpRequestException_WhenMatchingAttributeNotFound()
    {
        // Arrange
        var identifier = "test-identifier";

        // Arrange
        var attributeSchemas = new List<AttributeSchema>       {
            
            new AttributeSchema { DestinationField = "Username", SourceValue = "UserName" },
            new AttributeSchema { DestinationField = "Email", SourceValue = "ElectronicMailAddresses[0]:Value" },
            new AttributeSchema { DestinationField = "FirstName", SourceValue = "Name:GivenName" }
        };

        var integrationDetails = JsonConvert.SerializeObject(new SQLIntegrationDetails
        {
            Id = Guid.NewGuid(),
            AppId = "TestApp",
            PostSpName = "SP_CreateUser",
            GetSpName = "SP_GetUser",
            PatchSpName = "SP_UpdateUser",
            DeleteSpName = "SP_DeleteUser"
        });

        var appConfig = new AppConfig
        {
            UserURIs = null,
            UserAttributeSchemas = attributeSchemas,
            IntegrationMethodOutbound = null,
            AuthenticationDetails = null,
            IntegrationDetails = integrationDetails
        };

        var correlationId = Guid.NewGuid().ToString();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _odbcIntegration.DeleteAsync(identifier, appConfig, correlationId));
    }
}