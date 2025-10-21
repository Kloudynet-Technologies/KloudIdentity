using System.Data.Odbc;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using Microsoft.SCIM;

namespace KN.KloudIdentity.MapperTests;

public partial class SQLIntegrationTest
{
    [Fact]
    public async Task MapAndPreparePayloadAsync_ShouldThrowHttpRequestException_WhenSchemaIsInvalid()
    {
        // Arrange   
        var invalidSchema = new List<AttributeSchema>();
        var resource = new Core2EnterpriseUser();

        // Act
        async Task Act() => await _odbcIntegration.MapAndPreparePayloadAsync(invalidSchema, resource, CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<HttpRequestException>(Act);
    }

    [Fact]
    public async Task MapAndPreparePayloadAsync_ShouldThrowHttpRequestException_WhenSchemaIsValidButEmpty()
    {
        // Arrange
        var validSchema = new List<AttributeSchema>();
        var resource = new Core2EnterpriseUser();

        // Act
        async Task Act() => await _odbcIntegration.MapAndPreparePayloadAsync(validSchema, resource, CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<HttpRequestException>(Act);
    }

    [Fact]
    public async Task MapAndPreparePayloadAsync_ValidSchemaWithNullValues_ReturnsOdbcParameters()
    {
        // Arrange
        var schema = new List<AttributeSchema>
        {
            new AttributeSchema { SourceValue = "UserName", DestinationField = "Email", DestinationType = AttributeDataTypes.String },
            new AttributeSchema { SourceValue = "DisplayName", DestinationField = "Name" , DestinationType = AttributeDataTypes.String}
        };
        var resource = new Core2EnterpriseUser
        {
            UserName = null,
            ElectronicMailAddresses = new List<ElectronicMailAddress>
            {
                new ElectronicMailAddress { Value = null }
            }
        };

        // Act
        var result = await _odbcIntegration.MapAndPreparePayloadAsync(schema, resource);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<List<OdbcParameter>>(result);

        var parameters = result as List<OdbcParameter>;

#pragma warning disable CS8602
        Assert.Equal("Email", parameters[0].ParameterName);
        Assert.Equal("Name", parameters[1].ParameterName);
    }

    [Fact]
    public async Task MapAndPreparePayloadAsync_NVarCharNCharDataType_ReturnsOdbcParameters()
    {
        // Arrange
        var schema = new List<AttributeSchema>
        {
            new AttributeSchema { SourceValue = "UserName", DestinationField = "Email", DestinationType = AttributeDataTypes.NVarChar },
            new AttributeSchema { SourceValue = "DisplayName", DestinationField = "Name", DestinationType = AttributeDataTypes.NChar }
        };
        var resource = new Core2EnterpriseUser { UserName = "testuser@example.com", DisplayName = "中华人民共和国" };

        // Act
        var result = await _odbcIntegration.MapAndPreparePayloadAsync(schema, resource);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<List<OdbcParameter>>(result);

        var parameters = result as List<OdbcParameter>;

#pragma warning disable CS8602
        Assert.Equal(OdbcType.NVarChar, parameters[0].OdbcType);
        Assert.Equal(OdbcType.NChar, parameters[1].OdbcType);
        Assert.Equal("testuser@example.com", parameters[0].Value);
        Assert.Equal("中华人民共和国", parameters[1].Value);
    }

    [Fact]
    public async Task MapAndPreparePayloadAsync_BooleanDataType_ReturnsOdbcParameters()
    {
        // Arrange
        var schema = new List<AttributeSchema>
        {
            new AttributeSchema { SourceValue = "Active", DestinationField = "Active", DestinationType = AttributeDataTypes.Boolean }
        };
        var resource = new Core2EnterpriseUser { Active = true };

        // Act
        var result = await _odbcIntegration.MapAndPreparePayloadAsync(schema, resource);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<List<OdbcParameter>>(result);

        var parameters = result as List<OdbcParameter>;
#pragma warning disable CS8602
        Assert.Equal(OdbcType.Bit, parameters[0].OdbcType);
        // Assert.Equal(true, parameters[0].Value);
    }


    [Fact]
    public async Task MapAndPreparePayloadAsync_IntDataType_ReturnsOdbcParameters()
    {
        // Arrange
        var schema = new List<AttributeSchema>
        {
            new AttributeSchema { SourceValue = "Identifier", DestinationField = "Id", DestinationType = AttributeDataTypes.Int }
        };
        var resource = new Core2EnterpriseUser { Identifier = "1" };

        // Act
        var result = await _odbcIntegration.MapAndPreparePayloadAsync(schema, resource);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<List<OdbcParameter>>(result);

        var parameters = result as List<OdbcParameter>;
        Assert.Equal("Id", parameters[0].ParameterName);
        Assert.Equal(OdbcType.Int, parameters[0].OdbcType);

        // Accept both int and long
        Assert.True(Convert.ToInt64(parameters[0].Value) == 1);
    }

    [Fact]
    public async Task MapAndPreparePayloadAsync_DateDataType_ReturnsOdbcParameters()
    {
        // Arrange
        var schema = new List<AttributeSchema>
        {
            new AttributeSchema { SourceValue = "Locale", DestinationField = "JoinedDate", DestinationType = AttributeDataTypes.DateTime }
        };
        var resource = new Core2EnterpriseUser { Locale = new DateTime(2025, 1, 22, 14, 33, 12).ToString() };

        // Act
        var result = await _odbcIntegration.MapAndPreparePayloadAsync(schema, resource);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<List<OdbcParameter>>(result);

        var parameters = result as List<OdbcParameter>;

        Assert.Equal("JoinedDate", parameters[0].ParameterName);
        Assert.Equal(OdbcType.DateTime, parameters[0].OdbcType);

        var expectedValue = new DateTime(2025, 1, 22, 14, 33, 12);
        Assert.Equal(expectedValue, parameters[0].Value);
    }

    [Fact]
    public async Task MapAndPreparePayloadAsync_DirectMapping_ShouldReturnParameters_ForValidSchemaAnd()
    {
        // Arrange
        var schema = new List<AttributeSchema>
        {
            new AttributeSchema
            {
                SourceValue = "UserName",
                MappingType = MappingTypes.Direct,
                DestinationField = "Username",
                DestinationType = AttributeDataTypes.VarChar,
                DestinationTypeLength = 50
            }
        };

        var resource = new Core2EnterpriseUser { UserName = "testuser@example.com", DisplayName = "中华人民共和国" };


        // Act
        var result = await _odbcIntegration.MapAndPreparePayloadAsync(schema, resource);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<List<OdbcParameter>>(result);

        var parameters = result as List<OdbcParameter>;
#pragma warning disable CS8602
        Assert.Equal(OdbcType.VarChar, parameters[0].OdbcType);
        Assert.Equal(resource.UserName, parameters[0].Value);
    }

    [Fact]
    public async Task MapAndPreparePayloadAsync_ConstantMapping_ShouldReturnParameters()
    {
        // Arrange
        var schema = new List<AttributeSchema>
        {
            new AttributeSchema
            {
                MappingType = MappingTypes.Constant,
                SourceValue = "John David",
                DestinationField = "name",
                DestinationType =  AttributeDataTypes.VarChar,
                DestinationTypeLength = 100
            }
        };

        var resource = new Core2EnterpriseUser { };

        // Act
        var result = await _odbcIntegration.MapAndPreparePayloadAsync(schema, resource);

        // Assert
        Assert.NotNull(result);
        var parameters = Assert.IsType<List<OdbcParameter>>(result);

        Assert.Equal(OdbcType.VarChar, parameters[0].OdbcType);
        Assert.Equal("John David", parameters[0].Value);
    }

    [Fact]
    public async Task MapAndPreparePayloadAsync_ShouldThrowException_ForInvalidSchema()
    {
        // Arrange
        var schema = new List<AttributeSchema>(); // Invalid schema
        var resource = new Core2EnterpriseUser(); // Mock resource

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await _odbcIntegration.MapAndPreparePayloadAsync(schema, resource));
    }

    [Fact]
    public async Task MapAndPreparePayloadAsync_ShouldThrowException_ForNullResource()
    {
        // Arrange
        var schema = new List<AttributeSchema>
        {
            new AttributeSchema
            {
                SourceValue = "UserName",
                DestinationField = "Email",
                DestinationType = AttributeDataTypes.String,
                MappingType = MappingTypes.Direct
            }
        };

        // Act & Assert
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        await _odbcIntegration.MapAndPreparePayloadAsync(schema, null));
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }

    [Fact]
    public async Task MapAndPreparePayloadAsync_ShouldHandleNullOrEmptySourceValue_ForConstantMapping()
    {
        // Arrange
        var schema = new List<AttributeSchema>
        {
            new AttributeSchema
            {
                #pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
                SourceValue = null, // Null constant value
                DestinationField = "Email",
                DestinationType = AttributeDataTypes.String,
                MappingType = MappingTypes.Constant
                #pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            }
        };

        var resource = new Core2EnterpriseUser();

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await _odbcIntegration.MapAndPreparePayloadAsync(schema, resource));
    }
}