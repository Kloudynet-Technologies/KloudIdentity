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
                aes.Key = Encoding.UTF8.GetBytes(key);
                aes.IV = Encoding.UTF8.GetBytes(iv);

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                    {
                        byte[] plainBytes = Encoding.UTF8.GetBytes(text);
                        cryptoStream.Write(plainBytes, 0, plainBytes.Length);
                    }

                    return Convert.ToBase64String(memoryStream.ToArray());
                }
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
                aes.Key = Encoding.UTF8.GetBytes(key);
                aes.IV = Encoding.UTF8.GetBytes(iv);

                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                byte[] cipherBytes = Convert.FromBase64String(cipherText);

                using (MemoryStream memoryStream = new MemoryStream(cipherBytes))
                {
                    using (CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader reader = new StreamReader(cryptoStream))
                        {
                            return reader.ReadToEnd();
                        }
                    }
                }
            }
        }
    }
}
