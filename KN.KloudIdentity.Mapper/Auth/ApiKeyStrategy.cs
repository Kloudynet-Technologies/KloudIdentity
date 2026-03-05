//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Common.Encryption;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Serilog;

namespace KN.KloudIdentity.Mapper;

/// <summary>
/// Represents an authentication strategy using an API key.
/// </summary>
public class ApiKeyStrategy(IConfiguration configuration) : IAuthStrategy
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

        if (!string.IsNullOrWhiteSpace(apiKeyAuth?.APIKey))
            return await Task.FromResult(apiKeyAuth.APIKey);

        // TODO: Implement API key retrieval from a server.
        throw new NotImplementedException();
    }

    /// <summary>
    /// Validates the parameters for API Key.
    /// </summary>
    /// <param name="authConfig">The authentication configuration.</param>
    /// <param name="authentication"></param>
    /// <exception cref="ArgumentNullException">Thrown when the authentication configuration is null.</exception>
    private void ValidateParameters(dynamic authConfig, out APIKeyAuthentication authentication)
    {
        authentication = null;

        if (authConfig is null)
        {
            Log.Error("authConfig is null.");
            throw new ArgumentNullException(nameof(authConfig));
        }

        string authJson;

        try
        {
            authJson = authConfig.ToString();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed converting authConfig to string.");
            throw;
        }

        if (string.IsNullOrWhiteSpace(authJson))
        {
            Log.Error("Authentication configuration is empty.");
            throw new ArgumentException("Authentication configuration is empty.", nameof(authConfig));
        }

        try
        {
            authentication = JsonConvert.DeserializeObject<APIKeyAuthentication>(authJson);
        }
        catch (JsonException ex)
        {
            Log.Error(ex, "JSON deserialization failed. Payload: {Payload}", authJson);
            throw new ArgumentException("Invalid authentication configuration JSON.", nameof(authConfig));
        }

        if (authentication is null)
        {
            Log.Error("Deserialized authentication object is null.");
            throw new ArgumentException("Invalid authentication configuration payload.", nameof(authConfig));
        }

        var encryptedData = authentication.EncryptedData;
        if (encryptedData is null)
        {
            Log.Error("EncryptedData is missing.");
            throw new ArgumentNullException(nameof(authentication.EncryptedData));
        }

        var encryptionKey = configuration["EncryptionKey"];
        if (string.IsNullOrWhiteSpace(encryptionKey))
        {
            Log.Error("Encryption key is not configured.");
            throw new InvalidOperationException("Encryption key is not configured.");
        }

        if (string.IsNullOrWhiteSpace(encryptedData.EncryptedValue) ||
            string.IsNullOrWhiteSpace(encryptedData.IV))
        {
            Log.Error("EncryptedValue or IV missing.");
            throw new ArgumentException("EncryptedValue or IV missing.", nameof(authConfig));
        }

        var apiKey = EncryptionHelper.Decrypt(
            encryptedData.EncryptedValue,
            encryptionKey,
            encryptedData.IV);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Log.Error("API key decrypted but empty.");
            throw new InvalidOperationException("Decrypted API key is empty.");
        }

        authentication.APIKey = apiKey;
    }
}