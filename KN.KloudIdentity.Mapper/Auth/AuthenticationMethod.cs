//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

namespace KN.KloudIdentity.Mapper.Auth;

/// <summary>
/// Authentication methods for 3rd party API calls.
/// </summary>
public enum AuthenticationMethod
{
    /// <summary>
    /// No authentication.
    /// </summary>
    None,

    /// <summary>
    /// Basic authentication.
    /// </summary>
    Basic,

    /// <summary>
    /// Bearer token authentication.
    /// </summary>
    Bearer,

    /// <summary>
    /// OAuth2 authentication.
    /// </summary>
    OAuth2,

    /// <summary>
    /// API key authentication.
    /// </summary>
    ApiKey
}
