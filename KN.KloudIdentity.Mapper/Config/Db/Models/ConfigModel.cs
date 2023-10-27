using System.ComponentModel.DataAnnotations;
using KN.KloudIdentity.Mapper.Config;

namespace KN.KloudIdentity.Mapper;

public class ConfigModel
{
    [Key]
    public required string AppId { get; set; }

    public required string UserProvisioningApiUrl { get; set; }

    public required string GroupProvisioningApiUrl { get; set; }

    public required AuthConfigModel AuthConfig { get; set; }

    public required ICollection<UserSchemaModel> UserSchema { get; set; }

    public required ICollection<GroupSchemaModel> GroupSchema { get; set; }

}
