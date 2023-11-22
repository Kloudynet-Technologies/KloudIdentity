//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

namespace KN.KloudIdentity.Mapper.Config;

public class SchemaAttribute
{
    /// <summary>
    /// Name of the field in the schema.
    /// </summary>
    public required string FieldName { get; set; }

    /// <summary>
    /// JSON data type of the field.
    /// </summary>
    public JSonDataType DataType { get; set; }

    /// <summary>
    /// Whether the field is required or not.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Name of the mapped SCIM attribute.
    /// </summary>
    public required string MappedAttribute { get; set; }

    /// <summary>
    /// Array element type of the field.
    /// </summary>
    public JSonDataType? ArrayElementType { get; set; }

    /// <summary>
    /// For plain JSON array, the field name of the array element.
    /// </summary>  
    public string? ArrayElementMappingField { get; set; }

    /// <summary>
    /// Child schemas of the schema.
    /// </summary>
    public IList<SchemaAttribute>? ChildSchemas { get; set; }
}
