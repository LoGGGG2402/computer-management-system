using System;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace CMSAgent.Security
{
    /// <summary>
    /// Class để mã hóa và giải mã token xác thực của agent.
    /// </summary>
    public class TokenProtector
    {
        private readonly IDataProtector _protector;

        public TokenProtector(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector("CMSAgent.TokenProtection");
        }

        /// <summary>
        /// Mã hóa token plaintext bằng DPAPI.
        /// </summary>
        /// <param name="plainToken">Token gốc dạng plaintext.</param>
        /// <returns>Chuỗi token đã mã hóa ở dạng Base64.</returns>
        /// <exception cref="System.Security.Cryptography.CryptographicException">Ném ra khi gặp lỗi mã hóa.</exception>
        public string EncryptToken(string plainToken)
        {
            if (string.IsNullOrEmpty(plainToken))
                throw new ArgumentNullException(nameof(plainToken));

            var protectedBytes = _protector.Protect(System.Text.Encoding.UTF8.GetBytes(plainToken));
            return Convert.ToBase64String(protectedBytes);
        }

        /// <summary>
        /// Giải mã token đã mã hóa bằng DPAPI.
        /// </summary>
        /// <param name="encryptedTokenBase64">Token đã mã hóa ở dạng Base64.</param>
        /// <returns>Token gốc dạng plaintext.</returns>
        /// <exception cref="System.Security.Cryptography.CryptographicException">Ném ra khi gặp lỗi giải mã.</exception>
        public string DecryptToken(string encryptedTokenBase64)
        {
            if (string.IsNullOrEmpty(encryptedTokenBase64))
                throw new ArgumentNullException(nameof(encryptedTokenBase64));

            try
            {
                var encryptedBytes = Convert.FromBase64String(encryptedTokenBase64);
                var decryptedBytes = _protector.Unprotect(encryptedBytes);
                return System.Text.Encoding.UTF8.GetString(decryptedBytes);
            }
            catch (FormatException)
            {
                throw new CryptographicException("Invalid Base64 string");
            }
        }
    }
} 