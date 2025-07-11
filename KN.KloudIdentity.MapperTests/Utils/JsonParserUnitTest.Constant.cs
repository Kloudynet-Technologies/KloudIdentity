using KN.KloudIdentity.Mapper;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.MapperTests;

public partial class JSONParserUtilTests
{
    [Fact]
    public void ConstantToSurfaceLevelAttribute()
    {
        // Arrange
        var AttributeSchemas = new List<AttributeSchema>
            {
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "Identifier",
                    DefaultValue = "1",
                    DestinationField = "urn:kn:ki:schema:id",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.Number,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "DisplayName",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:name",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.String,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },
                new AttributeSchema
                {
                    MappingType = MappingTypes.Constant,
                    SourceValue = "Dept-1",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:department",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.String,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },
                new AttributeSchema
                {
                    MappingType = MappingTypes.Constant,
                    SourceValue = "Custome Value",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:CustomeAtt",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.String,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                }
            };

        var resource = new Core2EnterpriseUser { Identifier = "1", DisplayName = "John Doe" };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": 1,
                ""name"": ""John Doe"",
                ""department"": ""Dept-1"",
                ""CustomeAtt"": ""Custome Value""
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void ConstantToNestedAttributes()
    {
        // Arrange
        var AttributeSchemas = new List<AttributeSchema>
            {
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "Identifier",
                    DefaultValue = "1",
                    DestinationField = "urn:kn:ki:schema:id",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.Number,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "DisplayName",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:name",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.String,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },
                new AttributeSchema
                {
                    MappingType = MappingTypes.Constant,
                    SourceValue = "Dept-1",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:const:department",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.String,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },
                new AttributeSchema
                {
                    MappingType = MappingTypes.Constant,
                    SourceValue = "Custome Value",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:const:CustomeAtt",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.String,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                }
            };

        var resource = new Core2EnterpriseUser { Identifier = "1", DisplayName = "John Doe" };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": 1,
                ""name"": ""John Doe"",
                ""const"": {
                    ""department"": ""Dept-1"",
                    ""CustomeAtt"": ""Custome Value""
                }
            }");

        Assert.Equal(expectedJson, result);
    }
}
