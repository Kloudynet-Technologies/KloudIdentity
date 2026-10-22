using System.Data.Odbc;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using Microsoft.SCIM;

namespace KN.KloudIdentity.MapperTests;

public partial class SQLIntegrationTest
{
    [Theory]
    [InlineData("Output", true)]
    [InlineData("output", true)]
    [InlineData("Output:ResponseCode", true)]
    [InlineData("  OUTPUT:responsemessage ", true)]
    [InlineData("Output:RetCode", true)]
    [InlineData("OutputValue", false)]
    [InlineData("UserName", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsOutputMapping_ShouldDetectMarkerRows(string? sourceValue, bool expected)
    {
        var attribute = new AttributeSchema { SourceValue = sourceValue!, DestinationField = "@Param" };

        Assert.Equal(expected, TestableSQLIntegration.CallIsOutputMapping(attribute));
    }

    [Fact]
    public void GetOutputMappings_ShouldReturnMarkerRowsWithRoleNameAndType()
    {
        var schema = new List<AttributeSchema>
        {
            new() { SourceValue = "UserName", DestinationField = "@LoginID", DestinationType = AttributeDataTypes.String },
            new() { SourceValue = "Output:responsecode", MappingType = MappingTypes.Constant, DestinationField = "urn:kn:ki:schema:@ResponseCode", DestinationType = AttributeDataTypes.Int },
            new() { SourceValue = "Output:ResponseMessage", MappingType = MappingTypes.Direct, DestinationField = "@ResponseMessage", DestinationType = AttributeDataTypes.String },
            new() { SourceValue = "Output", MappingType = MappingTypes.Constant, DestinationField = "@NewUserId", DestinationType = AttributeDataTypes.BigInt }
        };

        var outputs = TestableSQLIntegration.CallGetOutputMappings(schema);

        Assert.Equal(3, outputs.Count);

        Assert.Equal("@ResponseCode", outputs[0].ParameterName);
        Assert.Equal("ResponseCode", outputs[0].Role);
        Assert.Equal("INT", outputs[0].SqlType);

        Assert.Equal("@ResponseMessage", outputs[1].ParameterName);
        Assert.Equal("ResponseMessage", outputs[1].Role);
        Assert.Equal("NVARCHAR(4000)", outputs[1].SqlType);

        Assert.Equal("@NewUserId", outputs[2].ParameterName);
        Assert.Null(outputs[2].Role);
        Assert.Equal("BIGINT", outputs[2].SqlType);
    }

    [Theory]
    [InlineData(AttributeDataTypes.String, "NVARCHAR(4000)")]
    [InlineData(AttributeDataTypes.NVarChar, "NVARCHAR(4000)")]
    [InlineData(AttributeDataTypes.Int, "INT")]
    [InlineData(AttributeDataTypes.Number, "BIGINT")]
    [InlineData(AttributeDataTypes.BigInt, "BIGINT")]
    [InlineData(AttributeDataTypes.Boolean, "BIT")]
    [InlineData(AttributeDataTypes.Bit, "BIT")]
    [InlineData(AttributeDataTypes.DateTime, "DATETIME2")]
    [InlineData(AttributeDataTypes.Decimal, "DECIMAL(18,2)")]
    public void ToSqlDeclarationType_ShouldMapDestinationType(AttributeDataTypes dataType, string expected)
    {
        Assert.Equal(expected, TestableSQLIntegration.CallToSqlDeclarationType(dataType));
    }

    [Fact]
    public void ToSqlDeclarationType_ShouldThrow_ForBinaryTypes()
    {
        Assert.Throws<NotSupportedException>(() => TestableSQLIntegration.CallToSqlDeclarationType(AttributeDataTypes.VarBinary));
    }

    [Fact]
    public async Task MapAndPreparePayloadAsync_ShouldSkipOutputMarkerRows()
    {
        var schema = new List<AttributeSchema>
        {
            new() { SourceValue = "DisplayName", MappingType = MappingTypes.Direct, DestinationField = "@Name", DestinationType = AttributeDataTypes.String },
            new() { SourceValue = "Output:ResponseCode", MappingType = MappingTypes.Constant, DestinationField = "@ResponseCode", DestinationType = AttributeDataTypes.Int },
            new() { SourceValue = "Output:ResponseMessage", MappingType = MappingTypes.Direct, DestinationField = "@ResponseMessage", DestinationType = AttributeDataTypes.String }
        };
        var resource = new Core2EnterpriseUser { DisplayName = "Test User" };

        var result = await _odbcIntegration.MapAndPreparePayloadAsync(schema, resource);

        var parameters = Assert.IsType<List<OdbcParameter>>(result);
        var parameter = Assert.Single(parameters);
        Assert.Equal("@Name", parameter.ParameterName);
        Assert.Equal("Test User", parameter.Value);
    }
}
