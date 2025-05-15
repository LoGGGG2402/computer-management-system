using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CMSAgent.Core
{
    /// <summary>
    /// Base class for worker services, providing error handling and retry mechanisms.
    /// </summary>
    /// <param name="logger">Logger for logging.</param>
    public abstract class WorkerServiceBase(ILogger logger) : BackgroundService
    {
        protected readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private bool _isStopping;

        /// <summary>
        /// Executes the worker service periodically with retry mechanism when errors occur.
        /// </summary>
        /// <param name="stoppingToken">Token to cancel the operation.</param>
        /// <returns>Task representing the service execution process.</returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Service {ServiceName} is starting...", GetType().Name);

            try
            {
                await InitializeAsync(stoppingToken);

                _ = stoppingToken.Register(() =>
                {
                    _isStopping = true;
                    _logger.LogInformation("Service {ServiceName} is stopping...", GetType().Name);
                });

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        await DoWorkAsync(stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        if (_isStopping)
                        {
                            _logger.LogInformation("Task in {ServiceName} was cancelled because the service is stopping", GetType().Name);
                        }
                        else
                        {
                            _logger.LogWarning("Task in {ServiceName} was cancelled while running", GetType().Name);
                        }
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (_isStopping)
                        {
                            _logger.LogWarning(ex, "Error in {ServiceName} while stopping, ignoring", GetType().Name);
                            break;
                        }

                        _logger.LogError(ex, "Unhandled error in service {ServiceName}", GetType().Name);

                        // Wait before retrying to avoid looping too quickly when there are errors
                        try
                        {
                            await Task.Delay(GetRetryDelay(), stoppingToken);
                        }
                        catch (OperationCanceledException)
                        {
                            // Cancelled during retry delay
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Critical error in {ServiceName}, service has stopped", GetType().Name);
                throw;
            }
            finally
            {
                try
                {
                    await CleanupAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during cleanup of {ServiceName}", GetType().Name);
                }

                _logger.LogInformation("Service {ServiceName} has stopped", GetType().Name);
            }
        }

        /// <summary>
        /// Initializes necessary resources before starting the main work execution.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Task representing the initialization process.</returns>
        protected virtual Task InitializeAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Performs the main work of the worker.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Task representing the work execution process.</returns>
        protected abstract Task DoWorkAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Cleans up resources when the service stops.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Task representing the cleanup process.</returns>
        protected virtual Task CleanupAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets the delay time before retrying when an error occurs.
        /// </summary>
        /// <returns>Delay time (milliseconds).</returns>
        protected virtual TimeSpan GetRetryDelay()
        {
            return TimeSpan.FromSeconds(5);
        }
    }
}
