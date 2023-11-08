//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Auth;
using KN.KloudIdentity.Mapper.Config;

namespace KN.KloudIdentity.Mapper;

/// <summary>
/// Represents an authentication strategy using an API key.
/// </summary>
public class ApiKeyStrategy : IAuthStrategy
{
    public AuthenticationMethod AuthenticationMethod => AuthenticationMethod.ApiKey;

    /// <summary>
    /// Gets an authentication API Key using the provided authentication configuration.
    /// </summary>
    /// <param name="authConfig">The authentication configuration containing the API key.</param>
    /// <returns>The authentication token (API key) as a string.</returns>
    /// <exception cref="NotImplementedException">Thrown when the API key is not implemented.</exception>
    public async Task<string> GetTokenAsync(AuthConfig authConfig)
    {
        ValidateParameters(authConfig);

        if (!string.IsNullOrWhiteSpace(authConfig.ApiKey))
            return await Task.FromResult(authConfig.ApiKey);

        // TODO: Implement API key retrieval from a secret store.
        throw new NotImplementedException();
    }

    /// <summary>
    /// Validates the parameters for API Key.
    /// </summary>
    /// <param name="authConfig">The authentication configuration.</param>
    /// <exception cref="ArgumentNullException">Thrown when the authentication configuration is null.</exception>
    private void ValidateParameters(AuthConfig authConfig)
    {
        if (authConfig == null)
        {
            throw new ArgumentNullException(nameof(authConfig));
        }
    }
}