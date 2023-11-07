//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KN.KloudIdentity.Mapper;

public class AuthConfigModel
{
    [Key]
    public required string AppId { get; set; }

    public int AuthenticationMethod { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string? Token { get; set; }

    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    public string? Scope { get; set; }

    public string? RedirectUri { get; set; }

    public string? Authority { get; set; }

    [ForeignKey("AppId")]
    public AppConfigModel ConfigModel { get; set; }
}
