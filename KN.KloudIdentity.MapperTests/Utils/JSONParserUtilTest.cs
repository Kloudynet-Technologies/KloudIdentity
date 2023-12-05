using KN.KloudIdentity.Mapper.Config;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Xunit;

namespace KN.KloudIdentity.Tests.KN.KloudIdentity.Mapper.Utils
{
    public class JSONParserUtilTests
    {
        [Fact]
        public void Parse_SimpleObject_ReturnsExpectedJson()
        {
            // Arrange
            var schemaAttributes = new List<SchemaAttribute>
            {
                new SchemaAttribute { FieldName = "urn:kn:ki:schema:name", MappedAttribute = "DisplayName" },
                new SchemaAttribute { FieldName = "urn:kn:ki:schema:email", MappedAttribute = "UserName" }
            };
            var resource = new Core2EnterpriseUser { UserName = "john.d@mail.com", DisplayName = "John Doe" };

            // Act
            var result = JSONParserUtil<Core2EnterpriseUser>.Parse(schemaAttributes, resource);

            // Assert
            var expectedJson = JObject.Parse(@"{
                ""name"": ""John Doe"",
                ""email"": ""john.d@mail.com""
            }");
            Assert.Equal(expectedJson, result);
        }

        [Fact]
        public void Parse_ComplexObject_ReturnsExpectedJson_1()
        {
            // Arrange
            var schemaAttributes = new List<SchemaAttribute>
            {
                new SchemaAttribute { FieldName = "urn:kn:ki:schema:address:street", MappedAttribute = "Addresses:StreetAddress" },
                new SchemaAttribute { FieldName = "urn:kn:ki:schema:address:city", MappedAttribute = "Addresses:Locality" },
                new SchemaAttribute { FieldName = "urn:kn:ki:schema:address:state", MappedAttribute = "Addresses:Region" },
                new SchemaAttribute { FieldName = "urn:kn:ki:schema:address:zip", MappedAttribute = "Addresses:PostalCode" }
            };
            var resource = new Core2EnterpriseUser
            {
                Addresses = new List<Address> {
                    new Address
                    {
                        StreetAddress = "123 Main St",
                        Locality = "Anytown",
                        Region = "CA",
                        PostalCode = "12345"
                    }
                }
            };

            // Act
            var result = JSONParserUtil<Core2EnterpriseUser>.Parse(schemaAttributes, resource);

            // Assert
            var expectedJson = JObject.Parse(@"{
                ""address"": {
                    ""street"": ""123 Main St"",
                    ""city"": ""Anytown"",
                    ""state"": ""CA"",
                    ""zip"": ""12345""
                }
            }");
            Assert.Equal(expectedJson, result);
        }

        [Fact]
        public void Parse_ComplexObject_ReturnsExpectedJson_2()
        {
            // Arrange
            var schemaAttributes = new List<SchemaAttribute>
            {
                new SchemaAttribute { FieldName = "urn:kn:ki:schema:street", MappedAttribute = "Addresses:StreetAddress" },
                new SchemaAttribute { FieldName = "urn:kn:ki:schema:city", MappedAttribute = "Addresses:Locality" },
                new SchemaAttribute { FieldName = "urn:kn:ki:schema:state", MappedAttribute = "Addresses:Region" },
                new SchemaAttribute { FieldName = "urn:kn:ki:schema:zip", MappedAttribute = "Addresses:PostalCode" }
            };
            var resource = new Core2EnterpriseUser
            {
                Addresses = new List<Address> {
                    new Address
                    {
                        StreetAddress = "123 Main St",
                        Locality = "Anytown",
                        Region = "CA",
                        PostalCode = "12345"
                    }
                }
            };

            // Act
            var result = JSONParserUtil<Core2EnterpriseUser>.Parse(schemaAttributes, resource);

            // Assert
            var expectedJson = JObject.Parse(@"{
                ""street"": ""123 Main St"",
                    ""city"": ""Anytown"",
                    ""state"": ""CA"",
                    ""zip"": ""12345""
            }");
            Assert.Equal(expectedJson, result);
        }

        [Fact]
        public void Parse_ComplexObject_ReturnsExpectedJson_3()
        {
            // Arrange
            var schemaAttributes = new List<SchemaAttribute>
            {
                new SchemaAttribute { FieldName = "urn:kn:ki:schema:address:street", MappedAttribute = "Addresses:StreetAddress" },
                new SchemaAttribute { FieldName = "urn:kn:ki:schema:address:city", MappedAttribute = "Addresses:Locality" },
                new SchemaAttribute { FieldName = "urn:kn:ki:schema:address:state", MappedAttribute = "Addresses:Region" },
                new SchemaAttribute { FieldName = "urn:kn:ki:schema:address:zip", MappedAttribute = "Addresses:PostalCode" },
                new SchemaAttribute { FieldName = "urn:kn:ki:schema:name", MappedAttribute = "DisplayName" },
                new SchemaAttribute { FieldName = "urn:kn:ki:schema:email", MappedAttribute = "UserName" }
            };
            var resource = new Core2EnterpriseUser
            {
                DisplayName = "John Doe",
                UserName = "john.doe@mail.com",
                Addresses = new List<Address> {
                    new Address
                    {
                        StreetAddress = "123 Main St",
                        Locality = "Anytown",
                        Region = "CA",
                        PostalCode = "12345"
                    }
                }
            };

            // Act
            var result = JSONParserUtil<Core2EnterpriseUser>.Parse(schemaAttributes, resource);

            // Assert
            var expectedJson = JObject.Parse(@"{                
                ""address"": {
                    ""street"": ""123 Main St"",
                    ""city"": ""Anytown"",
                    ""state"": ""CA"",
                    ""zip"": ""12345""
                },
                ""name"": ""John Doe"",
                ""email"": ""john.doe@mail.com""
            }");
            Assert.Equal(expectedJson, result);
        }

        [Fact]
        public void Parse_ComplexArray_ReturnsFlatArrayExpectedJson_4()
        {
            // Arrange
            var schemaAttributes = new List<SchemaAttribute>
            {
                new SchemaAttribute
                {
                    DataType = JSonDataType.Array,
                    FieldName = "urn:kn:ki:schema:Emails",
                    MappedAttribute = "ElectronicMailAddresses",
                    ArrayElementType = JSonDataType.String,
                    ArrayElementMappingField = "ElectronicMailAddresses:Value"
                }
            };
            var resource = new Core2EnterpriseUser
            {
                ElectronicMailAddresses = new List<ElectronicMailAddress>
                {
                    new ElectronicMailAddress
                    {
                        Value = "a@b.com",
                        ItemType = "work",
                        Primary = true
                    },
                    new ElectronicMailAddress
                    {
                        Value = "test@gmail.com",
                        ItemType = "home",
                        Primary = false
                    }
                }
            };

            // Act
            var result = JSONParserUtil<Core2EnterpriseUser>.Parse(schemaAttributes, resource);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.ContainsKey("Emails"));

            var emailsArray = result["Emails"] as JArray;
            Assert.NotNull(emailsArray);
            Assert.Equal(2, emailsArray.Count);

            // Assert
            var expectedEmails = JArray.Parse(@"[
                                ""a@b.com"",
                                ""test@gmail.com""
                            ]");
            var actualJson = JObject.FromObject(result);

            var actualEmails = actualJson["Emails"] as JArray;

            Assert.NotNull(actualEmails);
            Assert.Equal(expectedEmails, actualEmails, JToken.EqualityComparer);
        }

        [Fact]
        public void Parse_ComplexArray_ReturnsArrayOfObjectExpectedJson_5()
        {
            // Arrange
            var schemaAttributes = new List<SchemaAttribute>
            {
                new SchemaAttribute
                {
                    DataType = JSonDataType.Array,
                    FieldName = "urn:kn:ki:schema:Emails",
                    MappedAttribute = "ElectronicMailAddresses",
                    ArrayElementType = JSonDataType.Object,
                    ChildSchemas = new List<SchemaAttribute>
                    {
                        new SchemaAttribute
                        {
                            DataType = JSonDataType.String,
                            FieldName = "urn:kn:ki:schema:Emails:Value",
                            MappedAttribute = "Value"
                        },
                        new SchemaAttribute
                        {
                            DataType = JSonDataType.String,
                            FieldName = "urn:kn:ki:schema:Emails:ItemType",
                            MappedAttribute = "ItemType"
                        },
                        new SchemaAttribute
                        {
                            DataType = JSonDataType.Boolean,
                            FieldName = "urn:kn:ki:schema:Emails:Primary",
                            MappedAttribute = "Primary"
                        }
                    }
                }
            };

            var resource = new Core2EnterpriseUser
            {
                ElectronicMailAddresses = new List<ElectronicMailAddress>
                {
                    new ElectronicMailAddress
                    {
                        Value = "work@gmail.com",
                        ItemType = "work",
                        Primary = true
                    },
                    new ElectronicMailAddress
                    {
                        Value = "home@gmail.com",
                        ItemType = "home",
                        Primary = false
                    }
                }
            };

            // Act
            var result = JSONParserUtil<Core2EnterpriseUser>.Parse(schemaAttributes, resource);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.ContainsKey("Emails"));

            var emailsArray = result["Emails"] as JArray;
            Assert.NotNull(emailsArray);
            Assert.Equal(2, emailsArray.Count);

            // Assert
            var expectedJson = JObject.Parse(@"{
                ""Emails"": [
                    {
                        ""Value"": ""work@gmail.com"",
                        ""ItemType"": ""work"",
                        ""Primary"": true
                    },
                    {
                        ""Value"": ""home@gmail.com"",
                        ""ItemType"": ""home"",
                        ""Primary"": false
                    }
                ]
            }");
            var actualJson = JObject.FromObject(result);

            Assert.True(JObject.DeepEquals(expectedJson, actualJson));

        }

        [Fact]

        public void Parse_ComplexNestedObject_ReturnsExpectedJson_6()
        {
            // Arrange
            var schemaAttributes = new List<SchemaAttribute>
            {
                new SchemaAttribute { FieldName = "urn:kn:ki:schema:UserName", MappedAttribute = "UserName" },
                new SchemaAttribute { FieldName = "urn:kn:ki:schema:DisplayName", MappedAttribute = "DisplayName" },
                new SchemaAttribute {
                 DataType = JSonDataType.Object,
                 FieldName = "urn:kn:ki:schema:NameDetail",
                 MappedAttribute = "Name",
                 ChildSchemas = new List<SchemaAttribute>
                 {
                        new SchemaAttribute { FieldName = "urn:kn:ki:schema:NameDetail:FirstName", MappedAttribute = "Name:GivenName" },
                        new SchemaAttribute { FieldName = "urn:kn:ki:schema:NameDetail:LastName", MappedAttribute = "Name:FamilyName" },
                        new SchemaAttribute { FieldName = "urn:kn:ki:schema:NameDetail:FullName", MappedAttribute = "Name:Formatted" }
                 }

                },

                new SchemaAttribute
                {
                 DataType = JSonDataType.Object,
                 FieldName = "urn:kn:ki:schema:ExtraInfo",
                 MappedAttribute = "EnterpriseExtension",
                 ChildSchemas = new List<SchemaAttribute>
                 {
                        new SchemaAttribute { FieldName = "urn:kn:ki:schema:ExtraInfo:Department", MappedAttribute = "EnterpriseExtension:Department" },
                        new SchemaAttribute{ FieldName = "urn:kn:ki:schema:ExtraInfo:Position", MappedAttribute = "Title" },
                        new SchemaAttribute {
                            DataType = JSonDataType.Object,
                            FieldName = "urn:kn:ki:schema:ExtraInfo:Manager",
                            MappedAttribute = "EnterpriseExtension:Manager",

                            ChildSchemas = new List<SchemaAttribute>
                            {
                                new SchemaAttribute { FieldName = "urn:kn:ki:schema:ExtraInfo:Manager:Name", MappedAttribute = "EnterpriseExtension:Manager:Value" },
                            }
                        }
                 }

                },
            };

            var resource = new Core2EnterpriseUser
            {
                UserName = "user1",
                DisplayName = "User 1",
                Title = "Software Engineer",
                Name = new Name
                {
                    GivenName = "Test",
                    FamilyName = "User",
                    Formatted = "Test User"
                },
                EnterpriseExtension = new ExtensionAttributeEnterpriseUser2
                {
                    Department = "IT",
                    Manager = new Manager
                    {
                        Value = "user2"
                    }
                }

            };

            // Act
            var result = JSONParserUtil<Core2EnterpriseUser>.Parse(schemaAttributes, resource);
            Assert.NotNull(result);

            // Assert
            var expectedJson = JObject.Parse(@"{
                ""UserName"": ""user1"",
                ""DisplayName"": ""User 1"",
                ""NameDetail"": {
                    ""FirstName"": ""Test"",
                    ""LastName"": ""User"",
                    ""FullName"": ""Test User""
                },
                ""ExtraInfo"": {
                    ""Department"": ""IT"",
                    ""Position"": ""Software Engineer"",
                    ""Manager"": {
                        ""Name"": ""user2""
                    }
                }
            }");

            var actualJson = JObject.FromObject(result);
            Assert.Equal(expectedJson, actualJson);
        }

        [Fact]
        public void Parse_ComplexObjectWithArray_ReturnsExpectedJson_7()
        {
            // Arrange
            var schemaAttributes = new List<SchemaAttribute>
            {
                new SchemaAttribute { FieldName = "urn:kn:ki:schema:GroupName", MappedAttribute = "DisplayName" },
                new SchemaAttribute { FieldName = "urn:kn:ki:schema:Id", MappedAttribute = "Identifier" },
                new SchemaAttribute
                {
                 DataType = JSonDataType.Array,
                 FieldName = "urn:kn:ki:schema:Members",
                 MappedAttribute = "Members",
                 ArrayElementType = JSonDataType.Object,
                 ChildSchemas = new List<SchemaAttribute>
                 {
                        new SchemaAttribute { FieldName = "urn:kn:ki:schema:Members:Email", MappedAttribute = "Members:Value" },
                        new SchemaAttribute { FieldName = "urn:kn:ki:schema:Members:Type", MappedAttribute = "Members:TypeName" }
                 }
                },
                new SchemaAttribute
                {
                    DataType = JSonDataType.Array,
                    FieldName = "urn:kn:ki:schema:Emails",
                    MappedAttribute = "Members",
                    ArrayElementType = JSonDataType.String,
                    ArrayElementMappingField = "Members:Value"
                }
            };

            var resource = new Core2Group
            {
                DisplayName = "Group 1",
                Identifier = "group1",
                Members = new List<Member>
                {
                    new Member
                    {
                        Value = "user1@gmail.com",
                        TypeName = "User"
                    },
                    new Member
                    {
                        Value = "user2@gmail.com",
                        TypeName = "User"
                    }
                }
            };

            // Act
            var result = JSONParserUtil<Core2Group>.Parse(schemaAttributes, resource);
            Assert.NotNull(result);

            var expectedJson = JObject.Parse(@"{
                ""GroupName"": ""Group 1"",
                ""Id"": ""group1"",
                ""Members"": [
                    {
                        ""Email"": ""user1@gmail.com"",
                        ""Type"": ""User""
                    },
                    {
                        ""Email"": ""user2@gmail.com"",
                        ""Type"": ""User""
                    }
                ],
               ""Emails"": [
                     ""user1@gmail.com"",
                     ""user2@gmail.com""
                ]
                }");

            var actualJson = JObject.FromObject(result);
            Assert.Equal(expectedJson, actualJson);
        }

        [Fact]
        public void Parse_SimpleComplexObjectArray_ReturnsExpectedJson_8()
        {
            // Arrange
            var schemaAttributes = new List<SchemaAttribute>
            {
                new SchemaAttribute { FieldName = "urn:kn:ki:schema:UserName", MappedAttribute = "UserName" },
                new SchemaAttribute { FieldName = "urn:kn:ki:schema:DisplayName", MappedAttribute = "DisplayName" },
                new SchemaAttribute { DataType = JSonDataType.Boolean, FieldName = "urn:kn:ki:schema:Active", MappedAttribute = "Active" },
                new SchemaAttribute { FieldName = "urn:kn:ki:schema:Id", MappedAttribute = "Identifier" },
                new SchemaAttribute { FieldName = "urn:kn:ki:schema:ExternalId", MappedAttribute = "ExternalIdentifier" },
                new SchemaAttribute { FieldName = "urn:kn:ki:schema:UserType", MappedAttribute = "UserType" },
                new SchemaAttribute { FieldName = "urn:kn:ki:schema:Title", MappedAttribute = "Title" },

                new SchemaAttribute {
                 DataType = JSonDataType.Object,
                 FieldName = "urn:kn:ki:schema:NameDetail",
                 MappedAttribute = "Name",
                 ChildSchemas = new List<SchemaAttribute>
                 {
                        new SchemaAttribute { FieldName = "urn:kn:ki:schema:NameDetail:FirstName", MappedAttribute = "Name:GivenName" },
                        new SchemaAttribute { FieldName = "urn:kn:ki:schema:NameDetail:LastName", MappedAttribute = "Name:FamilyName" },
                        new SchemaAttribute { FieldName = "urn:kn:ki:schema:NameDetail:FullName", MappedAttribute = "Name:Formatted" }
                 }

                },

                new SchemaAttribute
                {
                 DataType = JSonDataType.Object,
                 FieldName = "urn:kn:ki:schema:ExtraInfo",
                 MappedAttribute = "EnterpriseExtension",
                 ChildSchemas = new List<SchemaAttribute>
                 {
                        new SchemaAttribute { FieldName = "urn:kn:ki:schema:ExtraInfo:Department", MappedAttribute = "EnterpriseExtension:Department" },
                        new SchemaAttribute{ FieldName = "urn:kn:ki:schema:ExtraInfo:Position", MappedAttribute = "Title" },
                        new SchemaAttribute {
                            DataType = JSonDataType.Object,
                            FieldName = "urn:kn:ki:schema:ExtraInfo:Manager",
                            MappedAttribute = "EnterpriseExtension:Manager",

                            ChildSchemas = new List<SchemaAttribute>
                            {
                                new SchemaAttribute { FieldName = "urn:kn:ki:schema:ExtraInfo:Manager:Name", MappedAttribute = "EnterpriseExtension:Manager:Value" },
                            }
                        }
                 }

                },
                new SchemaAttribute
                {
                    DataType = JSonDataType.Array,
                    FieldName = "urn:kn:ki:schema:Emails",
                    MappedAttribute = "ElectronicMailAddresses",
                    ArrayElementType = JSonDataType.Object,
                    ChildSchemas = new List<SchemaAttribute>
                    {
                        new SchemaAttribute
                        {
                            DataType = JSonDataType.String,
                            FieldName = "urn:kn:ki:schema:Emails:Value",
                            MappedAttribute = "Value"
                        },
                        new SchemaAttribute
                        {
                            DataType = JSonDataType.String,
                            FieldName = "urn:kn:ki:schema:Emails:ItemType",
                            MappedAttribute = "ItemType"
                        },
                        new SchemaAttribute
                        {
                            DataType = JSonDataType.Boolean,
                            FieldName = "urn:kn:ki:schema:Emails:Primary",
                            MappedAttribute = "Primary"
                        }
                    }
                },
                 new SchemaAttribute
                {
                    DataType = JSonDataType.Array,
                    FieldName = "urn:kn:ki:schema:UserEmails",
                    MappedAttribute = "ElectronicMailAddresses",
                    ArrayElementType = JSonDataType.String,
                    ArrayElementMappingField = "ElectronicMailAddresses:Value"
                },

               new SchemaAttribute
               {
                   DataType = JSonDataType.Array,
                   FieldName = "urn:kn:ki:schema:Addresses",
                   MappedAttribute = "Addresses",
                   ArrayElementType = JSonDataType.Object,
                   ChildSchemas = new List<SchemaAttribute>
                   {
                          new SchemaAttribute
                          {
                            DataType = JSonDataType.String,
                            FieldName = "urn:kn:ki:schema:Addresses:StreetAddress",
                            MappedAttribute = "StreetAddress"
                          },
                          new SchemaAttribute
                          {
                            DataType = JSonDataType.String,
                            FieldName = "urn:kn:ki:schema:Addresses:Locality",
                            MappedAttribute = "Locality"
                          },
                          new SchemaAttribute
                          {
                            DataType = JSonDataType.String,
                            FieldName = "urn:kn:ki:schema:Addresses:Region",
                            MappedAttribute = "Region"
                          },
                          new SchemaAttribute
                          {
                            DataType = JSonDataType.String,
                            FieldName = "urn:kn:ki:schema:Addresses:PostalCode",
                            MappedAttribute = "PostalCode"
                          }
                     }
               },

               new SchemaAttribute
               {
                     DataType = JSonDataType.Array,
                     FieldName = "urn:kn:ki:schema:Roles",
                     MappedAttribute = "Roles",
                     ArrayElementType = JSonDataType.String,
                     ArrayElementMappingField = "Roles:Value"
                },

            };

            var resource = new Core2EnterpriseUser
            {
                UserName = "user1",
                DisplayName = "User 1",
                Title = "Software Engineer",
                Name = new Name
                {
                    GivenName = "Test",
                    FamilyName = "User",
                    Formatted = "Test User"
                },
                EnterpriseExtension = new ExtensionAttributeEnterpriseUser2
                {
                    Department = "IT",
                    Manager = new Manager
                    {
                        Value = "user2"
                    }
                },
                ElectronicMailAddresses = new List<ElectronicMailAddress>
                {
                    new ElectronicMailAddress
                    {
                        Value = "work@gmail.com",
                        ItemType = "work",
                        Primary = true
                    },
                    new ElectronicMailAddress
                    {
                        Value = "home@gmail.com",
                        ItemType = "home",
                        Primary = false
                    }
                },
                Addresses = new List<Address>
                {
                    new Address
                    {
                        StreetAddress = "123 Main St",
                        Locality = "Anytown",
                        Region = "CA",
                        PostalCode = "12345"
                    },
                    new Address
                    {
                        StreetAddress = "456 Main St",
                        Locality = "Anytown",
                        Region = "CA",
                        PostalCode = "12345"
                    }
                },
                Roles = new List<Role>
                {
                    new Role
                    {
                        Value = "role1",
                        ItemType = "Role",
                        Display = "Role 1",
                        Primary = true
                    },
                    new Role
                    {
                        Value = "role2",
                        ItemType = "Role",
                        Display = "Role 2",
                        Primary = false
                    }
                },
                
                Active = true,
                Identifier = "user1",
                ExternalIdentifier = "user1",
                UserType = "Employee"
            };

            // Act
            var result = JSONParserUtil<Core2EnterpriseUser>.Parse(schemaAttributes, resource);
            Assert.NotNull(result);

            // Assert
            var expectedJson = JObject.Parse(@"{
                ""UserName"": ""user1"",
                ""DisplayName"": ""User 1"",
                ""Active"": true,
                ""Id"": ""user1"",
                ""ExternalId"": ""user1"",
                ""UserType"": ""Employee"",
                ""Title"": ""Software Engineer"",
                ""NameDetail"": {
                    ""FirstName"": ""Test"",
                    ""LastName"": ""User"",
                    ""FullName"": ""Test User""
                },
                ""ExtraInfo"": {
                    ""Department"": ""IT"",
                    ""Position"": ""Software Engineer"",
                    ""Manager"": {
                        ""Name"": ""user2""
                    }
                },
                 ""Emails"": [
                    {
                        ""Value"": ""work@gmail.com"",
                        ""ItemType"": ""work"",
                        ""Primary"": true
                    },
                    {
                        ""Value"": ""home@gmail.com"",
                        ""ItemType"": ""home"",
                        ""Primary"": false
                    }
                ],
                ""Addresses"": [
                    {
                        ""StreetAddress"": ""123 Main St"",
                        ""Locality"": ""Anytown"",
                        ""Region"": ""CA"",
                        ""PostalCode"": ""12345""
                    },
                    {
                        ""StreetAddress"": ""456 Main St"",
                        ""Locality"": ""Anytown"",
                        ""Region"": ""CA"",
                        ""PostalCode"": ""12345""
                    }
                ],
                ""Roles"": [
                    ""role1"",
                    ""role2""
                ],
                ""UserEmails"":[
                     ""work@gmail.com"",
                     ""home@gmail.com""
                ]
            }");

            var actualJson = JObject.FromObject(result);
            Assert.True(JToken.DeepEquals(expectedJson, actualJson));
        }

    }
}