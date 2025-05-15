using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using System.Runtime.Versioning;

namespace CMSAgent.Security
{
    /// <summary>
    /// Class for protecting tokens through encryption and decryption.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class TokenProtector(ILogger<TokenProtector> logger)
    {
        private readonly ILogger<TokenProtector> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly byte[] _entropy = Encoding.UTF8.GetBytes("CMSAgent_Entropy_Key_2023");

        /// <summary>
        /// Encrypts a token.
        /// </summary>
        /// <param name="token">Token to encrypt.</param>
        /// <returns>Encrypted token as a Base64 string.</returns>
        public string EncryptToken(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                throw new ArgumentException("Token cannot be empty", nameof(token));
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
                _logger.LogError(ex, "Error when encrypting token");
                throw;
            }
        }

        /// <summary>
        /// Decrypts a token.
        /// </summary>
        /// <param name="encryptedToken">Encrypted token as a Base64 string.</param>
        /// <returns>Original token.</returns>
        public string DecryptToken(string encryptedToken)
        {
            if (string.IsNullOrEmpty(encryptedToken))
            {
                throw new ArgumentException("Encrypted token cannot be empty", nameof(encryptedToken));
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
                _logger.LogError(ex, "Error when decrypting token");
                throw;
            }
        }
    }
}