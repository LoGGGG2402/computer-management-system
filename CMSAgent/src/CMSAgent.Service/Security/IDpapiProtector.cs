// CMSAgent.Service/Security/IDpapiProtector.cs
namespace CMSAgent.Service.Security
{
    /// <summary>
    /// Interface defining methods to encrypt and decrypt data
    /// using Windows Data Protection API (DPAPI).
    /// Main purpose is to protect agentToken.
    /// </summary>
    public interface IDpapiProtector
    {
        /// <summary>
        /// Encrypt a plaintext string.
        /// </summary>
        /// <param name="plainText">String to encrypt.</param>
        /// <param name="optionalEntropy">Optional byte array to enhance security (can be null).</param>
        /// <returns>Encrypted string in Base64 format, or null if there is an error.</returns>
        string? Protect(string plainText, byte[]? optionalEntropy = null);

        /// <summary>
        /// Decrypt an encrypted string (ciphertext) back to plaintext.
        /// </summary>
        /// <param name="encryptedTextBase64">Encrypted string (in Base64 format) to decrypt.</param>
        /// <param name="optionalEntropy">Optional byte array used during encryption (must be identical, can be null).</param>
        /// <returns>Decrypted plaintext string, or null if there is an error (e.g., wrong entropy, corrupted data).</returns>
        string? Unprotect(string encryptedTextBase64, byte[]? optionalEntropy = null);
    }
}
