using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using System.Runtime.Versioning;

namespace CMSAgent.Security
{
    /// <summary>
    /// Lớp bảo vệ token bằng cách mã hóa và giải mã.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class TokenProtector(ILogger<TokenProtector> logger)
    {
        private readonly ILogger<TokenProtector> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly byte[] _entropy = Encoding.UTF8.GetBytes("CMSAgent_Entropy_Key_2023");

        /// <summary>
        /// Mã hóa token.
        /// </summary>
        /// <param name="token">Token cần mã hóa.</param>
        /// <returns>Token đã được mã hóa dưới dạng chuỗi Base64.</returns>
        public string EncryptToken(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                throw new ArgumentException("Token không được để trống", nameof(token));
            }

            try
            {
                byte[] tokenBytes = Encoding.UTF8.GetBytes(token);
                byte[] encryptedBytes = ProtectedData.Protect(
                    tokenBytes,
                    _entropy,
                    DataProtectionScope.LocalMachine);
                
                return Convert.ToBase64String(encryptedBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi mã hóa token");
                throw;
            }
        }

        /// <summary>
        /// Giải mã token.
        /// </summary>
        /// <param name="encryptedToken">Token đã mã hóa dưới dạng chuỗi Base64.</param>
        /// <returns>Token gốc.</returns>
        public string DecryptToken(string encryptedToken)
        {
            if (string.IsNullOrEmpty(encryptedToken))
            {
                throw new ArgumentException("Token đã mã hóa không được để trống", nameof(encryptedToken));
            }

            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(encryptedToken);
                byte[] decryptedBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    _entropy,
                    DataProtectionScope.LocalMachine);
                
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi giải mã token");
                throw;
            }
        }
    }
} 