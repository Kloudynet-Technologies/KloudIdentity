using KN.KloudIdentity.Mapper.Domain.Mapping;

namespace KN.KloudIdentity.Mapper.Domain.Application;

public class ActionStep
{
    /// <summary>
    /// Primary Key for the entity.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// ActionId to which this Action step belongs to.
    /// Foreign key to the Action entity.
    /// </summary>   
    public int ActionId { get; init; }

    /// <summary>
    /// Step Order
    /// </summary>
    public int StepOrder { get; init; }

    /// <summary>
    /// HttpVerb for step
    /// </summary>
    public HttpVerbs HttpVerb { get; init; }

    /// <summary>
    /// Action's End Point
    /// </summary>
    public string EndPoint { get; init; } = null!;

    /// <summary>
    /// Action step Mandatory or not
    /// </summary>
    public bool? IsMandatory { get; init; } = true;

    /// <summary>
    /// List of User Attribute Schemas
    /// </summary>
    public virtual ICollection<AttributeSchema>? UserAttributeSchemas { get; init; }

    /// <summary>
    /// List of Group Attribute Schemas
    /// </summary>
    public virtual ICollection<AttributeSchema>? GroupAttributeSchema { get; init; }
}
