using KN.KloudIdentity.Mapper;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.MapperTests;

public partial class JSONParserUtilTests
{
    [Fact]
    public void PrimitiveStringToObjectType()
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
                    }
                }
            };

        var resource = new Core2EnterpriseUser
        {
            Identifier = "001",
            DisplayName = "John Doe",
            Name = new Name { GivenName = "John", FamilyName = "Doe" },
            Nickname = "JD",
            Title = "Manager"
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": 1,
                ""person"": {
                    ""displayName"": ""John Doe"",
                    ""title"": ""Manager"",
                    ""nickname"": ""JD""
                }
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void PrimitiveStrIntBoolToObjectType()
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
                            SourceValue = "Active",
                            DefaultValue = "N/A",
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
            Identifier = "001",
            DisplayName = "John Doe",
            Name = new Name { GivenName = "John", FamilyName = "Doe" },
            Active = true
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": 1,
                ""person"": {
                    ""id"": 1,
                    ""displayName"": ""John Doe"",
                    ""IsActive"": true
                }
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void PrimitiveStrIntBoolToObjectType_DefaultValue()
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
                    DestinationField = "urn:kn:ki:schema:person",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.Object,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always },
                    ChildSchemas = new List<AttributeSchema>
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
                            SourceValue = "Active",
                            DefaultValue = "false",
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
            Identifier = "",
            DisplayName = "",
            Name = new Name { GivenName = "John", FamilyName = "Doe" }
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": 1,
                ""person"": {
                    ""id"": 0,
                    ""displayName"": ""N/A"",
                    ""IsActive"": false
                }
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void PrimitiveStrIntBoolToObjectType_NotRequired()
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
                    DestinationField = "urn:kn:ki:schema:person",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.Object,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always },
                    ChildSchemas = new List<AttributeSchema>
                    {
                        new AttributeSchema
                        {
                            MappingType = MappingTypes.Direct,
                            SourceValue = "Identifier",
                            DefaultValue = "",
                            DestinationField = "urn:kn:ki:schema:id",
                            IsRequired = false,
                            DestinationType = AttributeDataTypes.Number,
                            MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                        },
                        new AttributeSchema
                        {
                            MappingType = MappingTypes.Direct,
                            SourceValue = "DisplayName",
                            DefaultValue = "",
                            DestinationField = "urn:kn:ki:schema:displayName",
                            IsRequired = false,
                            DestinationType = AttributeDataTypes.String,
                            MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                        },
                        new AttributeSchema
                        {
                            MappingType = MappingTypes.Direct,
                            SourceValue = "Active",
                            DefaultValue = "",
                            DestinationField = "urn:kn:ki:schema:isActive",
                            IsRequired = false,
                            DestinationType = AttributeDataTypes.Boolean,
                            MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                        },
                    }
                }
            };

        var resource = new Core2EnterpriseUser
        {
            Identifier = "",
            DisplayName = "",
            Name = new Name { GivenName = "John", FamilyName = "Doe" }
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": 1,
                ""person"": {
                    ""id"": 0,
                    ""displayName"": null,
                    ""IsActive"": false
                }
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void ObjectTypeStrIntBoolToObjectType()
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
                    SourceValue = "Name",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:data",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.Object,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always },
                    ChildSchemas = new List<AttributeSchema>
                    {
                        new AttributeSchema
                        {
                            MappingType = MappingTypes.Direct,
                            SourceValue = "Name:GivenName",
                            DefaultValue = "",
                            DestinationField = "urn:kn:ki:schema:givenName",
                            IsRequired = false,
                            DestinationType = AttributeDataTypes.String,
                            MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                        },
                        new AttributeSchema
                        {
                            MappingType = MappingTypes.Direct,
                            SourceValue = "Metadata:Version",
                            DefaultValue = "",
                            DestinationField = "urn:kn:ki:schema:version",
                            IsRequired = false,
                            DestinationType = AttributeDataTypes.Number,
                            MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                        }
                    }
                }
            };

        var resource = new Core2EnterpriseUser
        {
            Identifier = "001",
            PhoneNumbers = new List<PhoneNumber>
            {
                new PhoneNumber { Value = "1234567890", ItemType = "work", Primary = true }
            },
            Name = new Name { GivenName = "John", FamilyName = "Doe" },
            Metadata = new Core2Metadata { ResourceType = "User", Created = DateTime.Now, LastModified = DateTime.Now, Version = "1" },
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": 1,
                ""data"": {
                    ""givenName"": ""John"",
                    ""version"": 1
                }
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void ObjectTypeStrIntBoolToObjectType_DefaultValue()
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
                    SourceValue = "Name",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:data",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.Object,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always },
                    ChildSchemas = new List<AttributeSchema>
                    {
                        new AttributeSchema
                        {
                            MappingType = MappingTypes.Direct,
                            SourceValue = "Name:GivenName",
                            DefaultValue = "No Name",
                            DestinationField = "urn:kn:ki:schema:givenName",
                            IsRequired = true,
                            DestinationType = AttributeDataTypes.String,
                            MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                        },
                        new AttributeSchema
                        {
                            MappingType = MappingTypes.Direct,
                            SourceValue = "Metadata:Version",
                            DefaultValue = "999",
                            DestinationField = "urn:kn:ki:schema:version",
                            IsRequired = true,
                            DestinationType = AttributeDataTypes.Number,
                            MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                        }
                    }
                }
            };

        var resource = new Core2EnterpriseUser
        {
            Identifier = "001",
            PhoneNumbers = new List<PhoneNumber>
            {
                new PhoneNumber { Value = "1234567890", ItemType = "work", Primary = true }
            },
            Name = new Name { GivenName = "", FamilyName = "Doe" },
            Metadata = new Core2Metadata { ResourceType = "User", Created = DateTime.Now, LastModified = DateTime.Now },
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": 1,
                ""data"": {
                    ""givenName"": ""No Name"",
                    ""version"": 999
                }
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void ObjectTypeStrIntBoolToObjectType_NotRequired()
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
                    SourceValue = "Name",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:data",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.Object,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always },
                    ChildSchemas = new List<AttributeSchema>
                    {
                        new AttributeSchema
                        {
                            MappingType = MappingTypes.Direct,
                            SourceValue = "Name:GivenName",
                            DefaultValue = "No Name",
                            DestinationField = "urn:kn:ki:schema:givenName",
                            IsRequired = false,
                            DestinationType = AttributeDataTypes.String,
                            MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                        },
                        new AttributeSchema
                        {
                            MappingType = MappingTypes.Direct,
                            SourceValue = "Metadata:Version",
                            DefaultValue = "999",
                            DestinationField = "urn:kn:ki:schema:version",
                            IsRequired = false,
                            DestinationType = AttributeDataTypes.Number,
                            MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                        }
                    }
                }
            };

        var resource = new Core2EnterpriseUser
        {
            Identifier = "001",
            PhoneNumbers = new List<PhoneNumber>
            {
                new PhoneNumber { Value = "1234567890", ItemType = "work", Primary = true }
            },
            Name = new Name { GivenName = "", FamilyName = "Doe" },
            Metadata = new Core2Metadata { ResourceType = "User", Created = DateTime.Now, LastModified = DateTime.Now },
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": 1,
                ""data"": {
                    ""givenName"": null,
                    ""version"": 0
                }
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void ObjectArrayTypeStrIntBoolToObjectType_NoIndex()
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
                    SourceValue = "PhoneNumbers",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:data",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.Object,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always },
                    ChildSchemas = new List<AttributeSchema>
                    {
                        new AttributeSchema
                        {
                            MappingType = MappingTypes.Direct,
                            SourceValue = "PhoneNumbers:Value",
                            DefaultValue = "000",
                            DestinationField = "urn:kn:ki:schema:phone",
                            IsRequired = false,
                            DestinationType = AttributeDataTypes.Number,
                            MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                        },
                        new AttributeSchema
                        {
                            MappingType = MappingTypes.Direct,
                            SourceValue = "PhoneNumbers:ItemType",
                            DefaultValue = "N/A",
                            DestinationField = "urn:kn:ki:schema:type",
                            IsRequired = false,
                            DestinationType = AttributeDataTypes.String,
                            MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                        },
                        new AttributeSchema
                        {
                            MappingType = MappingTypes.Direct,
                            SourceValue = "PhoneNumbers:Primary",
                            DefaultValue = "false",
                            DestinationField = "urn:kn:ki:schema:primary",
                            IsRequired = false,
                            DestinationType = AttributeDataTypes.Boolean,
                            MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                        }
                    }
                }
            };

        var resource = new Core2EnterpriseUser
        {
            Identifier = "001",
            PhoneNumbers = new List<PhoneNumber>
            {
                new PhoneNumber { Value = "1234567890", ItemType = "work", Primary = true },
                new PhoneNumber { Value = "8457589685", ItemType = "office", Primary = false }
            },
            Name = new Name { GivenName = "", FamilyName = "Doe" },
            Metadata = new Core2Metadata { ResourceType = "User", Created = DateTime.Now, LastModified = DateTime.Now },
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": 1,
                ""data"": {
                    ""phone"": 1234567890,
                    ""type"": ""work"",
                    ""primary"": true
                }
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void ObjectArrayTypeStrIntBoolToObjectType_WithIndex()
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
                    SourceValue = "PhoneNumbers",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:data",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.Object,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always },
                    ChildSchemas = new List<AttributeSchema>
                    {
                        new AttributeSchema
                        {
                            MappingType = MappingTypes.Direct,
                            SourceValue = "PhoneNumbers[2]:Value",
                            DefaultValue = "000",
                            DestinationField = "urn:kn:ki:schema:phone",
                            IsRequired = false,
                            DestinationType = AttributeDataTypes.Number,
                            MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                        },
                        new AttributeSchema
                        {
                            MappingType = MappingTypes.Direct,
                            SourceValue = "PhoneNumbers[2]:ItemType",
                            DefaultValue = "N/A",
                            DestinationField = "urn:kn:ki:schema:type",
                            IsRequired = false,
                            DestinationType = AttributeDataTypes.String,
                            MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                        },
                        new AttributeSchema
                        {
                            MappingType = MappingTypes.Direct,
                            SourceValue = "PhoneNumbers[2]:Primary",
                            DefaultValue = "false",
                            DestinationField = "urn:kn:ki:schema:primary",
                            IsRequired = false,
                            DestinationType = AttributeDataTypes.Boolean,
                            MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                        }
                    }
                }
            };

        var resource = new Core2EnterpriseUser
        {
            Identifier = "001",
            PhoneNumbers = new List<PhoneNumber>
            {
                new PhoneNumber { Value = "1234567890", ItemType = "work", Primary = true },
                new PhoneNumber { Value = "8457589685", ItemType = "office", Primary = false },
                new PhoneNumber { Value = "2514256352", ItemType = "office-2", Primary = false }
            },
            Name = new Name { GivenName = "", FamilyName = "Doe" },
            Metadata = new Core2Metadata { ResourceType = "User", Created = DateTime.Now, LastModified = DateTime.Now },
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": 1,
                ""data"": {
                    ""phone"": 2514256352,
                    ""type"": ""office-2"",
                    ""primary"": false
                }
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void ObjectArrayTypeStrIntBoolToObjectType_IndexOverflow()
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
                    SourceValue = "PhoneNumbers",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:data",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.Object,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always },
                    ChildSchemas = new List<AttributeSchema>
                    {
                        new AttributeSchema
                        {
                            MappingType = MappingTypes.Direct,
                            SourceValue = "PhoneNumbers[5]:Value",
                            DefaultValue = "000",
                            DestinationField = "urn:kn:ki:schema:phone",
                            IsRequired = true,
                            DestinationType = AttributeDataTypes.Number,
                            MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                        },
                        new AttributeSchema
                        {
                            MappingType = MappingTypes.Direct,
                            SourceValue = "PhoneNumbers[5]:ItemType",
                            DefaultValue = "N/A",
                            DestinationField = "urn:kn:ki:schema:type",
                            IsRequired = true,
                            DestinationType = AttributeDataTypes.String,
                            MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                        },
                        new AttributeSchema
                        {
                            MappingType = MappingTypes.Direct,
                            SourceValue = "PhoneNumbers[5]:Primary",
                            DefaultValue = "false",
                            DestinationField = "urn:kn:ki:schema:primary",
                            IsRequired = true,
                            DestinationType = AttributeDataTypes.Boolean,
                            MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                        }
                    }
                }
            };

        var resource = new Core2EnterpriseUser
        {
            Identifier = "001",
            PhoneNumbers = new List<PhoneNumber>
            {
                new PhoneNumber { Value = "1234567890", ItemType = "work", Primary = true },
                new PhoneNumber { Value = "8457589685", ItemType = "office", Primary = false },
                new PhoneNumber { Value = "2514256352", ItemType = "office-2", Primary = false }
            },
            Name = new Name { GivenName = "", FamilyName = "Doe" },
            Metadata = new Core2Metadata { ResourceType = "User", Created = DateTime.Now, LastModified = DateTime.Now },
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": 1,
                ""data"": {
                    ""phone"": 0,
                    ""type"": ""N/A"",
                    ""primary"": false
                }
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void ObjectArrayTypeStrIntBoolToObjectType_NotRequired()
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
                    SourceValue = "PhoneNumbers",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:data",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.Object,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always },
                    ChildSchemas = new List<AttributeSchema>
                    {
                        new AttributeSchema
                        {
                            MappingType = MappingTypes.Direct,
                            SourceValue = "PhoneNumbers[5]:Value",
                            DefaultValue = "000",
                            DestinationField = "urn:kn:ki:schema:phone",
                            IsRequired = false,
                            DestinationType = AttributeDataTypes.Number,
                            MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                        },
                        new AttributeSchema
                        {
                            MappingType = MappingTypes.Direct,
                            SourceValue = "PhoneNumbers[5]:ItemType",
                            DefaultValue = "N/A",
                            DestinationField = "urn:kn:ki:schema:type",
                            IsRequired = false,
                            DestinationType = AttributeDataTypes.String,
                            MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                        },
                        new AttributeSchema
                        {
                            MappingType = MappingTypes.Direct,
                            SourceValue = "PhoneNumbers[5]:Primary",
                            DefaultValue = "false",
                            DestinationField = "urn:kn:ki:schema:primary",
                            IsRequired = false,
                            DestinationType = AttributeDataTypes.Boolean,
                            MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                        }
                    }
                }
            };

        var resource = new Core2EnterpriseUser
        {
            Identifier = "001",
            PhoneNumbers = new List<PhoneNumber>
            {
                new PhoneNumber { Value = "1234567890", ItemType = "work", Primary = true },
                new PhoneNumber { Value = "8457589685", ItemType = "office", Primary = false },
                new PhoneNumber { Value = "2514256352", ItemType = "office-2", Primary = false }
            },
            Name = new Name { GivenName = "", FamilyName = "Doe" },
            Metadata = new Core2Metadata { ResourceType = "User", Created = DateTime.Now, LastModified = DateTime.Now },
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": 1,
                ""data"": {
                    ""phone"": 0,
                    ""type"": null,
                    ""primary"": false
                }
            }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void ObjectArrayTypeStrIntBoolToObjectType_EmptyValues()
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
                    SourceValue = "PhoneNumbers",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:data",
                    IsRequired = true,
                    DestinationType = AttributeDataTypes.Object,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always },
                    ChildSchemas = new List<AttributeSchema>
                    {
                        new AttributeSchema
                        {
                            MappingType = MappingTypes.Direct,
                            SourceValue = "PhoneNumbers[1]:Value",
                            DefaultValue = "000",
                            DestinationField = "urn:kn:ki:schema:phone",
                            IsRequired = true,
                            DestinationType = AttributeDataTypes.Number,
                            MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                        },
                        new AttributeSchema
                        {
                            MappingType = MappingTypes.Direct,
                            SourceValue = "PhoneNumbers[1]:ItemType",
                            DefaultValue = "N/A",
                            DestinationField = "urn:kn:ki:schema:type",
                            IsRequired = true,
                            DestinationType = AttributeDataTypes.String,
                            MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                        },
                        new AttributeSchema
                        {
                            MappingType = MappingTypes.Direct,
                            SourceValue = "PhoneNumbers[1]:Primary",
                            DefaultValue = "false",
                            DestinationField = "urn:kn:ki:schema:primary",
                            IsRequired = true,
                            DestinationType = AttributeDataTypes.Boolean,
                            MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                        }
                    }
                }
            };

        var resource = new Core2EnterpriseUser
        {
            Identifier = "001",
            PhoneNumbers = new List<PhoneNumber>
            {
                new PhoneNumber { Value = "1234567890", ItemType = "work", Primary = true },
                new PhoneNumber { Value = "", ItemType = ""},
                new PhoneNumber { Value = "2514256352", ItemType = "office-2", Primary = false }
            },
            Name = new Name { GivenName = "", FamilyName = "Doe" },
            Metadata = new Core2Metadata { ResourceType = "User", Created = DateTime.Now, LastModified = DateTime.Now },
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
                ""id"": 1,
                ""data"": {
                    ""phone"": 0,
                    ""type"": ""N/A"",
                    ""primary"": false
                }
            }");

        Assert.Equal(expectedJson, result);
    }
}
