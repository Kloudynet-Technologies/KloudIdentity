//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using System.Security.Authentication;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using Newtonsoft.Json;

namespace KN.KloudIdentity.Mapper;

/// <summary>
/// Authentication methods for 3rd party API calls.
/// </summary>
public class AuthContextV1 : IAuthContext
{
    private IAuthStrategy? _authStrategy;
    private readonly IEnumerable<IAuthStrategy> _authStrategies;

    /// <summary>
    /// Initializes a new instance of the AuthContextV1 class with a collection of authentication strategies.
    /// </summary>
    /// <param name="authStrategies">A collection of authentication strategies.</param>
    public AuthContextV1(IEnumerable<IAuthStrategy> authStrategies)
    {
        _authStrategies = authStrategies;
    }

    /// <summary>
    /// Gets the authentication token using the provided authentication configuration.
    /// </summary>
    /// <param name="appConfig">The authentication configuration model</param>
    /// <returns>The authentication token as a string.</returns>
    /// <exception cref="AuthenticationException">Thrown when authentication fails.</exception>
    public async Task<string> GetTokenAsync(dynamic appConfig, SCIMDirections direction)
    {
        var method = direction == SCIMDirections.Inbound ? appConfig.AuthenticationMethodInbound : appConfig.AuthenticationMethodOutbound;

        _authStrategy = _authStrategies.FirstOrDefault(x => x.AuthenticationMethod == method);

        if (_authStrategy == null)
        {
            throw new AuthenticationException($"Authentication method {appConfig.AuthenticationMethod} is not supported.");
        }

        var authDetails = JsonConvert.DeserializeObject<dynamic>(appConfig.AuthenticationDetails.ToString());

        var authConfig = direction == SCIMDirections.Inbound ? authDetails : authDetails.Outbound;

        return await _authStrategy.GetTokenAsync(authConfig);
    }
}
