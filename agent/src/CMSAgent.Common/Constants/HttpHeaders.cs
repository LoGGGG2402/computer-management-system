namespace CMSAgent.Common.Constants
{
    /// <summary>
    /// Hằng số cho HTTP headers sử dụng trong giao tiếp giữa agent và server.
    /// </summary>
    public static class HttpHeaders
    {
        /// <summary>
        /// Header chứa mã định danh của agent.
        /// </summary>
        public const string AgentIdHeader = "X-Agent-Id";

        /// <summary>
        /// Header xác định loại client.
        /// </summary>
        public const string ClientTypeHeader = "X-Client-Type";

        /// <summary>
        /// Giá trị cho ClientTypeHeader khi gửi từ agent.
        /// </summary>
        public const string ClientTypeValue = "agent";

        /// <summary>
        /// Header xác thực.
        /// </summary>
        public const string AuthorizationHeader = "Authorization";

        /// <summary>
        /// Tiền tố cho token Bearer trong header Authorization.
        /// </summary>
        public const string BearerPrefix = "Bearer ";
    }
}
