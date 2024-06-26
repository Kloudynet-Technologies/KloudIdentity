﻿//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using System.Dynamic;
using KN.KloudIdentity.Mapper.Auth;
using KN.KloudIdentity.Mapper.Config;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using Newtonsoft.Json;

namespace KN.KloudIdentity.Mapper;

/// <summary>
/// Represents an authentication strategy using an API key.
/// </summary>
public class ApiKeyStrategy : IAuthStrategy
{
    public AuthenticationMethods AuthenticationMethod => AuthenticationMethods.APIKey;

    /// <summary>
    /// Gets an authentication API Key using the provided authentication configuration.
    /// </summary>
    /// <param name="authConfig">The authentication configuration containing the API key.</param>
    /// <returns>The authentication token (API key) as a string.</returns>
    /// <exception cref="NotImplementedException">Thrown when the API key is not implemented.</exception>
    public async Task<string> GetTokenAsync(dynamic authConfig)
    {
        APIKeyAuthentication apiKeyAuth;

        ValidateParameters(authConfig, out apiKeyAuth);

        if (!string.IsNullOrWhiteSpace(apiKeyAuth?.APIKey))
            return await Task.FromResult(apiKeyAuth.APIKey);

        // TODO: Implement API key retrieval from a server.
        throw new NotImplementedException();
    }

    /// <summary>
    /// Validates the parameters for API Key.
    /// </summary>
    /// <param name="authConfig">The authentication configuration.</param>
    /// <exception cref="ArgumentNullException">Thrown when the authentication configuration is null.</exception>
    private void ValidateParameters(dynamic authConfig, out APIKeyAuthentication authentication)
    {
        authentication = JsonConvert.DeserializeObject<APIKeyAuthentication>(authConfig.ToString());

        if (authConfig is null || authentication is null)
        {
            throw new ArgumentNullException(nameof(authConfig));
        }
    }
}