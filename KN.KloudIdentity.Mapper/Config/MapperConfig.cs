namespace KN.KloudIdentity.Mapper.Config;

/// <summary>
/// This class contains the configuration for the mapper.
/// </summary>
public class MapperConfig
{
    /// <summary>
    /// Application ID from the Azure AD side.
    /// </summary>
    public required string AppId { get; set; }

    /// <summary>
    /// Auth configuration for the specific application API.
    /// </summary>
    public required AuthConfig AuthConfig { get; set; }

    /// <summary>
    /// URL for the user provisioning API.
    /// </summary>
    public required string UserProvisioningApiUrl { get; set; }

    /// <summary>
    /// URL for the group provisioning API.
    /// </summary>
    public required string GroupProvisioningApiUrl { get; set; }

    /// <summary>
    /// User schema for the user provisioning API.
    /// </summary>
    public required IList<SchemaAttribute> UserSchema { get; set; }

    /// <summary>
    /// Group schema for the group provisioning API.
    /// </summary>
    public required IList<SchemaAttribute> GroupSchema { get; set; }
}
