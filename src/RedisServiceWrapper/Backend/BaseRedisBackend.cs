using LanguageExt;
using static LanguageExt.Prelude;
using System;
using System.Threading.Tasks;
using RedisServiceWrapper.Configuration;
using RedisServiceWrapper.Logging;
using CustomLogger = RedisServiceWrapper.Logging.ILogger;

namespace RedisServiceWrapper.Backend;

/// <summary>
/// Base class for Redis backend implementations.
/// Provides common functionality and default implementations for backend operations.
/// </summary>
public abstract class BaseRedisBackend : IRedisBackend
{
    private readonly object _statusLock = new object();
    private BackendStatusInfo _currentStatus;
    private bool _disposed = false;

    /// <summary>
    /// Gets the current status of the backend.
    /// </summary>
    public BackendStatusInfo CurrentStatus
    {
        get
        {
            lock (_statusLock)
            {
                return _currentStatus;
            }
        }
        protected set
        {
            lock (_statusLock)
            {
                var oldStatus = _currentStatus;
                _currentStatus = value;
                
                if (oldStatus.Status != value.Status)
                {
                    OnStatusChanged(new BackendEvent(
                        "StatusChanged",
                        $"Status changed from {oldStatus.Status} to {value.Status}",
                        DateTime.UtcNow,
                        value.Status
                    ));
                }
            }
        }
    }

    /// <summary>
    /// Gets the backend type (WSL2 or Docker).
    /// </summary>
    public abstract string BackendType { get; }

    /// <summary>
    /// Gets the configuration used by this backend.
    /// </summary>
    public ServiceConfiguration Configuration { get; private set; } = null!;

    /// <summary>
    /// Gets the logger instance.
    /// </summary>
    protected CustomLogger Logger { get; private set; } = null!;

    /// <summary>
    /// Event fired when the backend status changes.
    /// </summary>
    public event EventHandler<BackendEvent>? StatusChanged;

    /// <summary>
    /// Event fired when a backend operation completes.
    /// </summary>
    public event EventHandler<BackendOperationResult>? OperationCompleted;

    /// <summary>
    /// Initializes the backend with the given configuration.
    /// </summary>
    /// <param name="configuration">The service configuration</param>
    /// <param name="logger">The logger instance</param>
    /// <returns>A TryAsync containing the operation result</returns>
    public virtual TryAsync<BackendOperationResult> InitializeAsync(ServiceConfiguration configuration, CustomLogger logger)
    {
        return TryAsync(async () =>
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BaseRedisBackend));

            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));

            Logger.LogInfo($"Initializing {BackendType} backend");
            
            SetStatus(BackendStatus.Initializing, "Initializing backend");

            try
            {
                await OnInitializeAsync();
                
                var result = BackendOperationResult.Success($"Successfully initialized {BackendType} backend");
                OnOperationCompleted(result);
                
                Logger.LogSuccess($"Successfully initialized {BackendType} backend");
                return result;
            }
            catch (Exception ex)
            {
                var result = BackendOperationResult.Failure($"Failed to initialize {BackendType} backend: {ex.Message}", ex);
                OnOperationCompleted(result);
                
                Logger.LogError($"Failed to initialize {BackendType} backend: {ex.Message}", ex);
                throw;
            }
        });
    }

    /// <summary>
    /// Starts the Redis backend.
    /// </summary>
    /// <returns>A TryAsync containing the operation result</returns>
    public virtual TryAsync<BackendOperationResult> StartAsync()
    {
        return TryAsync(async () =>
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BaseRedisBackend));

            if (CurrentStatus.Status == BackendStatus.Running)
            {
                var result = BackendOperationResult.Success($"{BackendType} backend is already running");
                OnOperationCompleted(result);
                return result;
            }

            Logger.LogInfo($"Starting {BackendType} backend");
            
            try
            {
                await OnStartAsync();
                
                var result = BackendOperationResult.Success($"Successfully started {BackendType} backend");
                OnOperationCompleted(result);
                
                Logger.LogSuccess($"Successfully started {BackendType} backend");
                return result;
            }
            catch (Exception ex)
            {
                var result = BackendOperationResult.Failure($"Failed to start {BackendType} backend: {ex.Message}", ex);
                OnOperationCompleted(result);
                
                Logger.LogError($"Failed to start {BackendType} backend: {ex.Message}", ex);
                throw;
            }
        });
    }

    /// <summary>
    /// Stops the Redis backend gracefully.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for graceful shutdown</param>
    /// <returns>A TryAsync containing the operation result</returns>
    public virtual TryAsync<BackendOperationResult> StopAsync(TimeSpan? timeout = null)
    {
        return TryAsync(async () =>
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BaseRedisBackend));

            if (CurrentStatus.Status == BackendStatus.Stopped)
            {
                var result = BackendOperationResult.Success($"{BackendType} backend is already stopped");
                OnOperationCompleted(result);
                return result;
            }

            Logger.LogInfo($"Stopping {BackendType} backend");
            SetStatus(BackendStatus.Stopping, "Stopping backend");
            
            try
            {
                await OnStopAsync(timeout);
                
                var result = BackendOperationResult.Success($"Successfully stopped {BackendType} backend");
                OnOperationCompleted(result);
                
                Logger.LogSuccess($"Successfully stopped {BackendType} backend");
                return result;
            }
            catch (Exception ex)
            {
                var result = BackendOperationResult.Failure($"Failed to stop {BackendType} backend: {ex.Message}", ex);
                OnOperationCompleted(result);
                
                Logger.LogError($"Failed to stop {BackendType} backend: {ex.Message}", ex);
                throw;
            }
        });
    }

    /// <summary>
    /// Restarts the Redis backend.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for graceful shutdown before restart</param>
    /// <returns>A TryAsync containing the operation result</returns>
    public virtual TryAsync<BackendOperationResult> RestartAsync(TimeSpan? timeout = null)
    {
        return TryAsync(async () =>
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BaseRedisBackend));

            Logger.LogInfo($"Restarting {BackendType} backend");
            
            try
            {
                // Stop the backend
                var stopResult = await StopAsync(timeout);
                var stopSuccess = stopResult.Match(
                    success => success,
                    failure => throw new Exception($"Failed to stop {BackendType} backend during restart: {failure.Message}")
                );
                
                if (!stopSuccess.IsSuccess)
                {
                    var stopFailureResult = BackendOperationResult.Failure($"Failed to stop {BackendType} backend during restart: {stopSuccess.Message}");
                    OnOperationCompleted(stopFailureResult);
                    return stopFailureResult;
                }

                // Start the backend
                var startResult = await StartAsync();
                var startSuccess = startResult.Match(
                    success => success,
                    failure => throw new Exception($"Failed to start {BackendType} backend during restart: {failure.Message}")
                );
                
                if (!startSuccess.IsSuccess)
                {
                    var startFailureResult = BackendOperationResult.Failure($"Failed to start {BackendType} backend during restart: {startSuccess.Message}");
                    OnOperationCompleted(startFailureResult);
                    return startFailureResult;
                }

                var result = BackendOperationResult.Success($"Successfully restarted {BackendType} backend");
                OnOperationCompleted(result);
                
                Logger.LogSuccess($"Successfully restarted {BackendType} backend");
                return result;
            }
            catch (Exception ex)
            {
                var result = BackendOperationResult.Failure($"Failed to restart {BackendType} backend: {ex.Message}", ex);
                OnOperationCompleted(result);
                
                Logger.LogError($"Failed to restart {BackendType} backend: {ex.Message}", ex);
                throw;
            }
        });
    }

    /// <summary>
    /// Checks if the Redis backend is currently running.
    /// </summary>
    /// <returns>A TryAsync containing true if running, false otherwise</returns>
    public virtual TryAsync<bool> IsRunningAsync()
    {
        return TryAsync(async () =>
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BaseRedisBackend));

            return await OnIsRunningAsync();
        });
    }

    /// <summary>
    /// Gets detailed status information about the backend.
    /// </summary>
    /// <returns>A TryAsync containing the status information</returns>
    public virtual TryAsync<BackendStatusInfo> GetStatusAsync()
    {
        return TryAsync(async () =>
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BaseRedisBackend));

            return await OnGetStatusAsync();
        });
    }

    /// <summary>
    /// Performs a health check on the Redis instance.
    /// </summary>
    /// <returns>A TryAsync containing the health check result</returns>
    public virtual TryAsync<BackendOperationResult> HealthCheckAsync()
    {
        return TryAsync(async () =>
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BaseRedisBackend));

            try
            {
                var isRunningResult = await IsRunningAsync();
                var isRunning = isRunningResult.Match(
                    success => success,
                    failure => throw new Exception($"Failed to check if {BackendType} backend is running: {failure.Message}")
                );
                
                if (!isRunning)
                {
                    var result = BackendOperationResult.Failure($"{BackendType} backend is not running");
                    OnOperationCompleted(result);
                    return result;
                }

                var healthResult = await OnHealthCheckAsync();
                OnOperationCompleted(healthResult);
                return healthResult;
            }
            catch (Exception ex)
            {
                var result = BackendOperationResult.Failure($"Health check failed for {BackendType} backend: {ex.Message}", ex);
                OnOperationCompleted(result);
                
                Logger.LogError($"Health check failed for {BackendType} backend: {ex.Message}", ex);
                throw;
            }
        });
    }

    /// <summary>
    /// Gets the Redis connection string for this backend.
    /// </summary>
    /// <returns>A TryAsync containing the connection string</returns>
    public virtual TryAsync<string> GetConnectionStringAsync()
    {
        return TryAsync(async () =>
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BaseRedisBackend));

            return await OnGetConnectionStringAsync();
        });
    }

    /// <summary>
    /// Gets the Redis port for this backend.
    /// </summary>
    /// <returns>A TryAsync containing the port number</returns>
    public virtual TryAsync<int> GetPortAsync()
    {
        return TryAsync(async () =>
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BaseRedisBackend));

            return await OnGetPortAsync();
        });
    }

    /// <summary>
    /// Gets the Redis host/address for this backend.
    /// </summary>
    /// <returns>A TryAsync containing the host address</returns>
    public virtual TryAsync<string> GetHostAsync()
    {
        return TryAsync(async () =>
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BaseRedisBackend));

            return await OnGetHostAsync();
        });
    }

    /// <summary>
    /// Validates that the backend configuration is correct and the environment is ready.
    /// </summary>
    /// <returns>A TryAsync containing the validation result</returns>
    public virtual TryAsync<BackendOperationResult> ValidateConfigurationAsync()
    {
        return TryAsync(async () =>
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BaseRedisBackend));

            try
            {
                var validationResult = await OnValidateConfigurationAsync();
                OnOperationCompleted(validationResult);
                return validationResult;
            }
            catch (Exception ex)
            {
                var result = BackendOperationResult.Failure($"Configuration validation failed for {BackendType} backend: {ex.Message}", ex);
                OnOperationCompleted(result);
                
                Logger.LogError($"Configuration validation failed for {BackendType} backend: {ex.Message}", ex);
                throw;
            }
        });
    }

    /// <summary>
    /// Gets logs from the Redis backend.
    /// </summary>
    /// <param name="lines">Number of log lines to retrieve (optional)</param>
    /// <returns>A TryAsync containing the log content</returns>
    public virtual TryAsync<string> GetLogsAsync(int? lines = null)
    {
        return TryAsync(async () =>
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BaseRedisBackend));

            return await OnGetLogsAsync(lines);
        });
    }

    /// <summary>
    /// Clears logs from the Redis backend.
    /// </summary>
    /// <returns>A TryAsync containing the operation result</returns>
    public virtual TryAsync<BackendOperationResult> ClearLogsAsync()
    {
        return TryAsync(async () =>
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BaseRedisBackend));

            try
            {
                var result = await OnClearLogsAsync();
                OnOperationCompleted(result);
                return result;
            }
            catch (Exception ex)
            {
                var result = BackendOperationResult.Failure($"Failed to clear logs for {BackendType} backend: {ex.Message}", ex);
                OnOperationCompleted(result);
                
                Logger.LogError($"Failed to clear logs for {BackendType} backend: {ex.Message}", ex);
                throw;
            }
        });
    }

    /// <summary>
    /// Gets resource usage information for the backend.
    /// </summary>
    /// <returns>A TryAsync containing resource usage information</returns>
    public virtual TryAsync<BackendResourceUsage> GetResourceUsageAsync()
    {
        return TryAsync(async () =>
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BaseRedisBackend));

            return await OnGetResourceUsageAsync();
        });
    }

    /// <summary>
    /// Updates the backend configuration (requires restart if critical changes).
    /// </summary>
    /// <param name="newConfiguration">The new configuration</param>
    /// <returns>A TryAsync containing the operation result</returns>
    public virtual TryAsync<BackendOperationResult> UpdateConfigurationAsync(ServiceConfiguration newConfiguration)
    {
        return TryAsync(async () =>
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BaseRedisBackend));

            try
            {
                var result = await OnUpdateConfigurationAsync(newConfiguration);
                OnOperationCompleted(result);
                return result;
            }
            catch (Exception ex)
            {
                var result = BackendOperationResult.Failure($"Failed to update configuration for {BackendType} backend: {ex.Message}", ex);
                OnOperationCompleted(result);
                
                Logger.LogError($"Failed to update configuration for {BackendType} backend: {ex.Message}", ex);
                throw;
            }
        });
    }

    /// <summary>
    /// Disposes the backend and releases resources.
    /// </summary>
    public virtual void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            OnDispose();
            SetStatus(BackendStatus.Stopped, "Backend disposed");
        }
        catch (Exception ex)
        {
            Logger?.LogError($"Error during backend disposal: {ex.Message}", ex);
        }
        finally
        {
            _disposed = true;
        }
    }

    #region Protected Methods

    /// <summary>
    /// Sets the backend status and fires status change events.
    /// </summary>
    /// <param name="status">The new status</param>
    /// <param name="message">The status message</param>
    /// <param name="exception">Optional exception if status is error</param>
    protected void SetStatus(BackendStatus status, string message, Exception? exception = null)
    {
        CurrentStatus = new BackendStatusInfo(
            status,
            message,
            DateTime.UtcNow,
            exception ?? Option<Exception>.None
        );
    }

    /// <summary>
    /// Fires the status changed event.
    /// </summary>
    /// <param name="backendEvent">The backend event</param>
    protected virtual void OnStatusChanged(BackendEvent backendEvent)
    {
        StatusChanged?.Invoke(this, backendEvent);
    }

    /// <summary>
    /// Fires the operation completed event.
    /// </summary>
    /// <param name="result">The operation result</param>
    protected virtual void OnOperationCompleted(BackendOperationResult result)
    {
        OperationCompleted?.Invoke(this, result);
    }

    #endregion

    #region Abstract Methods

    /// <summary>
    /// Called when the backend is being initialized.
    /// </summary>
    /// <returns>A Task representing the initialization operation</returns>
    protected abstract Task OnInitializeAsync();

    /// <summary>
    /// Called when the backend is being started.
    /// </summary>
    /// <returns>A Task representing the start operation</returns>
    protected abstract Task OnStartAsync();

    /// <summary>
    /// Called when the backend is being stopped.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for graceful shutdown</param>
    /// <returns>A Task representing the stop operation</returns>
    protected abstract Task OnStopAsync(TimeSpan? timeout);

    /// <summary>
    /// Called to check if the backend is running.
    /// </summary>
    /// <returns>A Task containing true if running, false otherwise</returns>
    protected abstract Task<bool> OnIsRunningAsync();

    /// <summary>
    /// Called to get detailed status information.
    /// </summary>
    /// <returns>A Task containing the status information</returns>
    protected abstract Task<BackendStatusInfo> OnGetStatusAsync();

    /// <summary>
    /// Called to perform a health check.
    /// </summary>
    /// <returns>A Task containing the health check result</returns>
    protected abstract Task<BackendOperationResult> OnHealthCheckAsync();

    /// <summary>
    /// Called to get the Redis connection string.
    /// </summary>
    /// <returns>A Task containing the connection string</returns>
    protected abstract Task<string> OnGetConnectionStringAsync();

    /// <summary>
    /// Called to get the Redis port.
    /// </summary>
    /// <returns>A Task containing the port number</returns>
    protected abstract Task<int> OnGetPortAsync();

    /// <summary>
    /// Called to get the Redis host.
    /// </summary>
    /// <returns>A Task containing the host address</returns>
    protected abstract Task<string> OnGetHostAsync();

    /// <summary>
    /// Called to validate the backend configuration.
    /// </summary>
    /// <returns>A Task containing the validation result</returns>
    protected abstract Task<BackendOperationResult> OnValidateConfigurationAsync();

    /// <summary>
    /// Called to get logs from the backend.
    /// </summary>
    /// <param name="lines">Number of log lines to retrieve</param>
    /// <returns>A Task containing the log content</returns>
    protected abstract Task<string> OnGetLogsAsync(int? lines);

    /// <summary>
    /// Called to clear logs from the backend.
    /// </summary>
    /// <returns>A Task containing the operation result</returns>
    protected abstract Task<BackendOperationResult> OnClearLogsAsync();

    /// <summary>
    /// Called to get resource usage information.
    /// </summary>
    /// <returns>A Task containing resource usage information</returns>
    protected abstract Task<BackendResourceUsage> OnGetResourceUsageAsync();

    /// <summary>
    /// Called to update the backend configuration.
    /// </summary>
    /// <param name="newConfiguration">The new configuration</param>
    /// <returns>A Task containing the operation result</returns>
    protected abstract Task<BackendOperationResult> OnUpdateConfigurationAsync(ServiceConfiguration newConfiguration);

    /// <summary>
    /// Called when the backend is being disposed.
    /// </summary>
    protected abstract void OnDispose();

    #endregion
}
