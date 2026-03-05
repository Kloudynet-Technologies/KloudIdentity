using KN.KloudIdentity.Mapper.Common.Encryption;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace KN.KloudIdentity.Mapper;

public class BearerAuthStratergy(IConfiguration configuration) : IAuthStrategy
{
    public AuthenticationMethods AuthenticationMethod => AuthenticationMethods.Bearer;

    public Task<string> GetTokenAsync(dynamic authConfig)
    {
        var bearerAuth = JsonConvert.DeserializeObject<BearerAuthentication>(authConfig.ToString());

        if (authConfig == null || bearerAuth == null)
        {
            throw new ArgumentNullException(nameof(authConfig));
        }

        if (string.IsNullOrWhiteSpace(bearerAuth?.Token))
        {
            throw new ArgumentNullException(nameof(authConfig.Token));
        }
        
        var encryptedData = bearerAuth?.EncryptedData as EncryptedData;
        var encryptionKey = configuration["EncryptionKey"];
        if(encryptedData == null || string.IsNullOrWhiteSpace(encryptionKey))
            throw new ArgumentException("EncryptedData or EncryptionKey is missing in configuration.");
        
        var decryptedToken = EncryptionHelper.Decrypt(encryptedData.EncryptedValue, encryptionKey, encryptedData.IV);

        return Task.FromResult(decryptedToken);
    }
}
