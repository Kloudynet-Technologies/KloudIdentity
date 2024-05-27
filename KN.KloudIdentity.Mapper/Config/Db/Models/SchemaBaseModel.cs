//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Config;
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

    /// <summary>
    /// Array element type of the field.
    /// </summary>
    public JSonDataType? ArrayElementType { get; set; }

    /// <summary>
    /// For plain JSON array, the field name of the array element.
    /// </summary>  
    public string? ArrayElementMappingField { get; set; }

    /// <summary>
    /// Application ID this schema belongs to.
    /// </summary>
    public required string AppId { get; set; }
    /// <summary>
    /// The application ID this schema belongs to.
    /// </summary>

    [ForeignKey("AppId")]
    public AppConfigModel AppConfigModel { get; set; }
}