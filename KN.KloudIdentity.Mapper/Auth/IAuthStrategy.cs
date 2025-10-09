//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Auth;
using KN.KloudIdentity.Mapper.Config;
using KN.KloudIdentity.Mapper.Domain.Authentication;

namespace KN.KloudIdentity.Mapper;

public interface IAuthStrategy
{
    AuthenticationMethods AuthenticationMethod { get; }

    /// <summary>
    /// Gets the auth token.
    /// </summary>
    Task<string> GetTokenAsync(dynamic authConfig);

    /// <summary>
    /// Gets the auth token with additional arguments if needed.
    /// </summary>
    /// <param name="authConfig"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    Task<string> GetTokenAsync(dynamic authConfig, dynamic[]? args = null)
    {
        // Default implementation (can be overridden by implementing classes)
        return GetTokenAsync(authConfig);
    }
}
