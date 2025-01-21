using KN.KloudIdentity.Mapper;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.MapperTests;

public partial class JSONParserUtilTests
{
    [Fact]
    public void PrimitiveToStringArrayAttribute()
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
            Identifier = "001",
            UserName = "johndoe@email.com"
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": 1,
                ""usernames"": [""johndoe@email.com""]
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void ArrayToStringArrayAttribute()
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
            Identifier = "001",
            Addresses = new List<Address>
            {
                new Address
                {
                    StreetAddress = "1234 Main St",
                    Locality = "Anytown",
                    Region = "TX",
                    PostalCode = "12345",
                    Country = "USA"
                },
                new Address
                {
                    StreetAddress = "5678 Elm St",
                    Locality = "Othertown",
                    Region = "TX",
                    PostalCode = "54321",
                    Country = "USA"
                }
            }
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": 1,
                ""streetAddresses"": [""1234 Main St"", ""5678 Elm St""]
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void PrimitiveToStringArrayAttribute_Empty()
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
                    SourceValue = "UserName",
                    DestinationField = "urn:kn:ki:schema:usernames",
                    IsRequired = false,
                    DestinationType = AttributeDataTypes.Array,
                    ArrayDataType = AttributeDataTypes.String,
                    ArrayElementFieldName = "urn:kn:ki:schema:usernames",
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                }
            };

        var resource = new Core2EnterpriseUser
        {
            Identifier = "001",
            UserName = ""
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": 1,
                ""username"": []
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void ArrayToStringArrayAttribute_DefaultValue()
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
            Identifier = "001",
            Addresses = new List<Address>
            {
                new Address
                {
                    StreetAddress = "1234 Main St",
                    Locality = "Anytown",
                    Region = "TX",
                    PostalCode = "12345",
                    Country = "USA"
                },
                new Address
                {
                    StreetAddress = null,
                    Locality = "Othertown",
                    Region = "TX",
                    PostalCode = "54321",
                    Country = "USA"
                }
            }
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": 1,
                ""streetAddresses"": [""1234 Main St"", ""N/A""]
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void ObjectToStringArrayAttribute()
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
                    DestinationType = AttributeDataTypes.String,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },

                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "Name:Formatted",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:Names",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.Array,
                    ArrayDataType = AttributeDataTypes.String,
                    ArrayElementFieldName = "name:formatted",
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                }
            };

        var resource = new Core2EnterpriseUser
        {
            Identifier = "001",
            Name = new Name
            {
                GivenName = "John",
                FamilyName = "Doe",
                Formatted = "John Doe"
            }

        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": ""001"",
                ""names"": [""John Doe""]
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void PrimitiveToBooleanArrayAttribute()
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
                    DestinationType = AttributeDataTypes.String,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },

                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "Active",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:IsActives",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.Array,
                    ArrayDataType = AttributeDataTypes.String,
                    ArrayElementFieldName = "Active",
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                }
            };

        var resource = new Core2EnterpriseUser
        {
            Identifier = "001",
            Active = true,
            Name = new Name
            {
                GivenName = "John",
                FamilyName = "Doe",
                Formatted = "John Doe"
            }

        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": ""001"",
                ""isActives"": [true]
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void NestedObjectToStringArrayAttribute()
    {
        // Arrange
        var AttributeSchemas = new List<AttributeSchema>
        {
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "ExternalIdentifier",
                    DestinationField = "urn:kn:ki:schema:id",
                    IsRequired = false,
                    DestinationType = AttributeDataTypes.String,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },

                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "EnterpriseExtension:Manager:Value",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:Managers",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.Array,
                    ArrayDataType = AttributeDataTypes.String,
                    ArrayElementFieldName = "EnterpriseExtension:Manager:Value",
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                }
            };

        var resource = new Core2EnterpriseUser
        {
            ExternalIdentifier = "1001",
            EnterpriseExtension = new ExtensionAttributeEnterpriseUser2
            {
                Manager = new Manager
                {
                    Value = "John Doe"
                }
            }
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": ""1001"",
                ""managers"": [""John Doe""]
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void NestedObjectToStringArrayAttribute_DefaultValue()
    {
        // Arrange
        var AttributeSchemas = new List<AttributeSchema>
        {
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "EnterpriseExtension:Manager:Value",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:Managers",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.Array,
                    ArrayDataType = AttributeDataTypes.String,
                    ArrayElementFieldName = "EnterpriseExtension:Manager:Value",
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                }
            };

        var resource = new Core2EnterpriseUser
        {
            ExternalIdentifier = "1001",
            EnterpriseExtension = new ExtensionAttributeEnterpriseUser2
            {
                Manager = new Manager
                {
                    Value = null
                }
            }
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""managers"": [""N/A""]
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void ObjectToStringArrayAttribute_EmptyStr()
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
                    DestinationType = AttributeDataTypes.String,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },

                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "Name:Formatted",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:Names",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.Array,
                    ArrayDataType = AttributeDataTypes.String,
                    ArrayElementFieldName = "name:formatted",
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                }
            };

        var resource = new Core2EnterpriseUser
        {
            Identifier = "001",
            Name = new Name
            {
                GivenName = "John",
                FamilyName = "Doe",
                Formatted = ""
            }

        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": ""001"",
                ""names"": [""N/A""]
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void ObjectToIntegerArrayAttribute_Null()
    {
        // Arrange
        var AttributeSchemas = new List<AttributeSchema>
        {
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "ExternalIdentifier",
                    DestinationField = "urn:kn:ki:schema:id",
                    IsRequired = false,
                    DestinationType = AttributeDataTypes.Number,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },

                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "EnterpriseExtension:EmployeeNumber",
                    DefaultValue = "0",
                    DestinationField = "urn:kn:ki:schema:employeeNumbers",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.Array,
                    ArrayDataType = AttributeDataTypes.Number,
                    ArrayElementFieldName = "EnterpriseExtension:EmployeeNumber",
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                }
            };

        var resource = new Core2EnterpriseUser
        {
            ExternalIdentifier = "1001",
            EnterpriseExtension = new ExtensionAttributeEnterpriseUser2
            {
                Manager = new Manager
                {
                    Value = "John Doe"
                },
                EmployeeNumber = null,
                Department = null
            }
        };


        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": ""1001"",
                ""employeeNumbers"": [0]
            }");

        Assert.Equal(expectedJson, result);
    }
    [Fact]
    public void ArrayToStringArrayAttribute_SkipNullOrEmpty()
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
                    SourceValue = "Addresses",
                    DestinationField = "urn:kn:ki:schema:PostalCodes",
                    IsRequired = false,
                    DestinationType = AttributeDataTypes.Array,
                    ArrayDataType = AttributeDataTypes.String,
                    ArrayElementFieldName = "urn:kn:ki:schema:addresses:PostalCode",
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                }
            };

        var resource = new Core2EnterpriseUser
        {
            Identifier = "001",
            Addresses = new List<Address>
            {
                new Address
                {
                    StreetAddress = "1234 Main St",
                    Locality = "Anytown",
                    Region = "TX",
                    PostalCode = "12345",
                    Country = "USA"
                },
                new Address
                {
                    StreetAddress = "5678 Elm St",
                    Locality = "Othertown",
                    Region = "TX",
                    PostalCode = "54321",
                    Country = "USA"
                },
                new Address
                {
                    StreetAddress = "91011 Oak St",
                    Locality = "AnotherTown",
                    Region = "TX",
                    PostalCode = null,
                    Country = "USA"
                },
                new Address
                {
                    StreetAddress = "1213 Pine St",
                    Locality = "YetAnotherTown",
                    Region = "TX",
                    PostalCode = "",
                    Country = "USA"
                }
            }
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": 1,
                ""streetAddresses"": [""12345"", ""54321""]
            }");

        Assert.Equal(expectedJson, result);
    }
}
