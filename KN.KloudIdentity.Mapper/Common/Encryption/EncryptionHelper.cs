using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace KN.KloudIdentity.Mapper.Common.Encryption
{
    public class EncryptionHelper
    {

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
                    using (CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                    {
                        byte[] plainBytes = Encoding.UTF8.GetBytes(text);
                        cryptoStream.Write(plainBytes, 0, plainBytes.Length);
                    }

                    encryptedBytes = memoryStream.ToArray();
                }

                return Convert.ToBase64String(encryptedBytes);
            }
        }

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
                    using (CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
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
