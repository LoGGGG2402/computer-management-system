using CMSAgent.Shared.Models; 
using Serilog;

namespace CMSAgent.Shared.Utils
{
    /// <summary>
    /// Provides utility methods to create standard error report objects.
    /// </summary>
    public static class ErrorReportingUtils
    {
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
                    ExceptionInfo = new
                    {
                        Type = exception.GetType().FullName,
                        exception.Message,
                        exception.StackTrace,
                        InnerException = exception.InnerException != null ? new { Type = exception.InnerException.GetType().FullName, exception.InnerException.Message, exception.InnerException.StackTrace } : null
                    },
                    CustomInfo = customDetails
                };
            }
            else if (exception != null)
            {
                detailsPayload = new
                {
                    ExceptionInfo = new
                    {
                        Type = exception.GetType().FullName,
                        exception.Message,
                        exception.StackTrace,
                        InnerException = exception.InnerException != null ? new { Type = exception.InnerException.GetType().FullName, exception.InnerException.Message, exception.InnerException.StackTrace } : null
                    }
                };
            }
            else if (customDetails != null)
            {
                detailsPayload = customDetails;
            }

            if (detailsPayload != null)
            {
                try
                {
                    report.Details = detailsPayload;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to serialize error report details for error type {ErrorType}", errorType);
                    report.Details = $"Failed to serialize details: {ex.Message}";
                }
            }

            return report;
        }
    }
}
