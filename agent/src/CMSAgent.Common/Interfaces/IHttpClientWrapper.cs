using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CMSAgent.Common.Interfaces
{
    /// <summary>
    /// Interface cho wrapper của HttpClient để thực hiện các request HTTP.
    /// </summary>
    public interface IHttpClientWrapper
    {
        /// <summary>
        /// Thực hiện HTTP GET request.
        /// </summary>
        /// <typeparam name="TResponse">Kiểu dữ liệu của response body.</typeparam>
        /// <param name="endpoint">Endpoint API cần gọi.</param>
        /// <param name="agentId">ID của agent.</param>
        /// <param name="token">Token xác thực (có thể null).</param>
        /// <param name="queryParams">Các tham số query string (tùy chọn).</param>
        /// <returns>Response được deserialize từ JSON sang kiểu TResponse.</returns>
        Task<TResponse> GetAsync<TResponse>(string endpoint, string agentId, string token, Dictionary<string, string> queryParams = null);

        /// <summary>
        /// Thực hiện HTTP POST request.
        /// </summary>
        /// <param name="endpoint">Endpoint API cần gọi.</param>
        /// <param name="data">Dữ liệu cần gửi lên server.</param>
        /// <param name="agentId">ID của agent.</param>
        /// <param name="token">Token xác thực (có thể null).</param>
        /// <returns>Task đại diện cho request.</returns>
        Task PostAsync(string endpoint, object data, string agentId, string token);

        /// <summary>
        /// Thực hiện HTTP POST request và nhận response.
        /// </summary>
        /// <typeparam name="TRequest">Kiểu dữ liệu của request body.</typeparam>
        /// <typeparam name="TResponse">Kiểu dữ liệu của response body.</typeparam>
        /// <param name="endpoint">Endpoint API cần gọi.</param>
        /// <param name="data">Dữ liệu cần gửi lên server.</param>
        /// <param name="agentId">ID của agent.</param>
        /// <param name="token">Token xác thực (có thể null).</param>
        /// <returns>Response được deserialize từ JSON sang kiểu TResponse.</returns>
        Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest data, string agentId, string token);

        /// <summary>
        /// Tải xuống file từ server.
        /// </summary>
        /// <param name="endpoint">Endpoint API cần gọi.</param>
        /// <param name="agentId">ID của agent.</param>
        /// <param name="token">Token xác thực (có thể null).</param>
        /// <returns>Stream chứa dữ liệu file được tải xuống.</returns>
        Task<Stream> DownloadFileAsync(string endpoint, string agentId, string token);
    }
}
