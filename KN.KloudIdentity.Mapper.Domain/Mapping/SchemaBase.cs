namespace KN.KloudIdentity.Mapper.Domain.Mapping;

public record SchemaBase
{
    /// <summary>
    /// Record ID of the schema.
    /// </summary>
    public int Id { get; private set; }

    /// <summary>
    /// Application ID of the schema.
    /// </summary>
    public string AppId { get; private set; } = null!;

    /// <summary>
    /// Destination field name.
    /// </summary>
    public string DestinationField { get; private set; } = null!;

    /// <summary>
    /// Destination field type.
    /// </summary>
    public JsonDataTypes DestinationType { get; private set; }

    /// <summary>
    /// Indicates if the destination field is required.
    /// </summary>
    public bool IsRequired { get; private set; }

    /// <summary>
    /// Default value of the destination field.
    /// </summary>
    public string? DefaultValue { get; private set; }

    /// <summary>
    /// Mapping condition.
    /// </summary>
    public MappingCondition? MappingCondition { get; private set; }

    /// <summary>
    /// Mapping type.
    /// </summary>
    public MappingTypes MappingType { get; private set; }

    /// <summary>
    /// Source value to be mapped to the destination field.
    /// </summary>
    public string SourceValue { get; private set; } = null!;

    /// <summary>
    /// If the destination type is an array, this property will contain the type of the array elements.
    /// </summary>
    public JsonDataTypes ArrayDataType { get; private set; }
}
