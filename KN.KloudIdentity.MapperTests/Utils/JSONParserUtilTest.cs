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
    }
}