using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CMSAgent.Common.DTOs;
using CMSAgent.Common.Interfaces;
using CMSAgent.Common.Models;
using CMSAgent.Persistence.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CMSAgent.Persistence
{
    /// <summary>
    /// Quản lý các hàng đợi offline khi mất kết nối với server.
    /// </summary>
    public class OfflineQueueManager : IOfflineQueueManager
    {
        private readonly ILogger<OfflineQueueManager> _logger;
        private readonly IConfigLoader _configLoader;
        private readonly IWebSocketConnector _wsConnector;
        private readonly IHttpClientWrapper _httpClient;
        private readonly ITokenProtector _tokenProtector;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly FileQueue<StatusUpdatePayload> _statusQueue;
        private readonly FileQueue<CommandResultPayload> _commandResultQueue;
        private readonly FileQueue<ErrorReportPayload> _errorReportQueue;
        private readonly string _basePath;

        /// <summary>
        /// Khởi tạo một instance mới của OfflineQueueManager.
        /// </summary>
        /// <param name="logger">Logger để ghi nhật ký.</param>
        /// <param name="configLoader">ConfigLoader để tải cấu hình.</param>
        /// <param name="queueSettings">Cấu hình cho hàng đợi offline.</param>
        /// <param name="dateTimeProvider">Provider để lấy thời gian hiện tại.</param>
        /// <param name="wsConnector">Connector để kết nối WebSocket.</param>
        /// <param name="httpClient">HTTP client để gửi request.</param>
        /// <param name="tokenProtector">Protector để mã hóa/giải mã token.</param>
        public OfflineQueueManager(
            ILogger<OfflineQueueManager> logger,
            IConfigLoader configLoader,
            IOptions<OfflineQueueSettingsOptions> queueSettings,
            IDateTimeProvider dateTimeProvider,
            IWebSocketConnector wsConnector,
            IHttpClientWrapper httpClient,
            ITokenProtector tokenProtector)
        {
            _logger = logger;
            _configLoader = configLoader;
            _dateTimeProvider = dateTimeProvider;
            _wsConnector = wsConnector;
            _httpClient = httpClient;
            _tokenProtector = tokenProtector;

            // Xác định đường dẫn cơ sở cho hàng đợi offline
            _basePath = queueSettings.Value.BasePath ?? Path.Combine(configLoader.GetDataPath(), "offline_queue");

            // Khởi tạo các hàng đợi
            _statusQueue = new FileQueue<StatusUpdatePayload>(
                Path.Combine(_basePath, "status_reports"),
                logger,
                dateTimeProvider,
                queueSettings.Value.StatusReportsMaxCount,
                queueSettings.Value.MaxSizeMb,
                queueSettings.Value.MaxAgeHours);

            _commandResultQueue = new FileQueue<CommandResultPayload>(
                Path.Combine(_basePath, "command_results"),
                logger,
                dateTimeProvider,
                queueSettings.Value.CommandResultsMaxCount,
                queueSettings.Value.MaxSizeMb,
                queueSettings.Value.MaxAgeHours);

            _errorReportQueue = new FileQueue<ErrorReportPayload>(
                Path.Combine(_basePath, "error_reports"),
                logger,
                dateTimeProvider,
                queueSettings.Value.ErrorReportsMaxCount,
                queueSettings.Value.MaxSizeMb,
                queueSettings.Value.MaxAgeHours);
        }

        /// <summary>
        /// Thêm một báo cáo trạng thái vào hàng đợi.
        /// </summary>
        /// <param name="payload">Payload chứa thông tin cập nhật trạng thái.</param>
        /// <returns>Task đại diện cho thao tác thêm vào hàng đợi.</returns>
        public async Task EnqueueStatusReportAsync(StatusUpdatePayload payload)
        {
            try
            {
                await _statusQueue.EnqueueAsync(payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thêm báo cáo trạng thái vào hàng đợi offline");
            }
        }

        /// <summary>
        /// Thêm một kết quả lệnh vào hàng đợi.
        /// </summary>
        /// <param name="payload">Payload chứa kết quả lệnh.</param>
        /// <returns>Task đại diện cho thao tác thêm vào hàng đợi.</returns>
        public async Task EnqueueCommandResultAsync(CommandResultPayload payload)
        {
            try
            {
                await _commandResultQueue.EnqueueAsync(payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thêm kết quả lệnh vào hàng đợi offline");
            }
        }

        /// <summary>
        /// Thêm một báo cáo lỗi vào hàng đợi.
        /// </summary>
        /// <param name="payload">Payload chứa thông tin lỗi.</param>
        /// <returns>Task đại diện cho thao tác thêm vào hàng đợi.</returns>
        public async Task EnqueueErrorReportAsync(ErrorReportPayload payload)
        {
            try
            {
                await _errorReportQueue.EnqueueAsync(payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thêm báo cáo lỗi vào hàng đợi offline");
            }
        }

        /// <summary>
        /// Xử lý tất cả các hàng đợi offline và gửi dữ liệu lên server.
        /// </summary>
        /// <param name="cancellationToken">Token để hủy thao tác.</param>
        /// <returns>Task đại diện cho thao tác xử lý hàng đợi.</returns>
        public async Task ProcessQueuesAsync(CancellationToken cancellationToken)
        {
            var runtimeConfig = await _configLoader.LoadRuntimeConfigAsync();
            if (runtimeConfig == null || string.IsNullOrEmpty(runtimeConfig.agent_token_encrypted))
            {
                _logger.LogWarning("Không thể xử lý hàng đợi offline: runtime config hoặc token không tồn tại");
                return;
            }

            string decryptedToken;
            try 
            {
                decryptedToken = _tokenProtector.DecryptToken(runtimeConfig.agent_token_encrypted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể xử lý hàng đợi offline: không thể giải mã token");
                return;
            }

            var agentId = runtimeConfig.agentId;

            await ProcessSpecificQueueAsync(
                _statusQueue,
                async (item) => await _wsConnector.SendStatusUpdateAsync(item.Data),
                cancellationToken);
            
            await ProcessSpecificQueueAsync(
                _commandResultQueue,
                async (item) => await _wsConnector.SendCommandResultAsync(item.Data),
                cancellationToken);
            
            await ProcessSpecificQueueAsync(
                _errorReportQueue,
                async (item) => await TrySendErrorReportViaHttpAsync(item.Data, agentId, decryptedToken),
                cancellationToken);
        }

        private async Task ProcessSpecificQueueAsync<T>(
            FileQueue<T> queue,
            Func<QueuedItem<T>, Task<bool>> sendAction,
            CancellationToken cancellationToken) where T : class
        {
            QueuedItem<T> queuedItem;
            while ((queuedItem = await queue.TryDequeueAsync()) != null && !cancellationToken.IsCancellationRequested)
            {
                bool success = false;
                try
                {
                    success = await sendAction(queuedItem);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi gửi item {ItemId} loại {Type} từ hàng đợi", 
                        queuedItem.ItemId, typeof(T).Name);
                }

                if (!success)
                {
                    await queue.RequeueAsync(queuedItem);
                    _logger.LogWarning("Không thể gửi item {ItemId}, đã thêm lại vào hàng đợi", queuedItem.ItemId);
                    break;  // Dừng xử lý hàng đợi này nếu gặp lỗi
                }
                else
                {
                    _logger.LogInformation("Đã gửi thành công item {ItemId} loại {Type} từ hàng đợi", 
                        queuedItem.ItemId, typeof(T).Name);
                }
            }
        }

        private async Task<bool> TrySendErrorReportViaHttpAsync(ErrorReportPayload payload, string agentId, string token)
        {
            try
            {
                await _httpClient.PostAsync(CMSAgent.Common.Constants.ApiRoutes.ReportError, payload, agentId, token);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể gửi báo cáo lỗi qua HTTP từ hàng đợi offline");
                return false;
            }
        }
    }
}
