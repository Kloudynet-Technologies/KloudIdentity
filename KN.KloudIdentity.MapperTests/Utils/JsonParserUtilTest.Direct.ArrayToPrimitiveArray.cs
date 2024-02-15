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
                    DestinationType = JsonDataTypes.Number,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "UserName",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:usernames",
                    IsRequired = true,
                    DestinationType = JsonDataTypes.Array,
                    ArrayDataType = JsonDataTypes.String,
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
                    DestinationType = JsonDataTypes.Number,
                    MappingCondition = new MappingCondition { Condition = MappingConditions.Always }
                },
                new AttributeSchema
                {
                    MappingType = MappingTypes.Direct,
                    SourceValue = "Addresses",
                    DefaultValue = "N/A",
                    DestinationField = "urn:kn:ki:schema:streetAddresses",
                    IsRequired = true,
                    DestinationType = JsonDataTypes.Array,
                    ArrayDataType = JsonDataTypes.String,
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
}
