using Serilog;
using System.Security.Cryptography;
using System.Text;

namespace CMSAgent.SystemOperations
{
    /// <summary>
    /// Helper class for cryptographic operations
    /// </summary>
    public static class CryptoHelper
    {
        // Use machine-specific entropy for encryption
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes(
            Environment.MachineName + 
            Environment.ProcessorCount + 
            Environment.UserDomainName);

        /// <summary>
        /// Encrypts the agent token using Windows Data Protection API
        /// </summary>
        public static string EncryptAgentToken(string token)
        {
            try
            {
                if (string.IsNullOrEmpty(token))
                {
                    return string.Empty;
                }

                byte[] tokenBytes = Encoding.UTF8.GetBytes(token);
                byte[] encryptedBytes = ProtectedData.Protect(
                    tokenBytes, 
                    Entropy, 
                    DataProtectionScope.LocalMachine);
                
                return Convert.ToBase64String(encryptedBytes);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error encrypting agent token: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Decrypts the agent token using Windows Data Protection API
        /// </summary>
        public static string DecryptAgentToken(string encryptedToken)
        {
            try
            {
                if (string.IsNullOrEmpty(encryptedToken))
                {
                    return string.Empty;
                }

                byte[] encryptedBytes = Convert.FromBase64String(encryptedToken);
                byte[] tokenBytes = ProtectedData.Unprotect(
                    encryptedBytes, 
                    Entropy, 
                    DataProtectionScope.LocalMachine);
                
                return Encoding.UTF8.GetString(tokenBytes);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error decrypting agent token: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Calculates an SHA256 hash of the input string
        /// </summary>
        public static string CalculateSHA256Hash(string input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(input);
                byte[] hash = sha256.ComputeHash(bytes);
                
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>
        /// Generates a random string of the specified length
        /// </summary>
        public static string GenerateRandomString(int length)
        {
            const string validChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] bytes = new byte[length];
                rng.GetBytes(bytes);
                
                StringBuilder sb = new StringBuilder(length);
                foreach (byte b in bytes)
                {
                    sb.Append(validChars[b % validChars.Length]);
                }
                
                return sb.ToString();
            }
        }
    }
}