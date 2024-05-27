using KN.KloudIdentity.Mapper;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.MapperTests;

public partial class JSONParserUtilTests
{
    [Fact]
    public void OneToOneStringAttribute()
    {
        // Arrange
        var AttributeSchemas = new List<AttributeSchema>
            {
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "DisplayName",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:name",
                    IsRequired = true,
                    DestinationType = JsonDataTypes.String,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "UserName",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:email",
                    IsRequired = true,
                    DestinationType = JsonDataTypes.String,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                }
            };

        var resource = new Core2EnterpriseUser { UserName = "john.d@mail.com", DisplayName = "John Doe" };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""name"": ""John Doe"",
                ""email"": ""john.d@mail.com""
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void OneToOneIntegerAttribute()
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
                    DestinationType = JsonDataTypes.Number,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                }
            };

        var resource = new Core2EnterpriseUser { Identifier = "25" };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": 25
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void OneToOneBooleanAttribute()
    {
        // Arrange
        var AttributeSchemas = new List<AttributeSchema>
            {
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "Active",
                    DefaultValue = "false",
                    DestinationField = "urn:kn:ki:schema:active",
                    IsRequired = true,
                    DestinationType = JsonDataTypes.Boolean,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                }
            };

        var resource = new Core2EnterpriseUser { Active = true };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""active"": true
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void OneToOneStringAttribute_DefaultValue_EmptyStr()
    {
        // Arrange
        var AttributeSchemas = new List<AttributeSchema>
            {
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "DisplayName",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:name",
                    IsRequired = true,
                    DestinationType = JsonDataTypes.String,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "UserName",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:email",
                    IsRequired = true,
                    DestinationType = JsonDataTypes.String,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                }
            };

        var resource = new Core2EnterpriseUser { UserName = "", DisplayName = "" };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""name"": ""N/A"",
                ""email"": ""N/A""
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void OneToOneStringAttribute_DefaultValue_Null()
    {
        // Arrange
        var AttributeSchemas = new List<AttributeSchema>
            {
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "DisplayName",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:name",
                    IsRequired = true,
                    DestinationType = JsonDataTypes.String,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "UserName",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:email",
                    IsRequired = true,
                    DestinationType = JsonDataTypes.String,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                }
            };

        var resource = new Core2EnterpriseUser { UserName = null, DisplayName = null };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""name"": ""N/A"",
                ""email"": ""N/A""
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void OneToOneStringAttribute_EmptyStr()
    {
        // Arrange
        var AttributeSchemas = new List<AttributeSchema>
            {
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "DisplayName",
                    DefaultValue = "",
                    DestinationField = "urn:kn:ki:schema:name",
                    IsRequired = false,
                    DestinationType = JsonDataTypes.String,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "UserName",
                    DefaultValue = "",
                    DestinationField = "urn:kn:ki:schema:email",
                    IsRequired = false,
                    DestinationType = JsonDataTypes.String,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                }
            };

        var resource = new Core2EnterpriseUser { UserName = "", DisplayName = "" };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""name"": null,
                ""email"": null
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void OneToOneIntegerAttribute_DefaultValue_EmptyStr()
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
                    DestinationType = JsonDataTypes.Number,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "Locale",
                    DefaultValue = "9999",
                    DestinationField = "urn:kn:ki:schema:locale",
                    IsRequired = true,
                    DestinationType = JsonDataTypes.Number,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                }
            };

        var resource = new Core2EnterpriseUser { Identifier = "", Locale = "" };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": 999,
                ""locale"": 9999
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void OneToOneIntegerAttribute_DefaultValue_Null()
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
                    DestinationType = JsonDataTypes.Number,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "Locale",
                    DefaultValue = "9999",
                    DestinationField = "urn:kn:ki:schema:locale",
                    IsRequired = true,
                    DestinationType = JsonDataTypes.Number,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                }
            };

        var resource = new Core2EnterpriseUser { Identifier = null, Locale = null };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": 999,
                ""locale"": 9999
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void OneToOneIntegerAttribute_EmptyStr()
    {
        // Arrange
        var AttributeSchemas = new List<AttributeSchema>
            {
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "Identifier",
                    DefaultValue = "",
                    DestinationField = "urn:kn:ki:schema:id",
                    IsRequired = false,
                    DestinationType = JsonDataTypes.Number,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "Locale",
                    DefaultValue = "",
                    DestinationField = "urn:kn:ki:schema:locale",
                    IsRequired = false,
                    DestinationType = JsonDataTypes.Number,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                }
            };

        var resource = new Core2EnterpriseUser { Identifier = "", Locale = "" };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": 0,
                ""locale"": 0
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void OneToOneBooleanAttribute_DefaultValue_EmptyStr()
    {
        // Arrange
        var AttributeSchemas = new List<AttributeSchema>
            {
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "Active",
                    DefaultValue = "False",
                    DestinationField = "urn:kn:ki:schema:isActive",
                    IsRequired = true,
                    DestinationType = JsonDataTypes.Boolean,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "Locale",
                    DefaultValue = "9999",
                    DestinationField = "urn:kn:ki:schema:locale",
                    IsRequired = true,
                    DestinationType = JsonDataTypes.Number,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                }
            };

        var resource = new Core2EnterpriseUser { Identifier = "", Locale = "" };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""isActive"": false,
                ""locale"": 9999
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void OneToOneBooleanAttribute_EmptyStr()
    {
        // Arrange
        var AttributeSchemas = new List<AttributeSchema>
            {
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "Active",
                    DefaultValue = "",
                    DestinationField = "urn:kn:ki:schema:isActive",
                    IsRequired = false,
                    DestinationType = JsonDataTypes.Boolean,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "Locale",
                    DefaultValue = "9999",
                    DestinationField = "urn:kn:ki:schema:locale",
                    IsRequired = true,
                    DestinationType = JsonDataTypes.Number,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                }
            };

        var resource = new Core2EnterpriseUser { Identifier = "", Locale = "" };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""isActive"": false,
                ""locale"": 9999
            }");

        Assert.Equal(expectedJson, result);
    }
}
