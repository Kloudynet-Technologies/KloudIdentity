namespace KN.KloudIdentity.Mapper.Domain.Mapping;

public record ChildSchema : SchemaBase
{
    /// <summary>
    /// Specifies if the schema is for users or groups.
    /// </summary>
    public ChildSchemaTypes SchemaTypes { get; private set; }

    /// <summary>
    /// The ID of the parent user schema.
    /// </summary>
    public int? UserSchemaId { get; set; }

    /// <summary>
    /// The ID of the parent group schema.
    /// </summary>
    public int? GroupSchemaId { get; set; }
}
