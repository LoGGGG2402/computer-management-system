namespace CMSAgent.Common.Enums
{
    /// <summary>
    /// Các loại lỗi để báo cáo lên server.
    /// </summary>
    public enum ErrorType
    {
        /// <summary>
        /// Lỗi kết nối WebSocket.
        /// </summary>
        WEBSOCKET_CONNECTION_FAILED,

        /// <summary>
        /// Lỗi xác thực WebSocket.
        /// </summary>
        WEBSOCKET_AUTH_FAILED,

        /// <summary>
        /// Lỗi gửi yêu cầu HTTP.
        /// </summary>
        HTTP_REQUEST_FAILED,

        /// <summary>
        /// Lỗi tải file cấu hình.
        /// </summary>
        CONFIG_LOAD_FAILED,

        /// <summary>
        /// Lỗi xác thực cấu hình không hợp lệ.
        /// </summary>
        CONFIG_VALIDATION_FAILED,

        /// <summary>
        /// Lỗi giải mã token.
        /// </summary>
        TOKEN_DECRYPTION_FAILED,

        /// <summary>
        /// Lỗi thu thập thông tin phần cứng.
        /// </summary>
        HARDWARE_INFO_COLLECTION_FAILED,

        /// <summary>
        /// Lỗi báo cáo trạng thái lên server.
        /// </summary>
        STATUS_REPORTING_FAILED,

        /// <summary>
        /// Lỗi thực thi lệnh.
        /// </summary>
        COMMAND_EXECUTION_FAILED,

        /// <summary>
        /// Lỗi hàng đợi lệnh đã đầy.
        /// </summary>
        COMMAND_QUEUE_FULL,

        /// <summary>
        /// Lỗi tải gói cập nhật.
        /// </summary>
        UPDATE_DOWNLOAD_FAILED,

        /// <summary>
        /// Lỗi kiểm tra checksum gói cập nhật.
        /// </summary>
        UPDATE_CHECKSUM_MISMATCH,

        /// <summary>
        /// Lỗi giải nén gói cập nhật.
        /// </summary>
        UPDATE_EXTRACTION_FAILED,

        /// <summary>
        /// Lỗi thực hiện rollback sau khi cập nhật thất bại.
        /// </summary>
        UPDATE_ROLLBACK_FAILED,

        /// <summary>
        /// Lỗi khởi động service sau khi cập nhật.
        /// </summary>
        UPDATE_SERVICE_START_FAILED,

        /// <summary>
        /// Lỗi ghi log.
        /// </summary>
        LOGGING_FAILED,

        /// <summary>
        /// Lỗi vượt quá giới hạn tài nguyên.
        /// </summary>
        RESOURCE_LIMIT_EXCEEDED,

        /// <summary>
        /// Lỗi ngoại lệ không được xử lý.
        /// </summary>
        UNHANDLED_EXCEPTION,

        /// <summary>
        /// Lỗi khi xử lý queue offline.
        /// </summary>
        OFFLINE_QUEUE_ERROR,

        /// <summary>
        /// Yêu cầu tải lên log từ server.
        /// </summary>
        LOG_UPLOAD_REQUESTED
    }
}