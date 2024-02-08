namespace KN.KloudIdentity.Mapper.Domain.Mapping;

public record SchemaBase
{
    /// <summary>
    /// Record ID of the schema.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Application ID of the schema.
    /// </summary>
    public string AppId { get; init; } = null!;

    /// <summary>
    /// Destination field name.
    /// </summary>
    public string DestinationField { get; init; } = null!;

    /// <summary>
    /// Destination field type.
    /// </summary>
    public JsonDataTypes DestinationType { get; init; }

    /// <summary>
    /// Indicates if the destination field is required.
    /// </summary>
    public bool IsRequired { get; init; }

    /// <summary>
    /// Default value of the destination field.
    /// </summary>
    public string? DefaultValue { get; init; }

    /// <summary>
    /// Mapping condition.
    /// </summary>
    public MappingCondition? MappingCondition { get; init; }

    /// <summary>
    /// Mapping type.
    /// </summary>
    public MappingTypes MappingType { get; init; }

    /// <summary>
    /// Source value to be mapped to the destination field.
    /// </summary>
    public string SourceValue { get; init; } = null!;

    /// <summary>
    /// If the destination type is an array, this property will contain the type of the array elements.
    /// </summary>
    public JsonDataTypes ArrayDataType { get; init; }
}
