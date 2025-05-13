using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CMSAgent.Common.Interfaces;
using CMSAgent.Persistence.Models;
using Microsoft.Extensions.Logging;

namespace CMSAgent.Persistence
{
    /// <summary>
    /// Lớp generic để quản lý hàng đợi dựa trên file.
    /// </summary>
    /// <typeparam name="T">Kiểu dữ liệu của item được lưu trữ trong hàng đợi.</typeparam>
    public class FileQueue<T> where T : class
    {
        private readonly string _queueDirectory;
        private readonly ILogger _logger;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly int _maxCount;
        private readonly long _maxSizeBytes;
        private readonly int _maxAgeHours;
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions 
        { 
            WriteIndented = false, 
            PropertyNameCaseInsensitive = true 
        };

        /// <summary>
        /// Khởi tạo một instance mới của hàng đợi file.
        /// </summary>
        /// <param name="queueDirectory">Đường dẫn thư mục để lưu trữ các file hàng đợi.</param>
        /// <param name="logger">Logger để ghi nhật ký.</param>
        /// <param name="dateTimeProvider">Provider để lấy thời gian hiện tại.</param>
        /// <param name="maxCount">Số lượng item tối đa trong hàng đợi.</param>
        /// <param name="maxSizeMb">Kích thước tối đa (MB) của hàng đợi.</param>
        /// <param name="maxAgeHours">Thời gian tối đa (giờ) một item có thể lưu trong hàng đợi.</param>
        public FileQueue(
            string queueDirectory, 
            ILogger logger, 
            IDateTimeProvider dateTimeProvider,
            int maxCount, 
            int maxSizeMb, 
            int maxAgeHours)
        {
            _queueDirectory = queueDirectory;
            _logger = logger;
            _dateTimeProvider = dateTimeProvider;
            _maxCount = maxCount;
            _maxSizeBytes = (long)maxSizeMb * 1024 * 1024;
            _maxAgeHours = maxAgeHours;

            // Đảm bảo thư mục tồn tại
            Directory.CreateDirectory(_queueDirectory);
        }

        /// <summary>
        /// Thêm một item vào hàng đợi.
        /// </summary>
        /// <param name="item">Item cần thêm vào hàng đợi.</param>
        /// <returns>Task đại diện cho thao tác thêm vào hàng đợi.</returns>
        public async Task EnqueueAsync(T item)
        {
            await CleanUpOldAndExceedingItemsAsync();

            var queuedItem = new QueuedItem<T>
            {
                ItemId = Guid.NewGuid().ToString(),
                Data = item,
                EnqueuedTimestampUtc = _dateTimeProvider.UtcNow
            };

            var filePath = Path.Combine(_queueDirectory, $"{queuedItem.ItemId}.json");

            try
            {
                await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(queuedItem, _jsonOptions));
                _logger.LogDebug("Đã thêm item {ItemId} vào hàng đợi {QueueDirectory}", queuedItem.ItemId, _queueDirectory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể thêm item vào hàng đợi {QueueDirectory}", _queueDirectory);
            }
        }

        /// <summary>
        /// Thử lấy và xóa item cũ nhất từ hàng đợi.
        /// </summary>
        /// <returns>Item được lấy ra, hoặc null nếu hàng đợi trống.</returns>
        public async Task<QueuedItem<T>> TryDequeueAsync()
        {
            var oldestFile = GetQueueFiles().OrderBy(f => f.CreationTimeUtc).FirstOrDefault();
            if (oldestFile == null)
            {
                return null;
            }

            try
            {
                var jsonContent = await File.ReadAllTextAsync(oldestFile.FullName);
                var queuedItem = JsonSerializer.Deserialize<QueuedItem<T>>(jsonContent, _jsonOptions);
                
                File.Delete(oldestFile.FullName);
                
                _logger.LogDebug("Đã lấy item {ItemId} từ hàng đợi {QueueDirectory}", queuedItem?.ItemId, _queueDirectory);
                return queuedItem;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể lấy item từ {FilePath}. File có thể bị hỏng hoặc không truy cập được. Xóa file có vấn đề.", oldestFile.FullName);
                
                try
                {
                    File.Delete(oldestFile.FullName);
                }
                catch (Exception deleteEx)
                {
                    _logger.LogError(deleteEx, "Không thể xóa file hàng đợi có vấn đề {FilePath}", oldestFile.FullName);
                }
                
                return null;
            }
        }

        /// <summary>
        /// Thêm lại một item đã lấy từ hàng đợi nhưng xử lý thất bại.
        /// </summary>
        /// <param name="queuedItem">Item cần thêm lại vào hàng đợi.</param>
        /// <returns>Task đại diện cho thao tác thêm lại vào hàng đợi.</returns>
        public async Task RequeueAsync(QueuedItem<T> queuedItem)
        {
            queuedItem.RetryAttempts++;
            queuedItem.EnqueuedTimestampUtc = _dateTimeProvider.UtcNow;

            var filePath = Path.Combine(_queueDirectory, $"{queuedItem.ItemId}.json");

            try
            {
                await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(queuedItem, _jsonOptions));
                _logger.LogDebug("Đã thêm lại item {ItemId} vào hàng đợi {QueueDirectory}, lần thử {RetryAttempts}", 
                    queuedItem.ItemId, _queueDirectory, queuedItem.RetryAttempts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể thêm lại item {ItemId} vào hàng đợi {QueueDirectory}", 
                    queuedItem.ItemId, _queueDirectory);
            }
        }

        /// <summary>
        /// Lấy danh sách tất cả các file trong thư mục hàng đợi.
        /// </summary>
        /// <returns>Danh sách FileInfo của các file trong thư mục hàng đợi.</returns>
        private IEnumerable<FileInfo> GetQueueFiles()
        {
            try
            {
                return new DirectoryInfo(_queueDirectory).GetFiles("*.json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể lấy danh sách file từ thư mục hàng đợi {QueueDirectory}", _queueDirectory);
                return Enumerable.Empty<FileInfo>();
            }
        }

        /// <summary>
        /// Dọn dẹp các file cũ hoặc vượt quá giới hạn trong hàng đợi.
        /// </summary>
        /// <returns>Task đại diện cho thao tác dọn dẹp.</returns>
        private async Task CleanUpOldAndExceedingItemsAsync()
        {
            try
            {
                var files = GetQueueFiles().ToList();
                
                // Kiểm tra số lượng file
                if (files.Count >= _maxCount)
                {
                    _logger.LogWarning("Hàng đợi {QueueDirectory} đã đạt giới hạn số lượng {MaxCount}, đang dọn dẹp...", 
                        _queueDirectory, _maxCount);
                    
                    // Sắp xếp theo thời gian tạo, xóa file cũ nhất
                    var filesToDelete = files.OrderBy(f => f.CreationTimeUtc)
                        .Take(files.Count - _maxCount + 1);  // +1 để đảm bảo còn chỗ cho file mới
                        
                    foreach (var file in filesToDelete)
                    {
                        try
                        {
                            file.Delete();
                            _logger.LogInformation("Đã xóa file {FileName} do vượt quá số lượng tối đa", file.Name);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Không thể xóa file {FileName} trong quá trình dọn dẹp", file.Name);
                        }
                    }
                }

                // Kiểm tra kích thước tổng
                long totalSize = files.Sum(f => f.Length);
                if (totalSize >= _maxSizeBytes)
                {
                    _logger.LogWarning("Hàng đợi {QueueDirectory} đã đạt giới hạn kích thước {MaxSizeMB}MB, đang dọn dẹp...", 
                        _queueDirectory, _maxSizeBytes / (1024 * 1024));
                    
                    // Sắp xếp theo thời gian, xóa file cũ nhất cho đến khi kích thước tổng < 80% giới hạn
                    var filesToDelete = files.OrderBy(f => f.CreationTimeUtc).ToList();
                    long sizeToFree = totalSize - (long)(_maxSizeBytes * 0.8);
                    long freedSize = 0;
                    
                    foreach (var file in filesToDelete)
                    {
                        if (freedSize >= sizeToFree)
                            break;
                            
                        try
                        {
                            freedSize += file.Length;
                            file.Delete();
                            _logger.LogInformation("Đã xóa file {FileName} do vượt quá kích thước tối đa", file.Name);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Không thể xóa file {FileName} trong quá trình dọn dẹp", file.Name);
                        }
                    }
                }

                // Xóa file cũ hơn maxAgeHours
                var cutoffTime = _dateTimeProvider.UtcNow.AddHours(-_maxAgeHours);
                var oldFiles = files.Where(f => f.CreationTimeUtc < cutoffTime);
                
                foreach (var file in oldFiles)
                {
                    try
                    {
                        file.Delete();
                        _logger.LogInformation("Đã xóa file {FileName} do quá cũ (> {MaxAgeHours} giờ)", 
                            file.Name, _maxAgeHours);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Không thể xóa file {FileName} quá cũ", file.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi trong quá trình dọn dẹp hàng đợi {QueueDirectory}", _queueDirectory);
            }
            
            await Task.CompletedTask;
        }
    }
}
