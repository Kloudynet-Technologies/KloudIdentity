//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using System.Security.Authentication;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using Newtonsoft.Json;

namespace KN.KloudIdentity.Mapper;

/// <summary>
/// Authentication methods for 3rd party API calls.
/// </summary>
public class AuthContextV2 : IAuthContext
{
    private readonly IEnumerable<IAuthStrategy> _authStrategies;

    /// <summary>
    /// Initializes a new instance of the AuthContextV2 class with a collection of authentication strategies.
    /// </summary>
    /// <param name="authStrategies">A collection of authentication strategies.</param>
    public AuthContextV2(IEnumerable<IAuthStrategy> authStrategies)
    {
        _authStrategies = authStrategies;
    }

    [Obsolete("Use GetTokenListAsync with Authentication Flow instead. This method use only for inbound authentication. It will be removed in future versions.")]
    public async Task<string> GetTokenAsync(dynamic appConfig, SCIMDirections direction)
    {
        if(direction != SCIMDirections.Inbound)
        {
            throw new AuthenticationException("GetTokenAsync is only supported for Inbound direction. Use GetTokenListAsync with Authentication Flow for Outbound direction.");
        }

        var authStrategy = _authStrategies.FirstOrDefault(x => x.AuthenticationMethod == appConfig.AuthenticationMethodInbound);

        if (authStrategy == null)
        {
            throw new AuthenticationException($"Authentication method {appConfig.AuthenticationMethod} is not supported.");
        }

        var authDetails = JsonConvert.DeserializeObject<dynamic>(appConfig.AuthenticationDetails.ToString());

        return await authStrategy.GetTokenAsync(authDetails);
    }

    /// <summary>
    /// Gets the authentication tokens list using the provided authentication flow configuration.
    /// </summary>
    /// <param name="appConfig">The authentication configuration model</param>
    /// <param name="direction">SCIM direction : Inbound or Outbound</param>
    /// <returns>Dictionary with Authentication Method and relevant Token</returns>
    /// <exception cref="AuthenticationException">Thrown when authentication fails.</exception>
    public async Task<Dictionary<int, string>> GetTokenListAsync(dynamic appConfig, SCIMDirections direction)
    {
        var tokens = new Dictionary<int, string>();
        var flow = appConfig.AuthenticationFlow as AuthenticationFlow;

        if (flow?.Steps == null || flow.Steps.Count == 0)
            throw new AuthenticationException("No authentication flow or steps found for this application.");

        foreach (var step in flow.Steps.OrderBy(s => s.StepOrder))
        {
            var method = step.AuthenticationMethod;
            var strategy = _authStrategies.FirstOrDefault(x => x.AuthenticationMethod == method)
                           ?? throw new AuthenticationException($"Authentication method {method} is not supported.");

            var authDetails = step.AuthenticationDetails;
            var token = await strategy.GetTokenAsync(authDetails);

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new AuthenticationException($"Authentication step '{step.StepTitle}' failed to produce a token.");
            }

            tokens[step.StepOrder] = token;
        }

        return tokens;
    }
}