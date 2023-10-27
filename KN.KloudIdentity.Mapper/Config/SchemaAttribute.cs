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
}
