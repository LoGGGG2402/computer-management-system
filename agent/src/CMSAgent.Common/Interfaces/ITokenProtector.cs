using System;

namespace CMSAgent.Common.Interfaces
{
    /// <summary>
    /// Interface để mã hóa và giải mã token xác thực của agent.
    /// </summary>
    public interface ITokenProtector
    {
        /// <summary>
        /// Mã hóa token plaintext bằng DPAPI.
        /// </summary>
        /// <param name="plainToken">Token gốc dạng plaintext.</param>
        /// <returns>Chuỗi token đã mã hóa ở dạng Base64.</returns>
        /// <exception cref="System.Security.Cryptography.CryptographicException">Ném ra khi gặp lỗi mã hóa.</exception>
        string EncryptToken(string plainToken);

        /// <summary>
        /// Giải mã token đã mã hóa bằng DPAPI.
        /// </summary>
        /// <param name="encryptedTokenBase64">Token đã mã hóa ở dạng Base64.</param>
        /// <returns>Token gốc dạng plaintext.</returns>
        /// <exception cref="System.Security.Cryptography.CryptographicException">Ném ra khi gặp lỗi giải mã.</exception>
        string DecryptToken(string encryptedTokenBase64);
    }
}
