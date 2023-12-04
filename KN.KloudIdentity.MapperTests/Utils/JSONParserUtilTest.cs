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
        public void Parse_ComplexObject_ReturnsExpectedJson_4()
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
        public void Parse_ComplexObject_ReturnsExpectedJson_5()
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

    }
}