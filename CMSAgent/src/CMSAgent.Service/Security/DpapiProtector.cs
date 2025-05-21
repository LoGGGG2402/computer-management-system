// CMSAgent.Service/Security/DpapiProtector.cs
using Microsoft.Extensions.Logging;
using System;
using System.Security.Cryptography; // Required for ProtectedData
using System.Text;
using System.Runtime.InteropServices; // For OSPlatform

namespace CMSAgent.Service.Security
{
    /// <summary>
    /// Triển khai IDpapiProtector sử dụng Windows Data Protection API (DPAPI).
    /// Mã hóa dữ liệu với phạm vi LocalMachine, nghĩa là chỉ có thể giải mã trên cùng một máy.
    /// </summary>
    public class DpapiProtector : IDpapiProtector
    {
        private readonly ILogger<DpapiProtector> _logger;

        // Phạm vi bảo vệ: Dữ liệu được mã hóa chỉ có thể được giải mã trên cùng một máy tính.
        private const DataProtectionScope Scope = DataProtectionScope.LocalMachine;

        public DpapiProtector(ILogger<DpapiProtector> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _logger.LogWarning("DPAPI Protector chỉ được hỗ trợ trên Windows. Các hoạt động mã hóa/giải mã sẽ không thành công trên các nền tảng khác.");
            }
        }

        /// <summary>
        /// Mã hóa một chuỗi văn bản gốc (plaintext).
        /// </summary>
        /// <param name="plainText">Chuỗi cần mã hóa.</param>
        /// <param name="optionalEntropy">Một mảng byte tùy chọn để tăng cường độ bảo mật (có thể là null).</param>
        /// <returns>Chuỗi đã được mã hóa dưới dạng Base64, hoặc null nếu có lỗi.</returns>
        public string? Protect(string plainText, byte[]? optionalEntropy = null)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                _logger.LogWarning("Không thể mã hóa chuỗi rỗng hoặc null.");
                return null;
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _logger.LogError("DPAPI không khả dụng trên nền tảng hiện tại. Không thể mã hóa.");
                // Trong môi trường không phải Windows, có thể ném NotSupportedException
                // hoặc trả về chuỗi gốc (không an toàn) tùy theo yêu cầu.
                // Hiện tại trả về null để báo lỗi.
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
                _logger.LogError(ex, "Lỗi mã hóa DPAPI.");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi không xác định trong quá trình mã hóa DPAPI.");
                return null;
            }
        }

        /// <summary>
        /// Giải mã một chuỗi đã được mã hóa (ciphertext) trở lại văn bản gốc.
        /// </summary>
        /// <param name="encryptedTextBase64">Chuỗi đã mã hóa (dưới dạng Base64) cần giải mã.</param>
        /// <param name="optionalEntropy">Mảng byte tùy chọn đã được sử dụng khi mã hóa (phải giống hệt, có thể là null).</param>
        /// <returns>Chuỗi văn bản gốc đã được giải mã, hoặc null nếu có lỗi (ví dụ: sai entropy, dữ liệu hỏng).</returns>
        public string? Unprotect(string encryptedTextBase64, byte[]? optionalEntropy = null)
        {
            if (string.IsNullOrEmpty(encryptedTextBase64))
            {
                _logger.LogWarning("Không thể giải mã chuỗi mã hóa rỗng hoặc null.");
                return null;
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _logger.LogError("DPAPI không khả dụng trên nền tảng hiện tại. Không thể giải mã.");
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
                _logger.LogError(ex, "Lỗi định dạng Base64 khi giải mã.");
                return null;
            }
            catch (CryptographicException ex)
            {
                // Lỗi này thường xảy ra nếu dữ liệu bị hỏng, sai scope, hoặc sai entropy.
                _logger.LogError(ex, "Lỗi giải mã DPAPI. Dữ liệu có thể bị hỏng hoặc không thể giải mã trên máy này/bởi user này (nếu scope là CurrentUser).");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi không xác định trong quá trình giải mã DPAPI.");
                return null;
            }
        }
    }
}
