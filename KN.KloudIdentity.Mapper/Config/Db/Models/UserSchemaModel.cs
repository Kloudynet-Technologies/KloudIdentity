//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using System.ComponentModel.DataAnnotations.Schema;

namespace KN.KloudIdentity.Mapper;

public class UserSchemaModel : SchemaBaseModel
{
    /// <summary>
    /// Foreign key for the parent schema
    /// </summary>
    public int? ParentId { get; set; }

    /// <summary>
    /// Navigation property for the parent schema
    /// </summary>
    [ForeignKey("ParentId")]
    public UserSchemaModel? ParentSchema { get; set; }

    /// <summary>
    /// Navigation property for the child schemas
    /// </summary>
    public ICollection<UserSchemaModel>? ChildSchemas { get; set; }
}
