//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Auth;
using KN.KloudIdentity.Mapper.Config;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using Newtonsoft.Json;

namespace KN.KloudIdentity.Mapper;

/// <summary>
/// Represents a basic authentication strategy.
/// </summary>
public class BasicAuthStrategy : IAuthStrategy
{
    public AuthenticationMethods AuthenticationMethod => AuthenticationMethods.Basic;

    /// <summary>
    /// Gets the authentication token using the provided authentication configuration.
    /// </summary>
    /// <param name="authConfig">The authentication configuration containing username and password.</param>
    /// <returns>The authentication token as a Base64-encoded string.</returns>
    public async Task<string> GetTokenAsync(dynamic authConfig)
    {
        ValidateParameters(authConfig);

        var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(
            $"{authConfig.Username}:{authConfig.Password}"
        );
        string base64EncodedValue = Convert.ToBase64String(plainTextBytes);

        string token = await Task.FromResult(base64EncodedValue);

        return "Basic " + token;
    }

    /// <summary>
    /// Validates the parameters for Basic Auth.
    /// </summary>
    /// <param name="authConfig"></param>
    /// <exception cref="ArgumentNullException"></exception>
    private void ValidateParameters(dynamic authConfig)
    {
        var basicAuth = JsonConvert.DeserializeObject<BasicAuthentication>(authConfig.ToString());
        if (authConfig is null || basicAuth is null)
        {
            throw new ArgumentNullException(nameof(authConfig));
        }

        if (string.IsNullOrWhiteSpace(basicAuth?.Password))
        {
            throw new ArgumentNullException(nameof(authConfig.Password));
        }

        if (string.IsNullOrWhiteSpace(basicAuth?.Username))
        {
            throw new ArgumentNullException(nameof(authConfig.Username));
        }
    }
}
