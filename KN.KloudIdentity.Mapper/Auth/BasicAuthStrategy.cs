//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Auth;
using KN.KloudIdentity.Mapper.Common.Encryption;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace KN.KloudIdentity.Mapper;

/// <summary>
/// Represents a basic authentication strategy.
/// </summary>
public class BasicAuthStrategy(
    IOptions<AppSettings> appSettings,
    ISecretManager secretManager
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

        var encryptedPassword = await secretManager.GetSecretAsync(authentication.KeyVaultReference!);
        var password = DecryptPassword(encryptedPassword, authentication.EncryptedData!);

        var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(
            $"{authentication.Username}:{password}"
        );
        string base64EncodedValue = Convert.ToBase64String(plainTextBytes);

        string token = await Task.FromResult(base64EncodedValue);

        return token;
    }

    /// <summary>
    /// Validates the parameters for Basic Auth.
    /// </summary>
    /// <param name="authConfig"></param>
    /// <param name="authentication"></param>
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
        
        if(string.IsNullOrWhiteSpace(authentication?.EncryptedData?.IV))
        {
            throw new ArgumentNullException(nameof(authConfig.EncryptedData.IV));
        }

        if (string.IsNullOrWhiteSpace(authentication?.KeyVaultReference))
        {
            throw new ArgumentNullException(nameof(authConfig.KeyVaultReference));
        }
    }
    
    private string DecryptPassword(string encryptedPassword, EncryptedData encryptedData)
    {
        var encryptionKey = appSettings.Value.EncryptionKey;
        if (string.IsNullOrWhiteSpace(encryptionKey))
        {
            throw new ArgumentException("Encryption key is not configured in EncryptionKey.");
        }

        var decryptedPassword = EncryptionHelper.Decrypt(encryptedPassword, encryptionKey, encryptedData.IV);
        return decryptedPassword;
    }
}