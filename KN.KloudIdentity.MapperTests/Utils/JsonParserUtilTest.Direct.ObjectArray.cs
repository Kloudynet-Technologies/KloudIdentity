using KN.KloudIdentity.Mapper;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.MapperTests;

public partial class JSONParserUtilTests
{
    [Fact]
    public void ObjectArray()
    {
        // Arrange
        var AttributeSchemas = new List<AttributeSchema>
            {
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "Identifier",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:users",
                    IsRequired = true,
                    DestinationType = JsonDataTypes.Array,
                    ArrayDataType = JsonDataTypes.Object,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always },
                    ChildSchemas = new List<AttributeSchema>
                    {
                        new AttributeSchema
                        {
                            MappingType = MappingTypes.Direct,
                            SourceValue = "Identifier",
                            DefaultValue = "N/A",
                            DestinationField = "urn:kn:ki:schema:id",
                            IsRequired = true,
                            DestinationType = JsonDataTypes.String,
                            MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                        },
                        new AttributeSchema
                        {
                            MappingType = MappingTypes.Direct,
                            SourceValue = "EnterpriseExtension:Manager:Value",
                            DefaultValue = "N/A",
                            DestinationField = "urn:kn:ki:schema:name",
                            IsRequired = true,
                            DestinationType = JsonDataTypes.String,
                            MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                        },
                        new AttributeSchema
                        {
                            MappingType = MappingTypes.Direct,
                            SourceValue = "EnterpriseExtension:EmployeeNumber",
                            DefaultValue = "N/A",
                            DestinationField = "urn:kn:ki:schema:employeeNumber",
                            IsRequired = true,
                            DestinationType = JsonDataTypes.String,
                            MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                        },
                        new AttributeSchema
                        {
                            MappingType = MappingTypes.Constant,
                            SourceValue = "123568",
                            DefaultValue = "N/A",
                            DestinationField = "urn:kn:ki:schema:role",
                            IsRequired = true,
                            DestinationType = JsonDataTypes.String,
                            MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                        }
                    }
                }
            };

        var resource = new Core2EnterpriseUser
        {
            Identifier = "001",
            EnterpriseExtension = new ExtensionAttributeEnterpriseUser2
            {
                Manager = new Manager
                {
                    Value = "John Doe"
                },
                EmployeeNumber = "123"
            }
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""users"": [
                    {
                        ""id"": 1,
                        ""name"": ""John Doe"",
                        ""employeeNumber"": ""123"",
                        ""role"": ""123568""
                    }
                ]
            }");

        Assert.Equal(expectedJson, result);
    }
}
