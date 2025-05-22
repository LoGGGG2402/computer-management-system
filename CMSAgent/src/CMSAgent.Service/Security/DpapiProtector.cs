// CMSAgent.Service/Security/DpapiProtector.cs
using Microsoft.Extensions.Logging;
using System;
using System.Security.Cryptography; // Required for ProtectedData
using System.Text;
using System.Runtime.InteropServices; // For OSPlatform

namespace CMSAgent.Service.Security
{
    /// <summary>
    /// Implementation of IDpapiProtector using Windows Data Protection API (DPAPI).
    /// Encrypts data with LocalMachine scope, meaning it can only be decrypted on the same machine.
    /// </summary>
    public class DpapiProtector : IDpapiProtector
    {
        private readonly ILogger<DpapiProtector> _logger;

        // Protection scope: Encrypted data can only be decrypted on the same computer.
        private const DataProtectionScope Scope = DataProtectionScope.LocalMachine;

        public DpapiProtector(ILogger<DpapiProtector> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _logger.LogWarning("DPAPI Protector is only supported on Windows. Encryption/decryption operations will fail on other platforms.");
            }
        }

        /// <summary>
        /// Encrypt a plaintext string.
        /// </summary>
        /// <param name="plainText">String to encrypt.</param>
        /// <param name="optionalEntropy">Optional byte array to enhance security (can be null).</param>
        /// <returns>Encrypted string in Base64 format, or null if there is an error.</returns>
        public string? Protect(string plainText, byte[]? optionalEntropy = null)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                _logger.LogWarning("Cannot encrypt empty or null string.");
                return null;
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _logger.LogError("DPAPI is not available on current platform. Cannot encrypt.");
                // In non-Windows environments, we could throw NotSupportedException
                // or return the original string (unsafe) depending on requirements.
                // Currently returning null to indicate error.
                return null;
            }

            try
            {
                byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] encryptedBytes = ProtectedData.Protect(plainTextBytes, optionalEntropy, Scope);
                return Convert.ToBase64String(encryptedBytes);
            }
            catch (CryptographicException ex)
            {
                _logger.LogError(ex, "DPAPI encryption error.");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unknown error during DPAPI encryption.");
                return null;
            }
        }

        /// <summary>
        /// Decrypt an encrypted string (ciphertext) back to plaintext.
        /// </summary>
        /// <param name="encryptedTextBase64">Encrypted string (in Base64 format) to decrypt.</param>
        /// <param name="optionalEntropy">Optional byte array used during encryption (must be identical, can be null).</param>
        /// <returns>Decrypted plaintext string, or null if there is an error (e.g., wrong entropy, corrupted data).</returns>
        public string? Unprotect(string encryptedTextBase64, byte[]? optionalEntropy = null)
        {
            if (string.IsNullOrEmpty(encryptedTextBase64))
            {
                _logger.LogWarning("Cannot decrypt empty or null encrypted string.");
                return null;
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _logger.LogError("DPAPI is not available on current platform. Cannot decrypt.");
                return null;
            }

            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(encryptedTextBase64);
                byte[] plainTextBytes = ProtectedData.Unprotect(encryptedBytes, optionalEntropy, Scope);
                return Encoding.UTF8.GetString(plainTextBytes);
            }
            catch (FormatException ex)
            {
                _logger.LogError(ex, "Base64 format error during decryption.");
                return null;
            }
            catch (CryptographicException ex)
            {
                // This error usually occurs if data is corrupted, wrong scope, or wrong entropy.
                _logger.LogError(ex, "DPAPI decryption error. Data may be corrupted or cannot be decrypted on this machine/by this user (if scope is CurrentUser).");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unknown error during DPAPI decryption.");
                return null;
            }
        }
    }
}
