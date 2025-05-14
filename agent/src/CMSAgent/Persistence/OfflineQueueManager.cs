using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CMSAgent.Common.DTOs;
using CMSAgent.Common.Interfaces;
using CMSAgent.Common.Models;
using CMSAgent.Security;
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
        private readonly TokenProtector _tokenProtector;
        private readonly FileQueue<CommandResultPayload> _commandResultQueue;
        private readonly FileQueue<ErrorReportPayload> _errorReportQueue;
        private readonly string _basePath;
        private readonly FileQueue<object> _fileQueue;

        /// <summary>
        /// Khởi tạo một instance mới của OfflineQueueManager.
        /// </summary>
        /// <param name="logger">Logger để ghi nhật ký.</param>
        /// <param name="configLoader">ConfigLoader để tải cấu hình.</param>
        /// <param name="queueSettings">Cấu hình cho hàng đợi offline.</param>
        /// <param name="tokenProtector">Protector để mã hóa/giải mã token.</param>
        /// <param name="wsConnector">Connector để kết nối WebSocket.</param>
        /// <param name="httpClient">HTTP client để gửi request.</param>
        public OfflineQueueManager(
            ILogger<OfflineQueueManager> logger,
            IConfigLoader configLoader,
            IOptions<OfflineQueueSettingsOptions> queueSettings,
            TokenProtector tokenProtector,
            IWebSocketConnector wsConnector,
            IHttpClientWrapper httpClient)
        {
            _logger = logger;
            _configLoader = configLoader;
            _tokenProtector = tokenProtector;
            _wsConnector = wsConnector;
            _httpClient = httpClient;

            // Xác định đường dẫn cơ sở cho hàng đợi offline
            _basePath = queueSettings.Value.BasePath ?? Path.Combine(configLoader.GetDataPath(), "offline_queue");

            _commandResultQueue = new FileQueue<CommandResultPayload>(
                Path.Combine(_basePath, "command_results"),
                logger,
                queueSettings.Value.CommandResultsMaxCount,
                queueSettings.Value.MaxSizeMb,
                queueSettings.Value.MaxAgeHours);

            _errorReportQueue = new FileQueue<ErrorReportPayload>(
                Path.Combine(_basePath, "error_reports"),
                logger,
                queueSettings.Value.ErrorReportsMaxCount,
                queueSettings.Value.MaxSizeMb,
                queueSettings.Value.MaxAgeHours);

            var settings = queueSettings.Value;
            _fileQueue = new FileQueue<object>(
                settings.QueueDirectory,
                logger,
                settings.MaxCount,
                settings.MaxSizeMb,
                settings.MaxAgeHours);
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
            if (runtimeConfig == null || string.IsNullOrEmpty(runtimeConfig.AgentTokenEncrypted))
            {
                _logger.LogWarning("Không thể xử lý hàng đợi offline: runtime config hoặc token không tồn tại");
                return;
            }

            string decryptedToken;
            try 
            {
                decryptedToken = _tokenProtector.DecryptToken(runtimeConfig.AgentTokenEncrypted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể xử lý hàng đợi offline: không thể giải mã token");
                return;
            }

            var agentId = runtimeConfig.AgentId ?? string.Empty;
            
            await ProcessSpecificQueueAsync(
                _commandResultQueue,
                async (item) => await _wsConnector.SendCommandResultAsync(item.Data),
                cancellationToken);
            
            await ProcessSpecificQueueAsync(
                _errorReportQueue,
                async (item) => {
                    await TrySendErrorReportViaHttpAsync(item.Data, agentId, decryptedToken);
                    return true;
                },
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
