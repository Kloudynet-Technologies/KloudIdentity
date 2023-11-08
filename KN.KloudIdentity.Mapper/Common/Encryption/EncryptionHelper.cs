//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using System.Security.Cryptography;
using System.Text;

namespace KN.KloudIdentity.Mapper.Common.Encryption
{
    /// <summary>
    /// Encryption Helper to encrypt and decrypt data
    /// </summary>
    public class EncryptionHelper
    {
        /// <summary>
        /// Encrypts the text using AES encryption.
        /// </summary>
        /// <param name="text">The text to be encrypted.</param>
        /// <param name="key">The encryption key.</param>
        /// <param name="iv">The initialization vector.</param>
        /// <returns>The encrypted text as a Base64-encoded string.</returns>
        public static string Encrypt(string text, string key, string iv)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = Encoding.ASCII.GetBytes(key);
                aes.IV = Encoding.ASCII.GetBytes(iv);

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                byte[] encryptedBytes;
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (
                        CryptoStream cryptoStream = new CryptoStream(
                            memoryStream,
                            encryptor,
                            CryptoStreamMode.Write
                        )
                    )
                    {
                        byte[] plainBytes = Encoding.UTF8.GetBytes(text);
                        cryptoStream.Write(plainBytes, 0, plainBytes.Length);
                    }

                    encryptedBytes = memoryStream.ToArray();
                }

                return Convert.ToBase64String(encryptedBytes);
            }
        }

        /// <summary>
        /// Decrypts the text using AES encryption.
        /// </summary>
        /// <param name="cipherText">The ciphertext to be decrypted (Base64-encoded).</param>
        /// <param name="key">The decryption key.</param>
        /// <param name="iv">The initialization vector.</param>
        /// <returns>The decrypted text.</returns>
        public static string Decrypt(string cipherText, string key, string iv)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = Encoding.ASCII.GetBytes(key);
                aes.IV = Encoding.ASCII.GetBytes(iv);

                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                byte[] cipherBytes = Convert.FromBase64String(cipherText);

                string decryptedText;
                using (MemoryStream memoryStream = new MemoryStream(cipherBytes))
                {
                    using (
                        CryptoStream cryptoStream = new CryptoStream(
                            memoryStream,
                            decryptor,
                            CryptoStreamMode.Read
                        )
                    )
                    {
                        byte[] plainBytes = new byte[cipherBytes.Length];
                        int bytesRead = cryptoStream.Read(plainBytes, 0, plainBytes.Length);
                        decryptedText = Encoding.UTF8.GetString(plainBytes, 0, bytesRead);
                    }
                }

                return decryptedText;
            }
        }
    }
}
