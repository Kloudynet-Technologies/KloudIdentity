//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Auth;

namespace KN.KloudIdentity.Mapper.Config;

/// <summary>
/// This class contains the authentication configuration for the application API.
/// </summary>
public class AuthConfig
{
    /// <summary>
    /// Authentication method for the API.
    /// </summary>
    public required AuthenticationMethod AuthenticationMethod { get; set; }

    /// <summary>
    /// Username for basic authentication.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Password for basic authentication.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Token for bearer authentication.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Client ID for OAuth2 authentication.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Client secret for OAuth2 authentication.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Scope for OAuth2 authentication.
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// Redirect URI for OAuth2 authentication.
    /// </summary>
    public string? RedirectUri { get; set; }

    /// <summary>
    /// Authority for OAuth2 authentication.
    /// </summary>
    public string? Authority { get; set; }

    /// <summary>
    /// OAuth2 token URL for OAuth2 authentication.
    /// </summary>
    public string? OAuth2TokenUrl { get; internal set; }

    /// <summary>
    /// Grant type for OAuth2 authentication.
    /// </summary>
    public string? GrantType { get; internal set; }
}