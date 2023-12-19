using KN.KloudIdentity.Mapper.Common.Encryption;
using Xunit;

namespace KN.KloudIdentity.MapperTests;

public class EncryptionHelperTests
{
    [Fact]
    public void Encrypt_Decrypt_ReturnsOriginalText()
    {
        // Arrange
        string originalText = "1000.9fb48b87e1687814e0a745188491da6b.aa83c5af2255fe2eed4df458ba9f07ec";
        string key = "0123456789ABCDEF";
        string iv = "FEDCBA9876543210";

        // Act
        string encryptedText = EncryptionHelper.Encrypt(originalText, key, iv);
        string decryptedText = EncryptionHelper.Decrypt(encryptedText, key, iv);

        // Assert
        Assert.Equal(originalText, decryptedText);
    }
}