//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Auth;
using KN.KloudIdentity.Mapper.Config;

namespace KN.KloudIdentity.Mapper;

/// <summary>
/// Represents a basic authentication strategy.
/// </summary>
public class BasicAuthStrategy : IAuthStrategy
{
    public AuthenticationMethod AuthenticationMethod => AuthenticationMethod.Basic;

    /// <summary>
    /// Gets the authentication token using the provided authentication configuration.
    /// </summary>
    /// <param name="authConfig">The authentication configuration containing username and password.</param>
    /// <returns>The authentication token as a Base64-encoded string.</returns>
    public async Task<string> GetTokenAsync(AuthConfig authConfig)
    {
        ValidateParameters(authConfig);

        var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(
            $"{authConfig.Username}:{authConfig.Password}"
        );
        string base64EncodedValue = Convert.ToBase64String(plainTextBytes);

        return await Task.FromResult(base64EncodedValue);
    }

    /// <summary>
    /// Validates the parameters for Basic Auth.
    /// </summary>
    /// <param name="authConfig"></param>
    /// <exception cref="ArgumentNullException"></exception>
    private void ValidateParameters(AuthConfig authConfig)
    {
        if (authConfig == null)
        {
            throw new ArgumentNullException(nameof(authConfig));
        }

        if (string.IsNullOrWhiteSpace(authConfig.Password))
        {
            throw new ArgumentNullException(nameof(authConfig.Password));
        }

        if (string.IsNullOrWhiteSpace(authConfig.Username))
        {
            throw new ArgumentNullException(nameof(authConfig.Username));
        }
    }
}
