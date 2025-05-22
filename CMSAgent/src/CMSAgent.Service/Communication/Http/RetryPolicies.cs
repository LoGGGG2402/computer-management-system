// CMSAgent.Service/Communication/Http/RetryPolicies.cs
using Polly;
using Polly.Extensions.Http;
using System;
using System.Net.Http;
using CMSAgent.Service.Configuration.Models; // For HttpRetryPolicySettings
using Microsoft.Extensions.Logging;

namespace CMSAgent.Service.Communication.Http
{
    /// <summary>
    /// Defines retry policies for HttpClient using Polly.
    /// </summary>
    public static class RetryPolicies
    {
        /// <summary>
        /// Creates a retry policy based on configuration.
        /// This policy will retry HTTP requests that fail due to temporary network errors or 5xx server errors.
        /// </summary>
        /// <param name="retrySettings">Settings for the retry policy.</param>
        /// <param name="logger">Logger to record retry attempts information.</param>
        /// <returns>An IAsyncPolicy<HttpResponseMessage> to use with HttpClient.</returns>
        public static IAsyncPolicy<HttpResponseMessage> GetHttpRetryPolicy(HttpRetryPolicySettings retrySettings, ILogger logger)
        {
            if (retrySettings == null)
            {
                // Use default values if no configuration provided
                retrySettings = new HttpRetryPolicySettings();
                logger.LogWarning("HttpRetryPolicySettings not provided, using default values.");
            }

            return HttpPolicyExtensions
                .HandleTransientHttpError() // Handle HttpRequestException, 5XX, 408
                .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound && msg.RequestMessage?.Method == HttpMethod.Get) // Can retry GET 404
                .WaitAndRetryAsync(
                    retryCount: retrySettings.MaxRetries,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(retrySettings.InitialDelaySeconds, retryAttempt)), // Exponential backoff
                    // Or can add jitter:
                    // sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(retrySettings.InitialDelaySeconds, retryAttempt))
                    //                                         + TimeSpan.FromMilliseconds(new Random().Next(0, 1000)),
                    onRetry: (outcome, timespan, retryAttempt, context) =>
                    {
                        var request = outcome.Result?.RequestMessage ?? (outcome.Exception as HttpRequestException)?.HttpRequestMessage;
                        var uri = request?.RequestUri?.ToString() ?? "N/A";
                        var statusCode = outcome.Result?.StatusCode.ToString() ?? "N/A";

                        logger.LogWarning(
                            "Retrying HTTP request attempt {RetryAttempt}/{MaxRetries} to {Uri} after {Timespan} seconds. Reason: {StatusCodeOrException}",
                            retryAttempt,
                            retrySettings.MaxRetries,
                            uri,
                            timespan.TotalSeconds,
                            outcome.Exception?.Message ?? $"Status code {statusCode}"
                        );
                    }
                );
        }

        /// <summary>
        /// Creates a no-retry policy (NoOp policy).
        /// </summary>
        /// <returns>An IAsyncPolicy<HttpResponseMessage> that does not perform retries.</returns>
        public static IAsyncPolicy<HttpResponseMessage> GetNoRetryPolicy()
        {
            return Policy.NoOpAsync<HttpResponseMessage>();
        }
    }
}
