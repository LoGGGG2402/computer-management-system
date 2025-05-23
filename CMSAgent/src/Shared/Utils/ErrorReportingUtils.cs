using CMSAgent.Shared.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace CMSAgent.Shared.Utils
{
    /// <summary>
    /// Provides utility methods for generating standardized error reports and handling error information.
    /// This class centralizes error reporting functionality and ensures consistent error format across the application.
    /// </summary>
    public static class ErrorReportingUtils
    {
        /// <summary>
        /// JSON serializer options for consistent error detail serialization across the application.
        /// </summary>
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Creates a standardized error report containing detailed information about an error condition.
        /// </summary>
        /// <param name="errorType">The category or classification of the error.</param>
        /// <param name="message">The primary error message describing what went wrong.</param>
        /// <param name="exception">Optional exception object containing additional error details and stack trace.</param>
        /// <param name="customDetails">Optional object containing any additional context-specific information.</param>
        /// <returns>A complete AgentErrorReport object containing all provided error information.</returns>
        /// <exception cref="ArgumentNullException">Thrown when errorType or message is null or whitespace.</exception>
        /// <remarks>
        /// This method combines all error information into a single, structured report object.
        /// If both exception and customDetails are provided, they are combined into a single details object.
        /// </remarks>
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

        /// <summary>
        /// Extracts detailed diagnostic information from an Exception object, including nested exceptions.
        /// </summary>
        /// <param name="exception">The exception to process.</param>
        /// <param name="depth">Current depth in the exception chain, used to prevent infinite recursion.</param>
        /// <returns>An object containing structured exception information including type, message, stack trace, and inner exceptions.</returns>
        /// <remarks>
        /// Processes the exception chain up to 5 levels deep to prevent excessive processing of deeply nested exceptions.
        /// For each exception, captures the full type name, message, stack trace, source, and HResult.
        /// </remarks>
        private static object ExtractExceptionInfo(Exception exception, int depth = 0)
        {
            if (depth > 5) // Limit the depth of inner exceptions
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

        /// <summary>
        /// Handles the safe serialization of error details to ensure they can be properly stored and transmitted.
        /// </summary>
        /// <param name="detailsPayload">The object containing the error details to be serialized.</param>
        /// <param name="errorType">The type of error being processed, used for logging if serialization fails.</param>
        /// <returns>
        /// For complex objects: Returns the original object.
        /// For simple types: Returns a wrapper containing the JSON serialized string.
        /// On error: Returns a minimal error object with basic failure information.
        /// </returns>
        /// <remarks>
        /// This method ensures that all error details can be properly serialized while preserving
        /// as much information as possible, even in failure scenarios.
        /// </remarks>
        private static object SerializeDetailsSafely(object detailsPayload, string errorType)
        {
            try
            {
                var jsonString = JsonSerializer.Serialize(detailsPayload, _jsonOptions);

                if (detailsPayload.GetType().IsClass && detailsPayload.GetType() != typeof(string))
                {
                    return detailsPayload;
                }

                return new { SerializedDetails = jsonString };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "JSON serialization failed for error type {ErrorType}, using minimal details", errorType);

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
