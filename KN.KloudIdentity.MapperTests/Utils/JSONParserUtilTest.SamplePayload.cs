using KN.KloudIdentity.Mapper;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.MapperTests;

public partial class JSONParserUtilTests
{
    [Fact]
    public void Parse_SimpleObject_ReturnsExpectedJson_SamplePayload()
    {
        // Arrange
        var AttributeSchemas = new List<AttributeSchema>
            {
                new AttributeSchema { DestinationField = "urn:kn:ki:schema:id", SourceValue = "Identifier", DestinationType = AttributeDataTypes.Number },
                new AttributeSchema { DestinationField = "urn:kn:ki:schema:name", SourceValue = "DisplayName", DestinationType = AttributeDataTypes.String },
                new AttributeSchema { DestinationField = "urn:kn:ki:schema:email", SourceValue = "UserName", DestinationType = AttributeDataTypes.String},
                new AttributeSchema { DestinationField = "urn:kn:ki:schema:phone", SourceValue = "PhoneNumber", DestinationType = AttributeDataTypes.String},
                new AttributeSchema { DestinationField = "urn:kn:ki:schema:active", SourceValue = "Active", DestinationType = AttributeDataTypes.Boolean},
            };
        var resource = new Core2EnterpriseUser { };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource, true);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": 0,
                ""name"": ""string"",
                ""email"": ""string"",
                ""phone"": ""string"",
                ""active"": false
            }");
        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void ObjectArray_SamplePayload()
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
                    DestinationType = AttributeDataTypes.Array,
                    ArrayDataType = AttributeDataTypes.Object,
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
                            DestinationType = AttributeDataTypes.String,
                            MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                        },
                        new AttributeSchema
                        {
                            MappingType = MappingTypes.Direct,
                            SourceValue = "EnterpriseExtension:Manager:Value",
                            DefaultValue = "N/A",
                            DestinationField = "urn:kn:ki:schema:name",
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
                        },
                        new AttributeSchema
                        {
                            MappingType = MappingTypes.Constant,
                            SourceValue = "123568",
                            DefaultValue = "N/A",
                            DestinationField = "urn:kn:ki:schema:role",
                            IsRequired = true,
                            DestinationType = AttributeDataTypes.String,
                            MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                        }
                    }
                }
            };

        var resource = new Core2EnterpriseUser
        {
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource, true);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""users"": [
                    {
                        ""id"": ""N/A"",
                        ""name"": ""N/A"",
                        ""employeeNumber"": ""N/A"",
                        ""role"": ""123568""
                    }
                ]
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void ObjectToPrimitiveTypes_SamplePayload()
    {
        // Arrange
        var AttributeSchemas = new List<AttributeSchema>
            {
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "Identifier",
                    DefaultValue = "999",
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
                    SourceValue = "Active",
                    DefaultValue = "false",
                    DestinationField = "urn:kn:ki:schema:isActive",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.Boolean,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                }
            };

        var resource = new Core2EnterpriseUser
        {
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource, true);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": 999,
                ""manager"": ""N/A"",
                ""isActive"": false
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void PrimitiveToStringArrayAttribute_SamplePayload()
    {
        // Arrange
        var AttributeSchemas = new List<AttributeSchema>
            {
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "Identifier",
                    DefaultValue = "999",
                    DestinationField = "urn:kn:ki:schema:id",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.Number,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "UserName",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:usernames",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.Array,
                    ArrayDataType = AttributeDataTypes.String,
                    ArrayElementFieldName = "UserName",
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                }
            };

        var resource = new Core2EnterpriseUser
        {
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource, true);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": 999,
                ""usernames"": [""N/A""]
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void ArrayToStringArrayAttribute_SamplePayload()
    {
        // Arrange
        var AttributeSchemas = new List<AttributeSchema>
            {
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "Identifier",
                    DefaultValue = "0",
                    DestinationField = "urn:kn:ki:schema:id",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.Number,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "Addresses",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:streetAddresses",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.Array,
                    ArrayDataType = AttributeDataTypes.String,
                    ArrayElementFieldName = "urn:kn:ki:schema:addresses:StreetAddress",
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                }
            };

        var resource = new Core2EnterpriseUser
        {
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource, true);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": 0,
                ""streetAddresses"": [""N/A""]
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void PrimitiveStringToObjectType_SamplePayload()
    {
        // Arrange
        var AttributeSchemas = new List<AttributeSchema>
            {
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "Identifier",
                    DefaultValue = "835",
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
                    DestinationField = "urn:kn:ki:schema:person",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.Object,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always },
                    ChildSchemas = new List<AttributeSchema>
                    {
                        new AttributeSchema
                        {
                            MappingType = MappingTypes.Direct,
                            SourceValue = "DisplayName",
                            DefaultValue = "N/A",
                            DestinationField = "urn:kn:ki:schema:displayName",
                            IsRequired = true,
                            DestinationType = AttributeDataTypes.String,
                            MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                        },
                        new AttributeSchema
                        {
                            MappingType = MappingTypes.Direct,
                            SourceValue = "Title",
                            DefaultValue = "N/A",
                            DestinationField = "urn:kn:ki:schema:title",
                            IsRequired = true,
                            DestinationType = AttributeDataTypes.String,
                            MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                        },
                        new AttributeSchema
                        {
                            MappingType = MappingTypes.Direct,
                            SourceValue = "Nickname",
                            DefaultValue = "N/A",
                            DestinationField = "urn:kn:ki:schema:nickname",
                            IsRequired = true,
                            DestinationType = AttributeDataTypes.String,
                            MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                        },
                        new AttributeSchema
                        {
                            MappingType = MappingTypes.Direct,
                            SourceValue = "Active",
                            DefaultValue = "true",
                            DestinationField = "urn:kn:ki:schema:isActive",
                            IsRequired = true,
                            DestinationType = AttributeDataTypes.Boolean,
                            MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                        },
                    }
                }
            };

        var resource = new Core2EnterpriseUser
        {
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource, true);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": 835,
                ""person"": {
                    ""displayName"": ""N/A"",
                    ""title"": ""N/A"",
                    ""nickname"": ""N/A"",
                    ""isActive"": false
                }
            }");

        Assert.Equal(expectedJson, result);
    }
}
