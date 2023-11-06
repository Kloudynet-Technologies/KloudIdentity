using System.ComponentModel.DataAnnotations;
using KN.KloudIdentity.Mapper.Config;

namespace KN.KloudIdentity.Mapper;

public class AppConfigModel
{
    [Key]
    public required string AppId { get; set; }

    public required string UserProvisioningApiUrl { get; set; }

    public required string GroupProvisioningApiUrl { get; set; }

    public AuthConfigModel AuthConfig { get; set; }

    public ICollection<UserSchemaModel> UserSchema { get; set; }

    public ICollection<GroupSchemaModel> GroupSchema { get; set; }
}
