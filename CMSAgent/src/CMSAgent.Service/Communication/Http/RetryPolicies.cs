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
    /// Định nghĩa các chính sách retry cho HttpClient sử dụng Polly.
    /// </summary>
    public static class RetryPolicies
    {
        /// <summary>
        /// Tạo một chính sách retry dựa trên cấu hình.
        /// Chính sách này sẽ thử lại các yêu cầu HTTP thất bại do lỗi mạng tạm thời hoặc lỗi server 5xx.
        /// </summary>
        /// <param name="retrySettings">Cài đặt cho chính sách retry.</param>
        /// <param name="logger">Logger để ghi lại thông tin về các lần thử lại.</param>
        /// <returns>Một IAsyncPolicy<HttpResponseMessage> để sử dụng với HttpClient.</returns>
        public static IAsyncPolicy<HttpResponseMessage> GetHttpRetryPolicy(HttpRetryPolicySettings retrySettings, ILogger logger)
        {
            if (retrySettings == null)
            {
                // Sử dụng giá trị mặc định nếu không có cấu hình
                retrySettings = new HttpRetryPolicySettings();
                logger.LogWarning("HttpRetryPolicySettings không được cung cấp, sử dụng giá trị mặc định.");
            }

            return HttpPolicyExtensions
                .HandleTransientHttpError() // Xử lý HttpRequestException, 5XX, 408
                .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound && msg.RequestMessage?.Method == HttpMethod.Get) // Có thể thử lại GET 404
                .WaitAndRetryAsync(
                    retryCount: retrySettings.MaxRetries,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(retrySettings.InitialDelaySeconds, retryAttempt)), // Exponential backoff
                    // Hoặc có thể thêm jitter:
                    // sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(retrySettings.InitialDelaySeconds, retryAttempt))
                    //                                         + TimeSpan.FromMilliseconds(new Random().Next(0, 1000)),
                    onRetry: (outcome, timespan, retryAttempt, context) =>
                    {
                        var request = outcome.Result?.RequestMessage ?? (outcome.Exception as HttpRequestException)?.HttpRequestMessage;
                        var uri = request?.RequestUri?.ToString() ?? "N/A";
                        var statusCode = outcome.Result?.StatusCode.ToString() ?? "N/A";

                        logger.LogWarning(
                            "Thử lại yêu cầu HTTP lần {RetryAttempt}/{MaxRetries} đến {Uri} sau {Timespan} giây. Lý do: {StatusCodeOrException}",
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
        /// Tạo một chính sách không retry (NoOp policy).
        /// </summary>
        /// <returns>Một IAsyncPolicy<HttpResponseMessage> không thực hiện retry.</returns>
        public static IAsyncPolicy<HttpResponseMessage> GetNoRetryPolicy()
        {
            return Policy.NoOpAsync<HttpResponseMessage>();
        }
    }
}
