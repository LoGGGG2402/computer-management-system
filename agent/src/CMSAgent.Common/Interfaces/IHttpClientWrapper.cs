using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CMSAgent.Common.Interfaces
{
    /// <summary>
    /// Interface for HttpClient wrapper to perform HTTP requests.
    /// </summary>
    public interface IHttpClientWrapper
    {
        /// <summary>
        /// Performs HTTP GET request.
        /// </summary>
        /// <typeparam name="TResponse">Response body data type.</typeparam>
        /// <param name="endpoint">API endpoint to call.</param>
        /// <param name="agentId">Agent ID.</param>
        /// <param name="token">Authentication token (can be null).</param>
        /// <param name="queryParams">Query string parameters (optional).</param>
        /// <returns>Response deserialized from JSON to TResponse type.</returns>
        Task<TResponse> GetAsync<TResponse>(string endpoint, string agentId, string? token, Dictionary<string, string>? queryParams = null);

        /// <summary>
        /// Performs HTTP POST request and receives response.
        /// </summary>
        /// <typeparam name="TRequest">Request body data type.</typeparam>
        /// <typeparam name="TResponse">Response body data type.</typeparam>
        /// <param name="endpoint">API endpoint to call.</param>
        /// <param name="data">Data to send to server.</param>
        /// <param name="agentId">Agent ID.</param>
        /// <param name="token">Authentication token (can be null).</param>
        /// <returns>Response deserialized from JSON to TResponse type.</returns>
        Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest data, string agentId, string? token);

        /// <summary>
        /// Downloads file from server.
        /// </summary>
        /// <param name="endpoint">API endpoint to call.</param>
        /// <param name="agentId">Agent ID.</param>
        /// <param name="token">Authentication token (can be null).</param>
        /// <returns>Stream containing downloaded file data.</returns>
        Task<Stream> DownloadFileAsync(string endpoint, string agentId, string? token);
    }
}
