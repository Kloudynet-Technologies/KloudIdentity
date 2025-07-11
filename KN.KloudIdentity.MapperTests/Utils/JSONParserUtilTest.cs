using KN.KloudIdentity.Mapper.Config;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Xunit;

namespace KN.KloudIdentity.MapperTests
{
    public partial class JSONParserUtilTests
    {
        [Fact]
        public void Parse_SimpleObject_ReturnsExpectedJson()
        {
            // Arrange
            var AttributeSchemas = new List<AttributeSchema>
            {
                new AttributeSchema { DestinationField = "urn:kn:ki:schema:name", SourceValue = "DisplayName" },
                new AttributeSchema { DestinationField = "urn:kn:ki:schema:email", SourceValue = "UserName" }
            };
            var resource = new Core2EnterpriseUser { UserName = "john.d@mail.com", DisplayName = "John Doe" };

            // Act
            var result = JSONParserUtil<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

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
            var AttributeSchemas = new List<AttributeSchema>
            {
                new AttributeSchema { DestinationField = "urn:kn:ki:schema:address:street", SourceValue = "Addresses:StreetAddress" },
                new AttributeSchema { DestinationField = "urn:kn:ki:schema:address:city", SourceValue = "Addresses:Locality" },
                new AttributeSchema { DestinationField = "urn:kn:ki:schema:address:state", SourceValue = "Addresses:Region" },
                new AttributeSchema { DestinationField = "urn:kn:ki:schema:address:zip", SourceValue = "Addresses:PostalCode" }
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
            var result = JSONParserUtil<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

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
            var AttributeSchemas = new List<AttributeSchema>
            {
                new AttributeSchema { DestinationField = "urn:kn:ki:schema:street", SourceValue = "Addresses:StreetAddress" },
                new AttributeSchema { DestinationField = "urn:kn:ki:schema:city", SourceValue = "Addresses:Locality" },
                new AttributeSchema { DestinationField = "urn:kn:ki:schema:state", SourceValue = "Addresses:Region" },
                new AttributeSchema { DestinationField = "urn:kn:ki:schema:zip", SourceValue = "Addresses:PostalCode" }
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
            var result = JSONParserUtil<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

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
            var AttributeSchemas = new List<AttributeSchema>
            {
                new AttributeSchema { DestinationField = "urn:kn:ki:schema:address:street", SourceValue = "Addresses:StreetAddress" },
                new AttributeSchema { DestinationField = "urn:kn:ki:schema:address:city", SourceValue = "Addresses:Locality" },
                new AttributeSchema { DestinationField = "urn:kn:ki:schema:address:state", SourceValue = "Addresses:Region" },
                new AttributeSchema { DestinationField = "urn:kn:ki:schema:address:zip", SourceValue = "Addresses:PostalCode" },
                new AttributeSchema { DestinationField = "urn:kn:ki:schema:name", SourceValue = "DisplayName" },
                new AttributeSchema { DestinationField = "urn:kn:ki:schema:email", SourceValue = "UserName" }
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
            var result = JSONParserUtil<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

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

        // [Fact]
        // public void Parse_ComplexArray_ReturnsFlatArrayExpectedJson_4()
        // {
        //     // Arrange
        //     var AttributeSchemas = new List<AttributeSchema>
        //     {
        //         new AttributeSchema
        //         {
        //             DestinationType = JsonDataTypess.Array,
        //             DestinationField = "urn:kn:ki:schema:Emails",
        //             SourceValue = "ElectronicMailAddresses",
        //             ArrayDataType = JsonDataTypess.String,
        //             ArrayElementMappingField = "ElectronicMailAddresses:Value"
        //         }
        //     };
        //     var resource = new Core2EnterpriseUser
        //     {
        //         ElectronicMailAddresses = new List<ElectronicMailAddress>
        //         {
        //             new ElectronicMailAddress
        //             {
        //                 Value = "a@b.com",
        //                 ItemType = "work",
        //                 Primary = true
        //             },
        //             new ElectronicMailAddress
        //             {
        //                 Value = "test@gmail.com",
        //                 ItemType = "home",
        //                 Primary = false
        //             }
        //         }
        //     };

        //     // Act
        //     var result = JSONParserUtil<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

        //     // Assert
        //     Assert.NotNull(result);
        //     Assert.True(result.ContainsKey("Emails"));

        //     var emailsArray = result["Emails"] as JArray;
        //     Assert.NotNull(emailsArray);
        //     Assert.Equal(2, emailsArray.Count);

        //     // Assert
        //     var expectedEmails = JArray.Parse(@"[
        //                         ""a@b.com"",
        //                         ""test@gmail.com""
        //                     ]");
        //     var actualJson = JObject.FromObject(result);

        //     var actualEmails = actualJson["Emails"] as JArray;

        //     Assert.NotNull(actualEmails);
        //     Assert.Equal(expectedEmails, actualEmails, JToken.EqualityComparer);
        // }

        [Fact]
        public void Parse_ComplexArray_ReturnsArrayOfObjectExpectedJson_5()
        {
            // Arrange
            var AttributeSchemas = new List<AttributeSchema>
            {
                new AttributeSchema
                {
                    DestinationType = AttributeDataTypes.Array,
                    DestinationField = "urn:kn:ki:schema:Emails",
                    SourceValue = "ElectronicMailAddresses",
                    ArrayDataType = AttributeDataTypes.Object,
                    ChildSchemas = new List<AttributeSchema>
                    {
                        new AttributeSchema
                        {
                            DestinationType = AttributeDataTypes.String,
                            DestinationField = "urn:kn:ki:schema:Emails:Value",
                            SourceValue = "Value"
                        },
                        new AttributeSchema
                        {
                            DestinationType = AttributeDataTypes.String,
                            DestinationField = "urn:kn:ki:schema:Emails:ItemType",
                            SourceValue = "ItemType"
                        },
                        new AttributeSchema
                        {
                            DestinationType = AttributeDataTypes.Boolean,
                            DestinationField = "urn:kn:ki:schema:Emails:Primary",
                            SourceValue = "Primary"
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
            var result = JSONParserUtil<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);

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
            var AttributeSchemas = new List<AttributeSchema>
            {
                new AttributeSchema { DestinationField = "urn:kn:ki:schema:UserName", SourceValue = "UserName" },
                new AttributeSchema { DestinationField = "urn:kn:ki:schema:DisplayName", SourceValue = "DisplayName" },
                new AttributeSchema {
                 DestinationType = AttributeDataTypes.Object,
                 DestinationField = "urn:kn:ki:schema:NameDetail",
                 SourceValue = "Name",
                 ChildSchemas = new List<AttributeSchema>
                 {
                        new AttributeSchema { DestinationField = "urn:kn:ki:schema:NameDetail:FirstName", SourceValue = "Name:GivenName" },
                        new AttributeSchema { DestinationField = "urn:kn:ki:schema:NameDetail:LastName", SourceValue = "Name:FamilyName" },
                        new AttributeSchema { DestinationField = "urn:kn:ki:schema:NameDetail:FullName", SourceValue = "Name:Formatted" }
                 }

                },

                new AttributeSchema
                {
                 DestinationType = AttributeDataTypes.Object,
                 DestinationField = "urn:kn:ki:schema:ExtraInfo",
                 SourceValue = "EnterpriseExtension",
                 ChildSchemas = new List<AttributeSchema>
                 {
                        new AttributeSchema { DestinationField = "urn:kn:ki:schema:ExtraInfo:Department", SourceValue = "EnterpriseExtension:Department" },
                        new AttributeSchema{ DestinationField = "urn:kn:ki:schema:ExtraInfo:Position", SourceValue = "Title" },
                        new AttributeSchema {
                            DestinationType = AttributeDataTypes.Object,
                            DestinationField = "urn:kn:ki:schema:ExtraInfo:Manager",
                            SourceValue = "EnterpriseExtension:Manager",

                            ChildSchemas = new List<AttributeSchema>
                            {
                                new AttributeSchema { DestinationField = "urn:kn:ki:schema:ExtraInfo:Manager:Name", SourceValue = "EnterpriseExtension:Manager:Value" },
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
            var result = JSONParserUtil<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);
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

        // [Fact]
        // public void Parse_ComplexObjectWithArray_ReturnsExpectedJson_7()
        // {
        //     // Arrange
        //     var AttributeSchemas = new List<AttributeSchema>
        //     {
        //         new AttributeSchema { DestinationField = "urn:kn:ki:schema:GroupName", SourceValue = "DisplayName" },
        //         new AttributeSchema { DestinationField = "urn:kn:ki:schema:Id", SourceValue = "Identifier" },
        //         new AttributeSchema
        //         {
        //          DestinationType = JsonDataTypes.Array,
        //          DestinationField = "urn:kn:ki:schema:Members",
        //          SourceValue = "Members",
        //          ArrayDataType = JsonDataTypes.Object,
        //          ChildSchemas = new List<AttributeSchema>
        //          {
        //                 new AttributeSchema { DestinationField = "urn:kn:ki:schema:Members:Email", SourceValue = "Members:Value" },
        //                 new AttributeSchema { DestinationField = "urn:kn:ki:schema:Members:Type", SourceValue = "Members:TypeName" }
        //          }
        //         },
        //         new AttributeSchema
        //         {
        //             DestinationType = JsonDataTypes.Array,
        //             DestinationField = "urn:kn:ki:schema:Emails",
        //             SourceValue = "Members",
        //             ArrayDataType = JsonDataTypes.String,
        //             ArrayElementMappingField = "Members:Value"
        //         }
        //     };

        //     var resource = new Core2Group
        //     {
        //         DisplayName = "Group 1",
        //         Identifier = "group1",
        //         Members = new List<Member>
        //         {
        //             new Member
        //             {
        //                 Value = "user1@gmail.com",
        //                 TypeName = "User"
        //             },
        //             new Member
        //             {
        //                 Value = "user2@gmail.com",
        //                 TypeName = "User"
        //             }
        //         }
        //     };

        //     // Act
        //     var result = JSONParserUtil<Core2Group>.Parse(AttributeSchemas, resource);
        //     Assert.NotNull(result);

        //     var expectedJson = JObject.Parse(@"{
        //         ""GroupName"": ""Group 1"",
        //         ""Id"": ""group1"",
        //         ""Members"": [
        //             {
        //                 ""Email"": ""user1@gmail.com"",
        //                 ""Type"": ""User""
        //             },
        //             {
        //                 ""Email"": ""user2@gmail.com"",
        //                 ""Type"": ""User""
        //             }
        //         ],
        //        ""Emails"": [
        //              ""user1@gmail.com"",
        //              ""user2@gmail.com""
        //         ]
        //         }");

        //     var actualJson = JObject.FromObject(result);
        //     Assert.Equal(expectedJson, actualJson);
        // }

        // [Fact]
        //     public void Parse_SimpleComplexObjectArray_ReturnsExpectedJson_8()
        //     {
        //         // Arrange
        //         var AttributeSchemas = new List<AttributeSchema>
        //         {
        //             new AttributeSchema { DestinationField = "urn:kn:ki:schema:UserName", SourceValue = "UserName" },
        //             new AttributeSchema { DestinationField = "urn:kn:ki:schema:DisplayName", SourceValue = "DisplayName" },
        //             new AttributeSchema { DestinationType = JsonDataTypes.Boolean, DestinationField = "urn:kn:ki:schema:Active", SourceValue = "Active" },
        //             new AttributeSchema { DestinationField = "urn:kn:ki:schema:Id", SourceValue = "Identifier" },
        //             new AttributeSchema { DestinationField = "urn:kn:ki:schema:ExternalId", SourceValue = "ExternalIdentifier" },
        //             new AttributeSchema { DestinationField = "urn:kn:ki:schema:UserType", SourceValue = "UserType" },
        //             new AttributeSchema { DestinationField = "urn:kn:ki:schema:Title", SourceValue = "Title" },

        //             new AttributeSchema {
        //              DestinationType = JsonDataTypes.Object,
        //              DestinationField = "urn:kn:ki:schema:NameDetail",
        //              SourceValue = "Name",
        //              ChildSchemas = new List<AttributeSchema>
        //              {
        //                     new AttributeSchema { DestinationField = "urn:kn:ki:schema:NameDetail:FirstName", SourceValue = "Name:GivenName" },
        //                     new AttributeSchema { DestinationField = "urn:kn:ki:schema:NameDetail:LastName", SourceValue = "Name:FamilyName" },
        //                     new AttributeSchema { DestinationField = "urn:kn:ki:schema:NameDetail:FullName", SourceValue = "Name:Formatted" }
        //              }

        //             },

        //             new AttributeSchema
        //             {
        //              DestinationType = JsonDataTypes.Object,
        //              DestinationField = "urn:kn:ki:schema:ExtraInfo",
        //              SourceValue = "EnterpriseExtension",
        //              ChildSchemas = new List<AttributeSchema>
        //              {
        //                     new AttributeSchema { DestinationField = "urn:kn:ki:schema:ExtraInfo:Department", SourceValue = "EnterpriseExtension:Department" },
        //                     new AttributeSchema{ DestinationField = "urn:kn:ki:schema:ExtraInfo:Position", SourceValue = "Title" },
        //                     new AttributeSchema {
        //                         DestinationType = JsonDataTypes.Object,
        //                         DestinationField = "urn:kn:ki:schema:ExtraInfo:Manager",
        //                         SourceValue = "EnterpriseExtension:Manager",

        //                         ChildSchemas = new List<AttributeSchema>
        //                         {
        //                             new AttributeSchema { DestinationField = "urn:kn:ki:schema:ExtraInfo:Manager:Name", SourceValue = "EnterpriseExtension:Manager:Value" },
        //                         }
        //                     }
        //              }

        //             },
        //             new AttributeSchema
        //             {
        //                 DestinationType = JsonDataTypes.Array,
        //                 DestinationField = "urn:kn:ki:schema:Emails",
        //                 SourceValue = "ElectronicMailAddresses",
        //                 ArrayDataType = JsonDataTypes.Object,
        //                 ChildSchemas = new List<AttributeSchema>
        //                 {
        //                     new AttributeSchema
        //                     {
        //                         DestinationType = JsonDataTypes.String,
        //                         DestinationField = "urn:kn:ki:schema:Emails:Value",
        //                         SourceValue = "Value"
        //                     },
        //                     new AttributeSchema
        //                     {
        //                         DestinationType = JsonDataTypes.String,
        //                         DestinationField = "urn:kn:ki:schema:Emails:ItemType",
        //                         SourceValue = "ItemType"
        //                     },
        //                     new AttributeSchema
        //                     {
        //                         DestinationType = JsonDataTypes.Boolean,
        //                         DestinationField = "urn:kn:ki:schema:Emails:Primary",
        //                         SourceValue = "Primary"
        //                     }
        //                 }
        //             },
        //              new AttributeSchema
        //             {
        //                 DestinationType = JsonDataTypes.Array,
        //                 DestinationField = "urn:kn:ki:schema:UserEmails",
        //                 SourceValue = "ElectronicMailAddresses",
        //                 ArrayDataType = JsonDataTypes.String,
        //                 ArrayElementMappingField = "ElectronicMailAddresses:Value"
        //             },

        //            new AttributeSchema
        //            {
        //                DestinationType = JsonDataTypes.Array,
        //                DestinationField = "urn:kn:ki:schema:Addresses",
        //                SourceValue = "Addresses",
        //                ArrayDataType = JsonDataTypes.Object,
        //                ChildSchemas = new List<AttributeSchema>
        //                {
        //                       new AttributeSchema
        //                       {
        //                         DestinationType = JsonDataTypes.String,
        //                         DestinationField = "urn:kn:ki:schema:Addresses:StreetAddress",
        //                         SourceValue = "StreetAddress"
        //                       },
        //                       new AttributeSchema
        //                       {
        //                         DestinationType = JsonDataTypes.String,
        //                         DestinationField = "urn:kn:ki:schema:Addresses:Locality",
        //                         SourceValue = "Locality"
        //                       },
        //                       new AttributeSchema
        //                       {
        //                         DestinationType = JsonDataTypes.String,
        //                         DestinationField = "urn:kn:ki:schema:Addresses:Region",
        //                         SourceValue = "Region"
        //                       },
        //                       new AttributeSchema
        //                       {
        //                         DestinationType = JsonDataTypes.String,
        //                         DestinationField = "urn:kn:ki:schema:Addresses:PostalCode",
        //                         SourceValue = "PostalCode"
        //                       }
        //                  }
        //            },

        //            new AttributeSchema
        //            {
        //                  DestinationType = JsonDataTypes.Array,
        //                  DestinationField = "urn:kn:ki:schema:Roles",
        //                  SourceValue = "Roles",
        //                  ArrayDataType = JsonDataTypes.String,
        //                  ArrayElementMappingField = "Roles:Value"
        //             },

        //         };

        //         var resource = new Core2EnterpriseUser
        //         {
        //             UserName = "user1",
        //             DisplayName = "User 1",
        //             Title = "Software Engineer",
        //             Name = new Name
        //             {
        //                 GivenName = "Test",
        //                 FamilyName = "User",
        //                 Formatted = "Test User"
        //             },
        //             EnterpriseExtension = new ExtensionAttributeEnterpriseUser2
        //             {
        //                 Department = "IT",
        //                 Manager = new Manager
        //                 {
        //                     Value = "user2"
        //                 }
        //             },
        //             ElectronicMailAddresses = new List<ElectronicMailAddress>
        //             {
        //                 new ElectronicMailAddress
        //                 {
        //                     Value = "work@gmail.com",
        //                     ItemType = "work",
        //                     Primary = true
        //                 },
        //                 new ElectronicMailAddress
        //                 {
        //                     Value = "home@gmail.com",
        //                     ItemType = "home",
        //                     Primary = false
        //                 }
        //             },
        //             Addresses = new List<Address>
        //             {
        //                 new Address
        //                 {
        //                     StreetAddress = "123 Main St",
        //                     Locality = "Anytown",
        //                     Region = "CA",
        //                     PostalCode = "12345"
        //                 },
        //                 new Address
        //                 {
        //                     StreetAddress = "456 Main St",
        //                     Locality = "Anytown",
        //                     Region = "CA",
        //                     PostalCode = "12345"
        //                 }
        //             },
        //             Roles = new List<Role>
        //             {
        //                 new Role
        //                 {
        //                     Value = "role1",
        //                     ItemType = "Role",
        //                     Display = "Role 1",
        //                     Primary = true
        //                 },
        //                 new Role
        //                 {
        //                     Value = "role2",
        //                     ItemType = "Role",
        //                     Display = "Role 2",
        //                     Primary = false
        //                 }
        //             },

        //             Active = true,
        //             Identifier = "user1",
        //             ExternalIdentifier = "user1",
        //             UserType = "Employee"
        //         };

        //         // Act
        //         var result = JSONParserUtil<Core2EnterpriseUser>.Parse(AttributeSchemas, resource);
        //         Assert.NotNull(result);

        //         // Assert
        //         var expectedJson = JObject.Parse(@"{
        //             ""UserName"": ""user1"",
        //             ""DisplayName"": ""User 1"",
        //             ""Active"": true,
        //             ""Id"": ""user1"",
        //             ""ExternalId"": ""user1"",
        //             ""UserType"": ""Employee"",
        //             ""Title"": ""Software Engineer"",
        //             ""NameDetail"": {
        //                 ""FirstName"": ""Test"",
        //                 ""LastName"": ""User"",
        //                 ""FullName"": ""Test User""
        //             },
        //             ""ExtraInfo"": {
        //                 ""Department"": ""IT"",
        //                 ""Position"": ""Software Engineer"",
        //                 ""Manager"": {
        //                     ""Name"": ""user2""
        //                 }
        //             },
        //              ""Emails"": [
        //                 {
        //                     ""Value"": ""work@gmail.com"",
        //                     ""ItemType"": ""work"",
        //                     ""Primary"": true
        //                 },
        //                 {
        //                     ""Value"": ""home@gmail.com"",
        //                     ""ItemType"": ""home"",
        //                     ""Primary"": false
        //                 }
        //             ],
        //             ""Addresses"": [
        //                 {
        //                     ""StreetAddress"": ""123 Main St"",
        //                     ""Locality"": ""Anytown"",
        //                     ""Region"": ""CA"",
        //                     ""PostalCode"": ""12345""
        //                 },
        //                 {
        //                     ""StreetAddress"": ""456 Main St"",
        //                     ""Locality"": ""Anytown"",
        //                     ""Region"": ""CA"",
        //                     ""PostalCode"": ""12345""
        //                 }
        //             ],
        //             ""Roles"": [
        //                 ""role1"",
        //                 ""role2""
        //             ],
        //             ""UserEmails"":[
        //                  ""work@gmail.com"",
        //                  ""home@gmail.com""
        //             ]
        //         }");

        //         var actualJson = JObject.FromObject(result);
        //         Assert.True(JToken.DeepEquals(expectedJson, actualJson));
        //     }

        // }
    }
}