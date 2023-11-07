//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KN.KloudIdentity.Mapper;

public class SchemaBaseModel
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ID { get; set; }

    /// <summary>
    /// Name of the field in the schema.
    /// </summary>
    public required string FieldName { get; set; }

    /// <summary>
    /// JSON data type of the field.
    /// </summary>
    public int DataType { get; set; }

    /// <summary>
    /// Whether the field is required or not.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Name of the mapped SCIM attribute.
    /// </summary>
    public required string MappedAttribute { get; set; }

    public required string AppId { get; set; }
    /// <summary>
    /// The application ID this schema belongs to.
    /// </summary>
    [ForeignKey("AppId")]
    public AppConfigModel AppConfigModel { get; set; }
}
