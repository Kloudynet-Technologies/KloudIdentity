namespace KN.KloudIdentity.Mapper.Domain.Mapping;

public record AttributeSchema : SchemaBase
{
    /// <summary>
    /// If the destination field is an object, this property contains the child schema.
    /// </summary>
    public virtual ICollection<AttributeSchema>? ChildSchemas { get; init; }

    /// <summary>
    /// Destination field type length.
    /// </summary>
    public int? DestinationTypeLength { get; init; }
}
