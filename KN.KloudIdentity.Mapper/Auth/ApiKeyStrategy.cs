//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Common.Encryption;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace KN.KloudIdentity.Mapper;

/// <summary>
/// Represents an authentication strategy using an API key.
/// </summary>
public class ApiKeyStrategy(
    IOptions<AppSettings> appSettings, 
    ISecretManager secretManager) : IAuthStrategy
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
        ValidateParameters(authConfig, out APIKeyAuthentication apiKeyAuth);
        
        var encryptedApiKey = await secretManager.GetSecretAsync(apiKeyAuth.KeyVaultReference!);
        var apiKey = DecryptPassword(encryptedApiKey, apiKeyAuth.EncryptedData!);
        
        if(string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API Key is required in APIKeyAuthentication.");
        
        return apiKey;
    }
   
    /// <summary>
    /// Validates the parameters for API Key.
    /// </summary>
    /// <param name="authConfig">The authentication configuration.</param>
    /// <param name="authentication"></param>
    /// <exception cref="ArgumentNullException">Thrown when the authentication configuration is null.</exception>
    private void ValidateParameters(dynamic authConfig, out APIKeyAuthentication authentication)
    {
        authentication = JsonConvert.DeserializeObject<APIKeyAuthentication>(authConfig.ToString());
        
        if (authentication == null)
        {
            throw new ArgumentNullException(nameof(authConfig), "Authentication configuration is null or invalid.");
        }
        
        if (string.IsNullOrWhiteSpace(authentication.AuthHeaderName))
        {
            throw new ArgumentException("AuthHeaderName is required in APIKeyAuthentication.");
        }
        
        if (string.IsNullOrWhiteSpace(authentication.KeyVaultReference))
        {
            throw new ArgumentException("KeyVaultReference is required in APIKeyAuthentication for retrieving the encrypted API key.");
        }
        
        if (authentication.EncryptedData == null)
        {
            throw new ArgumentException("EncryptedData is required in APIKeyAuthentication for decrypting the API key.");
        }
        
        if (string.IsNullOrWhiteSpace(authentication.EncryptedData.IV))
        {
            throw new ArgumentException("EncryptedData.IV is required in APIKeyAuthentication for decrypting the API key.");
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
