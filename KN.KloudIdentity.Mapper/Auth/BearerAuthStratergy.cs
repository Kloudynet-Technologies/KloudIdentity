using KN.KloudIdentity.Mapper.Common.Encryption;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace KN.KloudIdentity.Mapper;

public class BearerAuthStratergy(
    IOptions<AppSettings> appSettings,
    ISecretManager secretManager
    ) : IAuthStrategy
{
    public AuthenticationMethods AuthenticationMethod => AuthenticationMethods.Bearer;

    public async Task<string> GetTokenAsync(dynamic authConfig)
    {
        var bearerAuth = JsonConvert.DeserializeObject<BearerAuthentication>(authConfig.ToString());
        ValidateParameters(bearerAuth);
        
        var encryptedToken = await secretManager.GetSecretAsync(bearerAuth.KeyVaultReference!);
        var decryptedToken = DecryptPassword(encryptedToken, bearerAuth.EncryptedData!);
        
        return decryptedToken;
    }
    
    private void ValidateParameters(BearerAuthentication authentication)
    {
        if (string.IsNullOrWhiteSpace(authentication?.KeyVaultReference))
        {
            throw new ArgumentNullException(nameof(authentication.KeyVaultReference));
        }
        if (string.IsNullOrWhiteSpace(authentication?.EncryptedData?.IV))
        {
            throw new ArgumentNullException(nameof(authentication.EncryptedData.IV));
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
