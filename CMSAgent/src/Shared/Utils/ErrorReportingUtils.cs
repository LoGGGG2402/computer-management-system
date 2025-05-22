using CMSAgent.Shared.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace CMSAgent.Shared.Utils
{
    /// <summary>
    /// Provides utility methods to create standard error report objects.
    /// </summary>
    public static class ErrorReportingUtils
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Create an AgentErrorReport object.
        /// </summary>
        /// <param name="errorType">Error type (see AgentConstants for update error types).</param>
        /// <param name="message">Main error message.</param>
        /// <param name="exception">Exception object (if any) to extract detailed info.</param>
        /// <param name="customDetails">An object containing other custom details.</param>
        /// <returns>An AgentErrorReport object.</returns>
        public static AgentErrorReport CreateErrorReport(
            string errorType,
            string message,
            Exception? exception = null,
            object? customDetails = null)
        {
            if (string.IsNullOrWhiteSpace(errorType))
                throw new ArgumentNullException(nameof(errorType));
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentNullException(nameof(message));

            var report = new AgentErrorReport
            {
                Type = errorType,
                Message = message
            };

            object? detailsPayload = null;
            if (exception != null && customDetails != null)
            {
                detailsPayload = new
                {
                    ExceptionInfo = ExtractExceptionInfo(exception),
                    CustomInfo = customDetails
                };
            }
            else if (exception != null)
            {
                detailsPayload = new
                {
                    ExceptionInfo = ExtractExceptionInfo(exception)
                };
            }
            else if (customDetails != null)
            {
                detailsPayload = customDetails;
            }

            if (detailsPayload != null)
            {
                report.Details = SerializeDetailsSafely(detailsPayload, errorType);
            }

            return report;
        }

        private static object ExtractExceptionInfo(Exception exception, int depth = 0)
        {
            if (depth > 5) // Giới hạn độ sâu của inner exceptions
            {
                return new { Message = "Inner exception chain too deep" };
            }

            var info = new
            {
                Type = exception.GetType().FullName,
                exception.Message,
                exception.StackTrace,
                Source = exception.Source,
                HResult = exception.HResult,
                InnerException = exception.InnerException != null 
                    ? ExtractExceptionInfo(exception.InnerException, depth + 1) 
                    : null
            };

            return info;
        }

        private static object SerializeDetailsSafely(object detailsPayload, string errorType)
        {
            try
            {
                // Thử serialize thành JSON string để đảm bảo tính hợp lệ
                var jsonString = JsonSerializer.Serialize(detailsPayload, _jsonOptions);
                
                // Nếu detailsPayload là một đối tượng phức tạp, trả về nó trực tiếp
                if (detailsPayload.GetType().IsClass && detailsPayload.GetType() != typeof(string))
                {
                    return detailsPayload;
                }
                
                // Nếu là kiểu dữ liệu đơn giản, trả về JSON string
                return new { SerializedDetails = jsonString };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "JSON serialization failed for error type {ErrorType}, using minimal details", errorType);
                
                // Fallback: chỉ lưu thông tin cơ bản
                return new
                {
                    Error = "Failed to serialize details",
                    ErrorMessage = ex.Message,
                    OriginalType = detailsPayload.GetType().FullName
                };
            }
        }
    }
}
