using KN.KloudIdentity.Mapper;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.Kiota.Abstractions;
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KN.KloudIdentity.MapperTests;

public partial class JSONParserUtilTests
{

    [Fact]
    public void GetValue_SQLDataType_VarChar_ReturnsParsedJson()
    {
        // Arrange
        var AttributeSchema = new AttributeSchema { DestinationField = "urn:kn:ki:schema:name", SourceValue = "DisplayName", DestinationType = AttributeDataTypes.NChar };

        //NVarChar can store Unicode characters.
        var resource = new Core2EnterpriseUser
        {
            DisplayName = "John David"
        };       

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.GetValue(resource, AttributeSchema);

        // Assert
        var expectedValue = "John David";
        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public void GetValue_SQLDataType_Char_ReturnsParsedJson()
    {
        // Arrange
        var AttributeSchema = new AttributeSchema { DestinationField = "urn:kn:ki:schema:usertype", SourceValue = "UserType" , DestinationType = AttributeDataTypes.Char};

        var resource = new Core2EnterpriseUser
        {
            UserType = "Admin"
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.GetValue(resource, AttributeSchema);

        // Assert
        var expectedValue = "Admin";
        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public void GetValue_SQLDataType_NVarChar_ReturnsParsedJson()
    {
        // Arrange
        var AttributeSchema = new AttributeSchema { DestinationField = "urn:kn:ki:schema:name", SourceValue = "DisplayName", DestinationType = AttributeDataTypes.NChar };

        //NVarChar can store Unicode characters.
        var resource = new Core2EnterpriseUser {
            DisplayName = "你好 你好"
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.GetValue(resource, AttributeSchema);

        // Assert
        var expectedValue = "你好 你好";
        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public void GetValue_SQLDataType_NChar_ReturnsParsedJson()
    {
        // Arrange
        var AttributeSchema = new AttributeSchema { DestinationField = "urn:kn:ki:schema:name", SourceValue = "DisplayName" , DestinationType = AttributeDataTypes.NChar};

        //NChar can store Unicode characters.
        var resource = new Core2EnterpriseUser
        {
            DisplayName = "中华人民共和国"
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.GetValue(resource, AttributeSchema);

        // Assert
        var expectedValue = "中华人民共和国";
        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public void GetValue_SQLDataType_NText_ReturnsParsedJson()
    {
        // Arrange
        var AttributeSchema = new AttributeSchema { DestinationField = "urn:kn:ki:schema:name", SourceValue = "DisplayName" , DestinationType = AttributeDataTypes.NText};

        //NText can store Unicode characters.
        var resource = new Core2EnterpriseUser
        {
            DisplayName = "你好 你好"
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.GetValue(resource, AttributeSchema);

        // Assert
        var expectedValue = "你好 你好";
        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public void GetValue_SQLDataType_Text_ReturnsParsedJson()
    {
        // Arrange
        var AttributeSchema = new AttributeSchema { DestinationField = "urn:kn:ki:schema:name", SourceValue = "DisplayName" , DestinationType = AttributeDataTypes.Text};
 
        var resource = new Core2EnterpriseUser
        {
            DisplayName = "John David"
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.GetValue(resource, AttributeSchema);

        // Assert
        var expectedValue = "John David";
        Assert.Equal(expectedValue, result);
    }


    [Fact]
    public void GetValue_SQLDataType_Bit_ReturnsParsedJson()
    {
        // Arrange
        var AttributeSchema = new AttributeSchema { DestinationField = "urn:kn:ki:schema:active", SourceValue = "Active", DestinationType = AttributeDataTypes.Bit };
        
      
        var resource = new Core2EnterpriseUser
        {
            Active = true
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.GetValue(resource, AttributeSchema);

        // Assert
        var expectedValue = true;
        Assert.Equal(expectedValue, result);
    }


    [Fact]
    public void GetValue_SQLDataType_Number_ReturnsParsedJson()
    {
        // Arrange
       var AttributeSchema = new AttributeSchema { DestinationField = "urn:kn:ki:schema:id", SourceValue = "Identifier", DestinationType = AttributeDataTypes.Number };


        var resource = new Core2EnterpriseUser
        {                   
            Identifier = "1"
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.GetValue(resource, AttributeSchema);

        // Assert
        var expectedValue = 1;
        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public void GetValue_SQLDataType_BigInt_ReturnsParsedJson()
    {
        // Arrange
        var AttributeSchema = new AttributeSchema { DestinationField = "urn:kn:ki:schema:id", SourceValue = "Identifier", DestinationType = AttributeDataTypes.BigInt };


        var resource = new Core2EnterpriseUser
        {
            Identifier = "1"
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.GetValue(resource, AttributeSchema);

        // Assert
        var expectedValue = 1;
        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public void GetValue_SQLDataType_Numeric_ReturnsParsedJson()
    {
        // Arrange
        var AttributeSchema = new AttributeSchema { DestinationField = "urn:kn:ki:schema:id", SourceValue = "Identifier", DestinationType = AttributeDataTypes.Numeric };


        var resource = new Core2EnterpriseUser
        {
            Identifier = "1"
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.GetValue(resource, AttributeSchema);

        // Assert
        var expectedValue = 1;
        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public void GetValue_SQLDataType_Int_ReturnsParsedJson()
    {
        // Arrange
        var AttributeSchema = new AttributeSchema { DestinationField = "urn:kn:ki:schema:id", SourceValue = "Identifier", DestinationType = AttributeDataTypes.Int };


        var resource = new Core2EnterpriseUser
        {
            Identifier = "1"
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.GetValue(resource, AttributeSchema);

        // Assert
        var expectedValue = 1;
        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public void GetValue_SQLDataType_SmallInt_ReturnsParsedJson()
    {
        // Arrange
        var AttributeSchema = new AttributeSchema { DestinationField = "urn:kn:ki:schema:id", SourceValue = "Identifier", DestinationType = AttributeDataTypes.SmallInt };


        var resource = new Core2EnterpriseUser
        {
            Identifier = "1"
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.GetValue(resource, AttributeSchema);

        // Assert
        var expectedValue = 1;
        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public void GetValue_SQLDataType_TinyInt_ReturnsParsedJson()
    {
        // Arrange
        var AttributeSchema = new AttributeSchema { DestinationField = "urn:kn:ki:schema:id", SourceValue = "Identifier", DestinationType = AttributeDataTypes.TinyInt };


        var resource = new Core2EnterpriseUser
        {
            Identifier = "1"
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.GetValue(resource, AttributeSchema);

        // Assert
        var expectedValue = 1;
        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public void GetValue_SQLDataType_Double_ReturnsParsedJson()
    {
        // Arrange
        var AttributeSchema = new AttributeSchema { DestinationField = "urn:kn:ki:schema:id", SourceValue = "Identifier", DestinationType = AttributeDataTypes.Double };


        var resource = new Core2EnterpriseUser
        {
            Identifier = "1.0"
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.GetValue(resource, AttributeSchema);

        // Assert
        var expectedValue = 1.0;
        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public void GetValue_SQLDataType_Real_ReturnsParsedJson()
    {
        // Arrange
        var AttributeSchema = new AttributeSchema { DestinationField = "urn:kn:ki:schema:id", SourceValue = "Identifier", DestinationType = AttributeDataTypes.Real };


        var resource = new Core2EnterpriseUser
        {
            Identifier = "1.0"
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.GetValue(resource, AttributeSchema);

        // Assert
        var expectedValue = 1.0;
        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public void GetValue_SQLDataType_Decimal_ReturnsParsedJson()
    {
        // Arrange
        var AttributeSchema = new AttributeSchema { DestinationField = "urn:kn:ki:schema:locale", SourceValue = "Locale", DestinationType = AttributeDataTypes.Decimal };


        var resource = new Core2EnterpriseUser
        {
            Locale = "1.0"
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.GetValue(resource, AttributeSchema);

        // Assert
        var expectedValue = 1.0;
        Assert.Equal(expectedValue, result);
    }


    [Fact]
    public void GetValue_SQLDataType_DateTime_ReturnsParsedJson()
    {
        // Arrange
        var AttributeSchema = new AttributeSchema { DestinationField = "urn:kn:ki:schema:locale", SourceValue = "Locale", DestinationType = AttributeDataTypes.DateTime };


        var resource = new Core2EnterpriseUser
        {
            Locale = new DateTime(2025, 1, 22, 14, 33, 12).ToString()
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.GetValue(resource, AttributeSchema);

        // Assert
        var expectedValue = new DateTime(2025, 1, 22, 14, 33, 12); 
        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public void GetValue_SQLDataType_Time_ReturnsParsedJson()
    {
        // Arrange
        var AttributeSchema = new AttributeSchema { DestinationField = "urn:kn:ki:schema:locale", SourceValue = "Locale", DestinationType = AttributeDataTypes.Time };


        var resource = new Core2EnterpriseUser
        {
            Locale = new Time(3, 1, 22).ToString()
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.GetValue(resource, AttributeSchema)?.ToString("hh:MM:ss");

        // Assert
        var expectedValue = new Time(3, 1, 22).ToString();
        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public void GetValue_SQLDataType_Date_ReturnsParsedJson()
    {
        // Arrange
        var AttributeSchema = new AttributeSchema { DestinationField = "urn:kn:ki:schema:locale", SourceValue = "Locale", DestinationType = AttributeDataTypes.Date };


        var resource = new Core2EnterpriseUser
        {
            Locale = new Date(2025, 1, 22).ToString()
        };

        // Act
        var result = JSONParserUtilV2<Core2EnterpriseUser>.GetValue(resource, AttributeSchema)?.ToString("yyyy-MM-dd");

        // Assert
        var expectedValue = new Date(2025, 1, 22).ToString();
        Assert.Equal(expectedValue, result);
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

