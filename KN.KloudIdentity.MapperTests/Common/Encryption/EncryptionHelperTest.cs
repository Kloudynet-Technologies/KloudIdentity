using KN.KloudIdentity.Mapper.Common.Encryption;

namespace KN.KloudIdentity.MapperTests.Common.Encryption
{
    public class EncryptionHelperTest
    {
        [Fact]
        public void SimpleTextEncryptTest()
        {
            // Arrange
            string text = "This is a simple text.";
            string key = "2B7E151628AED2A6ABF7158809CF4F3C";
            string iv = "3AD77BB40D7A3660"; // 128-bit IV for AES

            // Act
            string result = EncryptionHelper.Encrypt(text, key, iv);

            string expected = EncryptionHelper.Decrypt(result, key, iv);

            // Assert
            Assert.Equal(text, expected);
        }

        [Fact]
        public void ComplexTextEncryptTest_1()
        {
            // Arrange
            string text = "1000.9fb48b87e1687814e0a745188491da6b.aa83c5af2255fe2eed4df458ba9f07ec";
            string key = "2B7E151628AED2A6ABF7158809CF4F3C";
            string iv = "3AD77BB40D7A3660"; // 128-bit IV for AES

            // Act
            string result = EncryptionHelper.Encrypt(text, key, iv);

            string expected = EncryptionHelper.Decrypt(result, key, iv);

            // Assert
            Assert.Equal(text, expected);
        }

        [Fact]
        public void ComplexTextEncryptTest_2()
        {
            string complexText = "This is a complex text with 123 numbers, !@# symbols, and spaces in it. It includes a mix of characters such as: 1qW$";
            string key = "2B7E151628AED2A6ABF7158809CF4F3C";
            string iv = "3AD77BB40D7A3660"; // 128-bit IV for AES

            string result = EncryptionHelper.Encrypt(complexText, key, iv);

            string expected = EncryptionHelper.Decrypt(result, key, iv);

            Assert.Equal(complexText, expected);
        }

    }
}
