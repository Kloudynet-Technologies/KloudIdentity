//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Auth;
using KN.KloudIdentity.Mapper.Common.Encryption;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace KN.KloudIdentity.Mapper;

/// <summary>
/// Represents a basic authentication strategy.
/// </summary>
public class BasicAuthStrategy(
    IConfiguration configuration
    ) : IAuthStrategy
{
    public AuthenticationMethods AuthenticationMethod => AuthenticationMethods.Basic;

    /// <summary>
    /// Gets the authentication token using the provided authentication configuration.
    /// </summary>
    /// <param name="authConfig">The authentication configuration containing username and password.</param>
    /// <returns>The authentication token as a Base64-encoded string.</returns>
    public async Task<string> GetTokenAsync(dynamic authConfig)
    {
        ValidateParameters(authConfig, out BasicAuthentication authentication);

        var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(
            $"{authentication.Username}:{authentication.Password}"
        );
        string base64EncodedValue = Convert.ToBase64String(plainTextBytes);

        string token = await Task.FromResult(base64EncodedValue);

        return token;
    }

    /// <summary>
    /// Validates the parameters for Basic Auth.
    /// </summary>
    /// <param name="authConfig"></param>
    /// <exception cref="ArgumentNullException"></exception>
    private void ValidateParameters(dynamic authConfig, out BasicAuthentication authentication)
    {
        authentication = JsonConvert.DeserializeObject<BasicAuthentication>(authConfig.ToString());
        if (authConfig is null || authentication is null)
        {
            throw new ArgumentNullException(nameof(authConfig));
        }
        
        if (string.IsNullOrWhiteSpace(authentication?.Username))
        {
            throw new ArgumentNullException(nameof(authConfig.Username));
        }
        var encryptedData = authentication?.EncryptedData as EncryptedData;
        var encryptionKey = configuration["EncryptionKey"];
        
        if(encryptedData == null || string.IsNullOrWhiteSpace(encryptionKey))
            throw new ArgumentException("EncryptedData or EncryptionKey is missing in configuration.");   
        
        var password = EncryptionHelper.Decrypt(encryptedData!.EncryptedValue, encryptionKey!, encryptedData.IV);
        
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Decrypted password is null or empty.");
        }
        
        authentication!.Password = password;
    }
}
