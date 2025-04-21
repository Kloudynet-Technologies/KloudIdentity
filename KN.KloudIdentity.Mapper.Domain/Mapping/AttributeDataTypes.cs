using System.Data.Odbc;

namespace KN.KloudIdentity.Mapper.Domain.Mapping;

/// <summary>
/// SQL Data Type - Image,Binary,VarBinary not supported for SCIM 
/// </summary>
public enum AttributeDataTypes
{
    // JSON Data Types
    String,
    Number,
    Boolean,
    Array,
    Object,
    Null,
    DateTime,
    
    // SQL Data Types
    BigInt,
    Binary,
    Bit,
    Char,  
    Decimal,
    Numeric,
    Double,
    Image,
    Int,
    NChar,
    NText,
    NVarChar,
    Real,
    UniqueIdentifier,
    SmallDateTime,
    SmallInt,
    Text,
    Timestamp,
    TinyInt,
    VarBinary,
    VarChar,
    Date,
    Time
}

public static class AttributeDataTypesExtensions
{
    public static OdbcType ToOdbcType(this AttributeDataTypes attributeDataType)
    {
        return attributeDataType switch
        {
            AttributeDataTypes.String => OdbcType.NVarChar,
            AttributeDataTypes.Number => OdbcType.BigInt,
            AttributeDataTypes.Boolean => OdbcType.Bit,
            AttributeDataTypes.Array => OdbcType.NVarChar,
            AttributeDataTypes.Object => OdbcType.NVarChar,
            AttributeDataTypes.Null => OdbcType.NVarChar,
            AttributeDataTypes.DateTime => OdbcType.DateTime,
            AttributeDataTypes.BigInt => OdbcType.BigInt,
            AttributeDataTypes.Binary => OdbcType.Binary,
            AttributeDataTypes.Bit => OdbcType.Bit,
            AttributeDataTypes.Char => OdbcType.Char,
            AttributeDataTypes.Decimal => OdbcType.Decimal,
            AttributeDataTypes.Numeric => OdbcType.Numeric,
            AttributeDataTypes.Double => OdbcType.Double,
            AttributeDataTypes.Image => OdbcType.Image,
            AttributeDataTypes.Int => OdbcType.Int,
            AttributeDataTypes.NChar => OdbcType.NChar,
            AttributeDataTypes.NText => OdbcType.NText,
            AttributeDataTypes.NVarChar => OdbcType.NVarChar,
            AttributeDataTypes.Real => OdbcType.Real,
            AttributeDataTypes.UniqueIdentifier => OdbcType.UniqueIdentifier,
            AttributeDataTypes.SmallDateTime => OdbcType.SmallDateTime,
            AttributeDataTypes.SmallInt => OdbcType.SmallInt,
            AttributeDataTypes.Text => OdbcType.Text,
            AttributeDataTypes.Timestamp => OdbcType.Timestamp,
            AttributeDataTypes.TinyInt => OdbcType.TinyInt,
            AttributeDataTypes.VarBinary => OdbcType.VarBinary,
            AttributeDataTypes.VarChar => OdbcType.VarChar,
            AttributeDataTypes.Date => OdbcType.Date,
            AttributeDataTypes.Time => OdbcType.Time,
            _ => throw new ArgumentOutOfRangeException(nameof(attributeDataType), attributeDataType, null)
        };
    }
}
