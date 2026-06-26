using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace ApexWallet.Api.Security
{
    public class AesCryptoService : ICryptoService
    {
        private readonly byte[] _key;
        private readonly byte[] _iv;

        public AesCryptoService(IConfiguration configuration)
        {
            var keyString = configuration["CryptoSettings:Key"] ?? throw new ArgumentNullException("Crypto Key is missing.");
            var ivString = configuration["CryptoSettings:IV"] ?? throw new ArgumentNullException("Crypto IV is missing.");

            _key = Encoding.UTF8.GetBytes(keyString); // Must be exactly 32 bytes for AES-256
            _iv = Encoding.UTF8.GetBytes(ivString);   // Must be exactly 16 bytes for AES block size
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs))
            {
                sw.Write(plainText);
            }

            return Convert.ToBase64String(ms.ToArray());
        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;

            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream(Convert.FromBase64String(cipherText));
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);

            return sr.ReadToEnd();
        }
    }
}