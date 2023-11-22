//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

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
    ///  URL for the PUT API for users.
    /// </summary>
    public string? PUTAPIForUsers { get; set; }

    /// <summary>
    /// URL for the PATCH API for users.
    /// </summary>  
    public string? PATCHAPIForUsers { get; set; }

    /// <summary>
    /// URL for the DELETE API for users.
    /// </summary>
    public string? DELETEAPIForUsers { get; set; }

    /// <summary>
    /// URL for the GET API for users.
    /// </summary>
    public string? GETAPIForUsers { get; set; }

    /// <summary>
    /// URL for the LIST API for users.
    /// </summary> 
    public string? LISTAPIForUsers { get; set; }

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
