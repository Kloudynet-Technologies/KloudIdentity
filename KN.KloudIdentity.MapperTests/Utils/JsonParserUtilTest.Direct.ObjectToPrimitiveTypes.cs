using KN.KloudIdentity.Mapper;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.MapperTests;

public partial class JSONParserUtilTests
{
    [Fact]
    public void ObjectToStringAttribute()
    {
        // Arrange
        var AttributeSchemas = new List<AttributeSchema>
            {
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "Identifier",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:id",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.Number,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "EnterpriseExtension:Manager:Value",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:manager",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.String,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "EnterpriseExtension:EmployeeNumber",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:employeeNumber",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.String,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
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
                ""id"": 1,
                ""manager"": ""John Doe"",
                ""employeeNumber"": ""123""
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void ObjectToIntegerAttribute()
    {
        // Arrange
        var AttributeSchemas = new List<AttributeSchema>
            {
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "Identifier",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:id",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.Number,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "EnterpriseExtension:Manager:Value",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:manager",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.String,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "EnterpriseExtension:EmployeeNumber",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:employeeNumber",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.Number,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
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
                ""id"": 1,
                ""manager"": ""John Doe"",
                ""employeeNumber"": 123
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void ObjectToStringAttribute_DefaultValue()
    {
        // Arrange
        var AttributeSchemas = new List<AttributeSchema>
            {
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "Identifier",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:id",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.Number,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "EnterpriseExtension:Manager:Value",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:manager",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.String,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "EnterpriseExtension:EmployeeNumber",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:employeeNumber",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.String,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                }
            };

        var resource = new Core2EnterpriseUser
        {
            Identifier = "001",
            EnterpriseExtension = new ExtensionAttributeEnterpriseUser2
            {
                Manager = new Manager
                {
                    Value = ""
                },
                EmployeeNumber = ""
            }
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": 1,
                ""manager"": ""N/A"",
                ""employeeNumber"": ""N/A""
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void ObjectToIntegerAttribute_DefaultValue()
    {
        // Arrange
        var AttributeSchemas = new List<AttributeSchema>
            {
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "Identifier",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:id",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.Number,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "EnterpriseExtension:Manager:Value",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:manager",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.String,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "EnterpriseExtension:EmployeeNumber",
                    DefaultValue = "999",
                    DestinationField = "urn:kn:ki:schema:employeeNumber",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.Number,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                }
            };

        var resource = new Core2EnterpriseUser
        {
            Identifier = "001",
            EnterpriseExtension = new ExtensionAttributeEnterpriseUser2
            {
                Manager = new Manager
                {
                    Value = ""
                },
                EmployeeNumber = ""
            }
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": 1,
                ""manager"": ""N/A"",
                ""employeeNumber"": 999
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void ObjectToStringAttribute_EmptyStr()
    {
        // Arrange
        var AttributeSchemas = new List<AttributeSchema>
            {
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "Identifier",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:id",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.Number,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "EnterpriseExtension:Manager:Value",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:manager",
                    IsRequired = false,
                    DestinationType = AttributeDataTypes.String,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "EnterpriseExtension:EmployeeNumber",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:employeeNumber",
                    IsRequired = false,
                    DestinationType = AttributeDataTypes.String,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                }
            };

        var resource = new Core2EnterpriseUser
        {
            Identifier = "001",
            EnterpriseExtension = new ExtensionAttributeEnterpriseUser2
            {
                Manager = new Manager
                {
                    Value = ""
                },
                EmployeeNumber = ""
            }
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": 1,
                ""manager"": null,
                ""employeeNumber"": null
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void ObjectToIntegerAttribute_EmptyStr()
    {
        // Arrange
        var AttributeSchemas = new List<AttributeSchema>
            {
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "Identifier",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:id",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.Number,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "EnterpriseExtension:Manager:Value",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:manager",
                    IsRequired = false,
                    DestinationType = AttributeDataTypes.String,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "EnterpriseExtension:EmployeeNumber",
                    DefaultValue = "999",
                    DestinationField = "urn:kn:ki:schema:employeeNumber",
                    IsRequired = false,
                    DestinationType = AttributeDataTypes.Number,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                }
            };

        var resource = new Core2EnterpriseUser
        {
            Identifier = "001",
            EnterpriseExtension = new ExtensionAttributeEnterpriseUser2
            {
                Manager = new Manager
                {
                    Value = ""
                },
                EmployeeNumber = ""
            }
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": 1,
                ""manager"": null,
                ""employeeNumber"": 0
            }");

        Assert.Equal(expectedJson, result);
    }
}
