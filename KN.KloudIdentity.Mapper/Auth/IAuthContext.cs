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

    Task<Dictionary<AuthenticationMethods, string>> GetTokenListAsync(dynamic appConfig, SCIMDirections direction);
}
