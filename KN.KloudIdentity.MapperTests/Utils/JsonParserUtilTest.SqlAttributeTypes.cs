using KN.KloudIdentity.Mapper;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using Microsoft.Kiota.Abstractions;
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.MapperTests;

public partial class JSONParserUtilTests
{

    [Fact]
    public void GetValue_SQLDataType_VarChar_ReturnsParsedJson()
    {
        // Arrange
        var AttributeSchema = new List<AttributeSchema> 
        { 
            new AttributeSchema { DestinationField = "urn:kn:ki:schema:name", SourceValue = "DisplayName", DestinationType = AttributeDataTypes.VarChar },
            new AttributeSchema { DestinationField = "urn:kn:ki:schema:email", SourceValue = "UserName", DestinationType = AttributeDataTypes.VarChar }
        };

        //NVarChar can store Unicode characters.
        var resource = new Core2EnterpriseUser
        {
            DisplayName = "John David",
            UserName = "john.d@mail.com"
        };       

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchema, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
            ""name"": ""John David"",
            ""email"": ""john.d@mail.com""
        }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void GetValue_SQLDataType_Char_ReturnsParsedJson()
    {
        // Arrange
        var AttributeSchema = new List<AttributeSchema>
        {
            new AttributeSchema { DestinationField = "urn:kn:ki:schema:name", SourceValue = "DisplayName", DestinationType = AttributeDataTypes.Char },
            new AttributeSchema { DestinationField = "urn:kn:ki:schema:email", SourceValue = "UserName", DestinationType = AttributeDataTypes.Char }
        };

        //NVarChar can store Unicode characters.
        var resource = new Core2EnterpriseUser
        {
            DisplayName = "John David",
            UserName = "john.d@mail.com"
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchema, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
            ""name"": ""John David"",
            ""email"": ""john.d@mail.com""
        }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void GetValue_SQLDataType_NVarChar_ReturnsParsedJson()
    {
        // Arrange
        var AttributeSchema = new List<AttributeSchema>
        {
            new AttributeSchema { DestinationField = "urn:kn:ki:schema:name", SourceValue = "DisplayName", DestinationType = AttributeDataTypes.NVarChar },
            new AttributeSchema { DestinationField = "urn:kn:ki:schema:email", SourceValue = "UserName", DestinationType = AttributeDataTypes.NVarChar }
        };

        //NVarChar can store Unicode characters.
        var resource = new Core2EnterpriseUser
        {
            DisplayName = "你好 你好",
            UserName = "john.d@mail.com"
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchema, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
            ""name"": ""你好 你好"",
            ""email"": ""john.d@mail.com""
        }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void GetValue_SQLDataType_NChar_ReturnsParsedJson()
    {
        // Arrange
        var AttributeSchema = new List<AttributeSchema>
        {
            new AttributeSchema { DestinationField = "urn:kn:ki:schema:name", SourceValue = "DisplayName", DestinationType = AttributeDataTypes.NChar },
            new AttributeSchema { DestinationField = "urn:kn:ki:schema:email", SourceValue = "UserName", DestinationType = AttributeDataTypes.NChar }
        };

        //NVarChar can store Unicode characters.
        var resource = new Core2EnterpriseUser
        {
            DisplayName = "中华人民共和国",
            UserName = "john.d@mail.com"
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchema, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
            ""name"": ""中华人民共和国"",
            ""email"": ""john.d@mail.com""
        }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void GetValue_SQLDataType_NText_ReturnsParsedJson()
    {
        // Arrange
        var AttributeSchema = new List<AttributeSchema>
        {
            new AttributeSchema { DestinationField = "urn:kn:ki:schema:name", SourceValue = "DisplayName", DestinationType = AttributeDataTypes.NText },
            new AttributeSchema { DestinationField = "urn:kn:ki:schema:email", SourceValue = "UserName", DestinationType = AttributeDataTypes.NText }
        };

        //NVarChar can store Unicode characters.
        var resource = new Core2EnterpriseUser
        {
            DisplayName = "你好 你好",
            UserName = "john.d@mail.com"
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchema, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
            ""name"": ""你好 你好"",
            ""email"": ""john.d@mail.com""
        }");

        Assert.Equal(expectedJson, result);        
    }

    [Fact]
    public void GetValue_SQLDataType_Text_ReturnsParsedJson()
    {
        // Arrange
        var AttributeSchema = new List<AttributeSchema>
        {
            new AttributeSchema { DestinationField = "urn:kn:ki:schema:name", SourceValue = "DisplayName", DestinationType = AttributeDataTypes.Text },
            new AttributeSchema { DestinationField = "urn:kn:ki:schema:email", SourceValue = "UserName", DestinationType = AttributeDataTypes.Text }
        };

        //NVarChar can store Unicode characters.
        var resource = new Core2EnterpriseUser
        {
            DisplayName = "John David",
            UserName = "john.d@mail.com"
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchema, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
            ""name"": ""John David"",
            ""email"": ""john.d@mail.com""
        }");

        Assert.Equal(expectedJson, result);
    }


    [Fact]
    public void GetValue_SQLDataType_Bit_ReturnsParsedJson()
    {
        // Arrange
        var AttributeSchema = new List<AttributeSchema>
        {
            new AttributeSchema { DestinationField = "urn:kn:ki:schema:active", SourceValue = "Active", DestinationType = AttributeDataTypes.Bit }
           
        };


        var resource = new Core2EnterpriseUser
        {
            Active = true
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchema, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
            ""active"": true
        }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void GetValue_SQLDataType_BigInt_ReturnsParsedJson()
    {
        // Arrange
        var AttributeSchema = new List<AttributeSchema>
        {
            new AttributeSchema { DestinationField = "urn:kn:ki:schema:id", SourceValue = "Identifier", DestinationType = AttributeDataTypes.BigInt },
            new AttributeSchema { DestinationField = "urn:kn:ki:schema:email", SourceValue = "UserName", DestinationType = AttributeDataTypes.Text }
        };

        //NVarChar can store Unicode characters.
        var resource = new Core2EnterpriseUser
        {
            Identifier = "1",
            UserName = "john.d@mail.com"
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchema, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
            ""id"": 1,
            ""email"": ""john.d@mail.com""
        }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void GetValue_SQLDataType_Numeric_ReturnsParsedJson()
    {
        // Arrange
        var AttributeSchema = new List<AttributeSchema>
        {
            new AttributeSchema { DestinationField = "urn:kn:ki:schema:id", SourceValue = "Identifier", DestinationType = AttributeDataTypes.Numeric },
            new AttributeSchema { DestinationField = "urn:kn:ki:schema:email", SourceValue = "UserName", DestinationType = AttributeDataTypes.Text }
        };

        //NVarChar can store Unicode characters.
        var resource = new Core2EnterpriseUser
        {
            Identifier = "1",
            UserName = "john.d@mail.com"
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchema, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
            ""id"": 1,
            ""email"": ""john.d@mail.com""
        }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void GetValue_SQLDataType_Int_ReturnsParsedJson()
    {
        // Arrange
        var AttributeSchema = new List<AttributeSchema>
        {
            new AttributeSchema { DestinationField = "urn:kn:ki:schema:id", SourceValue = "Identifier", DestinationType = AttributeDataTypes.Int },
            new AttributeSchema { DestinationField = "urn:kn:ki:schema:email", SourceValue = "UserName", DestinationType = AttributeDataTypes.Text }
        };

        //NVarChar can store Unicode characters.
        var resource = new Core2EnterpriseUser
        {
            Identifier = "1",
            UserName = "john.d@mail.com"
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchema, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
            ""id"": 1,
            ""email"": ""john.d@mail.com""
        }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void GetValue_SQLDataType_SmallInt_ReturnsParsedJson()
    {
        // Arrange
        var AttributeSchema = new List<AttributeSchema>
        {
            new AttributeSchema { DestinationField = "urn:kn:ki:schema:id", SourceValue = "Identifier", DestinationType = AttributeDataTypes.SmallInt },
            new AttributeSchema { DestinationField = "urn:kn:ki:schema:email", SourceValue = "UserName", DestinationType = AttributeDataTypes.Text }
        };

        //NVarChar can store Unicode characters.
        var resource = new Core2EnterpriseUser
        {
            Identifier = "1",
            UserName = "john.d@mail.com"
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchema, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
            ""id"": 1,
            ""email"": ""john.d@mail.com""
        }");

        Assert.Equal(expectedJson, result);
    }  

    [Fact]
    public void GetValue_SQLDataType_Double_ReturnsParsedJson()
    {
        // Arrange
        var AttributeSchema = new List<AttributeSchema>
        {
            new AttributeSchema { DestinationField = "urn:kn:ki:schema:id", SourceValue = "Identifier", DestinationType = AttributeDataTypes.Double },
            new AttributeSchema { DestinationField = "urn:kn:ki:schema:email", SourceValue = "UserName", DestinationType = AttributeDataTypes.Text }
        };

        //NVarChar can store Unicode characters.
        var resource = new Core2EnterpriseUser
        {
            Identifier = "1.0",
            UserName = "john.d@mail.com"
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchema, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
            ""id"": 1.0,
            ""email"": ""john.d@mail.com""
        }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void GetValue_SQLDataType_Real_ReturnsParsedJson()
    {
        // Arrange
        var AttributeSchema = new List<AttributeSchema>
        {
            new AttributeSchema { DestinationField = "urn:kn:ki:schema:id", SourceValue = "Identifier", DestinationType = AttributeDataTypes.Real },
            new AttributeSchema { DestinationField = "urn:kn:ki:schema:email", SourceValue = "UserName", DestinationType = AttributeDataTypes.Text }
        };

        //NVarChar can store Unicode characters.
        var resource = new Core2EnterpriseUser
        {
            Identifier = "1.5",
            UserName = "john.d@mail.com"
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchema, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
            ""id"": 1.5,
            ""email"": ""john.d@mail.com""
        }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void GetValue_SQLDataType_Decimal_ReturnsParsedJson()
    {
        // Arrange
        var AttributeSchema = new List<AttributeSchema>
        {
            new AttributeSchema { DestinationField = "urn:kn:ki:schema:id", SourceValue = "Identifier", DestinationType = AttributeDataTypes.Decimal },
            new AttributeSchema { DestinationField = "urn:kn:ki:schema:email", SourceValue = "UserName", DestinationType = AttributeDataTypes.Text }
        };

        //NVarChar can store Unicode characters.
        var resource = new Core2EnterpriseUser
        {
            Identifier = "1.8",
            UserName = "john.d@mail.com"
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchema, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
            ""id"": 1.8,
            ""email"": ""john.d@mail.com""
        }");

        Assert.Equal(expectedJson, result);
    }


    [Fact]
    public void GetValue_SQLDataType_DateTime_ReturnsParsedJson()
    {
        // Arrange
        var AttributeSchema = new List<AttributeSchema>
        {
            new AttributeSchema { DestinationField = "urn:kn:ki:schema:locale", SourceValue = "Locale", DestinationType = AttributeDataTypes.DateTime },
            new AttributeSchema { DestinationField = "urn:kn:ki:schema:email", SourceValue = "UserName", DestinationType = AttributeDataTypes.Text }
        };

        //NVarChar can store Unicode characters.
        var resource = new Core2EnterpriseUser
        {
            Locale = new DateTime(2025, 1, 22, 14, 33, 12).ToString(),
            UserName = "john.d@mail.com"
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchema, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
            ""locale"": '2025-01-22T14:33:12.0000000',
            ""email"": ""john.d@mail.com""
        }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void GetValue_SQLDataType_Time_ReturnsParsedJson()
    {
        // Arrange
        var AttributeSchema = new List<AttributeSchema>
        {
            new AttributeSchema { DestinationField = "urn:kn:ki:schema:locale", SourceValue = "Locale", DestinationType = AttributeDataTypes.Time },
            new AttributeSchema { DestinationField = "urn:kn:ki:schema:email", SourceValue = "UserName", DestinationType = AttributeDataTypes.Text }
        };

        //NVarChar can store Unicode characters.
        var resource = new Core2EnterpriseUser
        {
            Locale = new Time(3, 1, 22).ToString(),
            UserName = "john.d@mail.com"
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchema, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
            ""locale"": '03:01:22.0000000',
            ""email"": ""john.d@mail.com""
        }");

        Assert.Equal(expectedJson, result);
    }

    [Fact]
    public void GetValue_SQLDataType_Date_ReturnsParsedJson()
    {
        // Arrange
        var AttributeSchema = new List<AttributeSchema>
        {
            new AttributeSchema { DestinationField = "urn:kn:ki:schema:locale", SourceValue = "Locale", DestinationType = AttributeDataTypes.Date },
            new AttributeSchema { DestinationField = "urn:kn:ki:schema:email", SourceValue = "UserName", DestinationType = AttributeDataTypes.Text }
        };

        //NVarChar can store Unicode characters.
        var resource = new Core2EnterpriseUser
        {
            Locale = new Date(2025, 1, 22).ToString(),
            UserName = "john.d@mail.com"
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.Parse(AttributeSchema, resource);

        // Assert
        var expectedJson = JObject.Parse(@"{
            ""locale"": '2025-01-22',
            ""email"": ""john.d@mail.com""
        }");

        Assert.Equal(expectedJson, result);
    }
    
    [Fact]
    public void GetValue_SQLDataType_BinaryType_ThrowsNotSupportedException()
    {
        // Arrange
        var schemaAttribute = new AttributeSchema
        {
            DestinationType = AttributeDataTypes.Binary,
            SourceValue = "BinaryProperty"
        };

        var resource = new Core2EnterpriseUser
        {
            DisplayName = "John David"
        };       

        // Act & Assert
        Assert.Throws<NotSupportedException>(() =>
            JSONParserUtilV2<Core2EnterpriseUser>.GetValue(resource, schemaAttribute));
    }
}