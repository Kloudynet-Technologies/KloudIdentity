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

    public string? PUTAPIForUsers { get; set; }

    public string? PATCHAPIForUsers { get; set; }

    public string? DELETEAPIForUsers { get; set; }

    public string? GETAPIForUsers { get; set; }

    public string? LISTAPIForUsers { get; set; }

    public required string GroupProvisioningApiUrl { get; set; }

    public string? PUTAPIForGroups { get; set; }

    public string? PATCHAPIForGroups { get; set; }

    public string? DELETEAPIForGroups { get; set; }

    public string? GETAPIForGroups { get; set; }

    public string? LISTAPIForGroups { get; set; }

    public string? PATCHAPIForAddMemberToGroup { get; set; }

    public string? PATCHAPIForRemoveMemberFromGroup { get; set; }

    public string? PATCHAPIForRemoveAllMembersFromGroup { get; set; }

    public AuthConfigModel AuthConfig { get; set; }

    public ICollection<UserSchemaModel> UserSchema { get; set; }

    public ICollection<GroupSchemaModel> GroupSchema { get; set; }
}
