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
