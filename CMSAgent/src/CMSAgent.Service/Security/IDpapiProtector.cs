 // CMSAgent.Service/Security/IDpapiProtector.cs
namespace CMSAgent.Service.Security
{
    /// <summary>
    /// Interface định nghĩa các phương thức để mã hóa và giải mã dữ liệu
    /// sử dụng Windows Data Protection API (DPAPI).
    /// Mục đích chính là để bảo vệ agentToken.
    /// </summary>
    public interface IDpapiProtector
    {
        /// <summary>
        /// Mã hóa một chuỗi văn bản gốc (plaintext).
        /// </summary>
        /// <param name="plainText">Chuỗi cần mã hóa.</param>
        /// <param name="optionalEntropy">Một mảng byte tùy chọn để tăng cường độ bảo mật (có thể là null).</param>
        /// <returns>Chuỗi đã được mã hóa dưới dạng Base64, hoặc null nếu có lỗi.</returns>
        string? Protect(string plainText, byte[]? optionalEntropy = null);

        /// <summary>
        /// Giải mã một chuỗi đã được mã hóa (ciphertext) trở lại văn bản gốc.
        /// </summary>
        /// <param name="encryptedTextBase64">Chuỗi đã mã hóa (dưới dạng Base64) cần giải mã.</param>
        /// <param name="optionalEntropy">Mảng byte tùy chọn đã được sử dụng khi mã hóa (phải giống hệt, có thể là null).</param>
        /// <returns>Chuỗi văn bản gốc đã được giải mã, hoặc null nếu có lỗi (ví dụ: sai entropy, dữ liệu hỏng).</returns>
        string? Unprotect(string encryptedTextBase64, byte[]? optionalEntropy = null);
    }
}
