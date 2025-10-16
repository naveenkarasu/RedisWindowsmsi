using LanguageExt;
using static LanguageExt.Prelude;
using System;
using System.Threading.Tasks;
using RedisServiceWrapper.Configuration;
using RedisServiceWrapper.Logging;
using CustomLogger = RedisServiceWrapper.Logging.ILogger;

namespace RedisServiceWrapper.Backend;

/// <summary>
/// Represents the status of a Redis backend.
/// </summary>
public enum BackendStatus
{
    /// <summary>
    /// Backend is not initialized or has been disposed.
    /// </summary>
    NotInitialized,
    
    /// <summary>
    /// Backend is initializing.
    /// </summary>
    Initializing,
    
    /// <summary>
    /// Backend is running and Redis is accessible.
    /// </summary>
    Running,
    
    /// <summary>
    /// Backend is stopping.
    /// </summary>
    Stopping,
    
    /// <summary>
    /// Backend is stopped.
    /// </summary>
    Stopped,
    
    /// <summary>
    /// Backend encountered an error.
    /// </summary>
    Error,
    
    /// <summary>
    /// Backend is in an unknown state.
    /// </summary>
    Unknown
}

/// <summary>
/// Represents detailed information about the backend status.
/// </summary>
public sealed record BackendStatusInfo(
    BackendStatus Status,
    string Message,
    DateTime Timestamp,
    Option<Exception> LastError = default,
    Option<TimeSpan> Uptime = default,
    Option<int> ProcessId = default,
    Option<string> Version = default
)
{
    /// <summary>
    /// Gets a summary of the backend status.
    /// </summary>
    public string Summary => $"{Status}: {Message}";
    
    /// <summary>
    /// Indicates if the backend is in a healthy state.
    /// </summary>
    public bool IsHealthy => Status == BackendStatus.Running;
    
    /// <summary>
    /// Indicates if the backend is in a transitional state.
    /// </summary>
    public bool IsTransitional => Status is BackendStatus.Initializing or BackendStatus.Stopping;
    
    /// <summary>
    /// Indicates if the backend is in an error state.
    /// </summary>
    public bool IsError => Status == BackendStatus.Error;
}

/// <summary>
/// Represents a backend event that occurred.
/// </summary>
public sealed record BackendEvent(
    string EventType,
    string Message,
    DateTime Timestamp,
    BackendStatus Status,
    Option<Exception> Exception = default,
    Option<object> Data = default
)
{
    /// <summary>
    /// Gets a summary of the backend event.
    /// </summary>
    public string Summary => $"{EventType}: {Message}";
}

/// <summary>
/// Represents the result of a backend operation.
/// </summary>
public sealed record BackendOperationResult(
    bool IsSuccess,
    string Message,
    DateTime Timestamp,
    Option<Exception> Exception = default,
    Option<BackendStatusInfo> StatusInfo = default
)
{
    /// <summary>
    /// Creates a successful operation result.
    /// </summary>
    public static BackendOperationResult Success(string message, BackendStatusInfo? statusInfo = null) =>
        new(true, message, DateTime.UtcNow, Option<Exception>.None, statusInfo ?? Option<BackendStatusInfo>.None);
    
    /// <summary>
    /// Creates a failed operation result.
    /// </summary>
    public static BackendOperationResult Failure(string message, Exception? exception = null, BackendStatusInfo? statusInfo = null) =>
        new(false, message, DateTime.UtcNow, exception ?? Option<Exception>.None, statusInfo ?? Option<BackendStatusInfo>.None);
    
    /// <summary>
    /// Gets a summary of the operation result.
    /// </summary>
    public string Summary => IsSuccess ? $"Success: {Message}" : $"Failure: {Message}";
}

/// <summary>
/// Interface for managing Redis backends (WSL2 or Docker).
/// Provides a unified interface for starting, stopping, and monitoring Redis instances.
/// </summary>
public interface IRedisBackend : IDisposable
{
    /// <summary>
    /// Gets the current status of the backend.
    /// </summary>
    BackendStatusInfo CurrentStatus { get; }
    
    /// <summary>
    /// Gets the backend type (WSL2 or Docker).
    /// </summary>
    string BackendType { get; }
    
    /// <summary>
    /// Gets the configuration used by this backend.
    /// </summary>
    ServiceConfiguration Configuration { get; }
    
    /// <summary>
    /// Event fired when the backend status changes.
    /// </summary>
    event EventHandler<BackendEvent>? StatusChanged;
    
    /// <summary>
    /// Event fired when a backend operation completes.
    /// </summary>
    event EventHandler<BackendOperationResult>? OperationCompleted;
    
    /// <summary>
    /// Initializes the backend with the given configuration.
    /// </summary>
    /// <param name="configuration">The service configuration</param>
    /// <param name="logger">The logger instance</param>
    /// <returns>A TryAsync containing the operation result</returns>
    TryAsync<BackendOperationResult> InitializeAsync(ServiceConfiguration configuration, CustomLogger logger);
    
    /// <summary>
    /// Starts the Redis backend.
    /// </summary>
    /// <returns>A TryAsync containing the operation result</returns>
    TryAsync<BackendOperationResult> StartAsync();
    
    /// <summary>
    /// Stops the Redis backend gracefully.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for graceful shutdown</param>
    /// <returns>A TryAsync containing the operation result</returns>
    TryAsync<BackendOperationResult> StopAsync(TimeSpan? timeout = null);
    
    /// <summary>
    /// Restarts the Redis backend.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for graceful shutdown before restart</param>
    /// <returns>A TryAsync containing the operation result</returns>
    TryAsync<BackendOperationResult> RestartAsync(TimeSpan? timeout = null);
    
    /// <summary>
    /// Checks if the Redis backend is currently running.
    /// </summary>
    /// <returns>A TryAsync containing true if running, false otherwise</returns>
    TryAsync<bool> IsRunningAsync();
    
    /// <summary>
    /// Gets detailed status information about the backend.
    /// </summary>
    /// <returns>A TryAsync containing the status information</returns>
    TryAsync<BackendStatusInfo> GetStatusAsync();
    
    /// <summary>
    /// Performs a health check on the Redis instance.
    /// </summary>
    /// <returns>A TryAsync containing the health check result</returns>
    TryAsync<BackendOperationResult> HealthCheckAsync();
    
    /// <summary>
    /// Gets the Redis connection string for this backend.
    /// </summary>
    /// <returns>A TryAsync containing the connection string</returns>
    TryAsync<string> GetConnectionStringAsync();
    
    /// <summary>
    /// Gets the Redis port for this backend.
    /// </summary>
    /// <returns>A TryAsync containing the port number</returns>
    TryAsync<int> GetPortAsync();
    
    /// <summary>
    /// Gets the Redis host/address for this backend.
    /// </summary>
    /// <returns>A TryAsync containing the host address</returns>
    TryAsync<string> GetHostAsync();
    
    /// <summary>
    /// Validates that the backend configuration is correct and the environment is ready.
    /// </summary>
    /// <returns>A TryAsync containing the validation result</returns>
    TryAsync<BackendOperationResult> ValidateConfigurationAsync();
    
    /// <summary>
    /// Gets logs from the Redis backend.
    /// </summary>
    /// <param name="lines">Number of log lines to retrieve (optional)</param>
    /// <returns>A TryAsync containing the log content</returns>
    TryAsync<string> GetLogsAsync(int? lines = null);
    
    /// <summary>
    /// Clears logs from the Redis backend.
    /// </summary>
    /// <returns>A TryAsync containing the operation result</returns>
    TryAsync<BackendOperationResult> ClearLogsAsync();
    
    /// <summary>
    /// Gets resource usage information for the backend.
    /// </summary>
    /// <returns>A TryAsync containing resource usage information</returns>
    TryAsync<BackendResourceUsage> GetResourceUsageAsync();
    
    /// <summary>
    /// Updates the backend configuration (requires restart if critical changes).
    /// </summary>
    /// <param name="newConfiguration">The new configuration</param>
    /// <returns>A TryAsync containing the operation result</returns>
    TryAsync<BackendOperationResult> UpdateConfigurationAsync(ServiceConfiguration newConfiguration);
}

/// <summary>
/// Represents resource usage information for a backend.
/// </summary>
public sealed record BackendResourceUsage(
    Option<long> MemoryUsageBytes,
    Option<double> CpuUsagePercent,
    Option<long> DiskUsageBytes,
    Option<int> ProcessCount,
    DateTime Timestamp
)
{
    /// <summary>
    /// Gets memory usage in MB.
    /// </summary>
    public Option<double> MemoryUsageMB => MemoryUsageBytes.Map(bytes => bytes / (1024.0 * 1024.0));
    
    /// <summary>
    /// Gets disk usage in MB.
    /// </summary>
    public Option<double> DiskUsageMB => DiskUsageBytes.Map(bytes => bytes / (1024.0 * 1024.0));
    
    /// <summary>
    /// Gets a summary of resource usage.
    /// </summary>
    public string Summary
    {
        get
        {
            var parts = new List<string>();
            
            MemoryUsageMB.IfSome(mb => parts.Add($"Memory: {mb:F1} MB"));
            CpuUsagePercent.IfSome(cpu => parts.Add($"CPU: {cpu:F1}%"));
            DiskUsageMB.IfSome(disk => parts.Add($"Disk: {disk:F1} MB"));
            ProcessCount.IfSome(count => parts.Add($"Processes: {count}"));
            
            return parts.Count == 0 ? "No resource data available" : string.Join(", ", parts);
        }
    }
}
