using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using CMSAgent.Common.DTOs;
using CMSAgent.Common.Enums;

namespace CMSAgent.Common.Logging
{
    /// <summary>
    /// Lớp tiện ích để ghi lại các lỗi dưới dạng JSON theo chuẩn ErrorReportPayload
    /// </summary>
    public static class ErrorLogs
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        
        private static readonly string _errorLogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "CMSAgent",
            "error_reports");
            
        /// <summary>
        /// Ghi lỗi vào file JSON
        /// </summary>
        /// <param name="errorType">Loại lỗi</param>
        /// <param name="errorMessage">Thông điệp lỗi</param>
        /// <param name="errorDetails">Chi tiết lỗi (có thể là string hoặc object)</param>
        /// <param name="logger">Logger để ghi log bổ sung (tùy chọn)</param>
        public static void LogError(ErrorType errorType, string errorMessage, object errorDetails, ILogger? logger = null)
        {
            try
            {
                // Tạo payload lỗi
                var errorPayload = new ErrorReportPayload
                {
                    error_type = errorType,
                    error_message = errorMessage,
                    error_details = errorDetails,
                    timestamp = DateTime.Now
                };
                
                // Đảm bảo thư mục tồn tại
                if (!Directory.Exists(_errorLogDirectory))
                {
                    Directory.CreateDirectory(_errorLogDirectory);
                }
                
                // Tạo tên file với timestamp
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string errorFile = Path.Combine(_errorLogDirectory, 
                    $"error_{errorType}_{timestamp}.json");
                
                // Serialize và ghi ra file
                string jsonContent = JsonSerializer.Serialize(errorPayload, _jsonOptions);
                File.WriteAllText(errorFile, jsonContent);
                
                // Ghi log nếu ILogger được cung cấp
                logger?.LogError("Đã xảy ra lỗi {ErrorType}: {ErrorMessage}", errorType, errorMessage);
            }
            catch (Exception ex)
            {
                // Xử lý lỗi khi ghi log thất bại
                logger?.LogError(ex, "Không thể ghi lỗi vào file JSON: {ErrorMessage}", ex.Message);
            }
        }
        
        /// <summary>
        /// Ghi lỗi từ Exception vào file JSON
        /// </summary>
        /// <param name="errorType">Loại lỗi</param>
        /// <param name="exception">Exception cần ghi lại</param>
        /// <param name="logger">Logger để ghi log bổ sung (tùy chọn)</param>
        public static void LogException(ErrorType errorType, Exception exception, ILogger? logger = null)
        {
            LogError(
                errorType,
                exception.Message,
                new
                {
                    ExceptionType = exception.GetType().Name,
                    exception.StackTrace,
                    InnerException = exception.InnerException?.Message
                },
                logger
            );
        }
    }
} 