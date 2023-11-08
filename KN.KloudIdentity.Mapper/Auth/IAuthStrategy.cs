//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Auth;
using KN.KloudIdentity.Mapper.Config;

namespace KN.KloudIdentity.Mapper;

public interface IAuthStrategy
{
    AuthenticationMethod AuthenticationMethod { get; }

    /// <summary>
    /// Gets the auth token.
    /// </summary>
    Task<string> GetTokenAsync(AuthConfig authConfig);
}
