//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Domain.Authentication;
using KN.KloudIdentity.Mapper.Domain.Mapping;

namespace KN.KloudIdentity.Mapper;

/// <summary>
/// Authentication methods for 3rd party API calls.
/// </summary>
public interface IAuthContext
{
    /// <summary>
    /// Gets the auth token.
    /// </summary>
    Task<string> GetTokenAsync(dynamic appConfig, SCIMDirections direction);

    /// <summary>
    /// Gets the authentication tokens list using the provided authentication flow configuration.</summary>
    /// <param name="appConfig">The authentication configuration model</param>
    /// <param name="direction">SCIM direction : Inbound or Outbound</param>
    /// <returns>Dictionary with Authentication Method and relevant Token</returns>
    Task<Dictionary<int, string>> GetTokenListAsync(dynamic appConfig, SCIMDirections direction);
}
