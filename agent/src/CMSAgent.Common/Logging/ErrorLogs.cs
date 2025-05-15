using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using CMSAgent.Common.DTOs;
using CMSAgent.Common.Enums;

namespace CMSAgent.Common.Logging
{
    /// <summary>
    /// Utility class for logging errors in JSON format according to ErrorReportPayload standard
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
        /// Log error to JSON file
        /// </summary>
        /// <param name="errorType">Error type</param>
        /// <param name="errorMessage">Error message</param>
        /// <param name="errorDetails">Error details (can be string or object)</param>
        /// <param name="logger">Logger for additional logging (optional)</param>
        public static void LogError(ErrorType errorType, string errorMessage, object errorDetails, ILogger? logger = null)
        {
            try
            {
                // Create error payload
                var errorPayload = new ErrorReportPayload
                {
                    error_type = errorType,
                    error_message = errorMessage,
                    error_details = errorDetails,
                    timestamp = DateTime.Now
                };
                
                // Ensure directory exists
                if (!Directory.Exists(_errorLogDirectory))
                {
                    Directory.CreateDirectory(_errorLogDirectory);
                }
                
                // Create file name with timestamp
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string errorFile = Path.Combine(_errorLogDirectory, 
                    $"error_{errorType}_{timestamp}.json");
                
                // Serialize and write to file
                string jsonContent = JsonSerializer.Serialize(errorPayload, _jsonOptions);
                File.WriteAllText(errorFile, jsonContent);
                
                // Log if ILogger is provided
                logger?.LogError("An error occurred {ErrorType}: {ErrorMessage}", errorType, errorMessage);
            }
            catch (Exception ex)
            {
                // Handle error when logging fails
                logger?.LogError(ex, "Could not write error to JSON file: {ErrorMessage}", ex.Message);
            }
        }
        
        /// <summary>
        /// Log error from Exception to JSON file
        /// </summary>
        /// <param name="errorType">Error type</param>
        /// <param name="exception">Exception to log</param>
        /// <param name="logger">Logger for additional logging (optional)</param>
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