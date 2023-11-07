//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

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
