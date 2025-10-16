using LanguageExt;
using static LanguageExt.Prelude;
using System;
using System.Collections.Generic;
using RedisServiceWrapper.Configuration;
using RedisServiceWrapper.Logging;
using CustomLogger = RedisServiceWrapper.Logging.ILogger;

namespace RedisServiceWrapper.Backend;

/// <summary>
/// Represents information about an available backend.
/// </summary>
public sealed record BackendInfo(
    string BackendType,
    string DisplayName,
    string Description,
    bool IsAvailable,
    Option<string> AvailabilityMessage = default,
    Option<string> Version = default,
    Option<string> RequiredVersion = default
)
{
    /// <summary>
    /// Gets a summary of the backend information.
    /// </summary>
    public string Summary => $"{DisplayName} ({BackendType}): {(IsAvailable ? "Available" : "Unavailable")}";
}

/// <summary>
/// Represents the result of backend creation.
/// </summary>
public sealed record BackendCreationResult(
    bool IsSuccess,
    Option<IRedisBackend> Backend,
    string Message,
    DateTime Timestamp,
    Option<Exception> Exception = default,
    Option<BackendInfo> BackendInfo = default
)
{
    /// <summary>
    /// Creates a successful creation result.
    /// </summary>
    public static BackendCreationResult Success(IRedisBackend backend, string message, BackendInfo? backendInfo = null) =>
        new(true, Option<IRedisBackend>.Some(backend), message, DateTime.UtcNow, Option<Exception>.None, backendInfo ?? Option<BackendInfo>.None);
    
    /// <summary>
    /// Creates a failed creation result.
    /// </summary>
    public static BackendCreationResult Failure(string message, Exception? exception = null, BackendInfo? backendInfo = null) =>
        new(false, Option<IRedisBackend>.None, message, DateTime.UtcNow, exception ?? Option<Exception>.None, backendInfo ?? Option<BackendInfo>.None);
    
    /// <summary>
    /// Gets a summary of the creation result.
    /// </summary>
    public string Summary => IsSuccess ? $"Success: {Message}" : $"Failure: {Message}";
}

/// <summary>
/// Factory interface for creating Redis backend instances.
/// Provides backend discovery, validation, and creation capabilities.
/// </summary>
public interface IBackendFactory : IDisposable
{
    /// <summary>
    /// Gets all available backend types.
    /// </summary>
    /// <returns>A TryAsync containing the list of available backends</returns>
    TryAsync<Seq<BackendInfo>> GetAvailableBackendsAsync();
    
    /// <summary>
    /// Gets information about a specific backend type.
    /// </summary>
    /// <param name="backendType">The backend type to get information about</param>
    /// <returns>A TryAsync containing the backend information</returns>
    TryAsync<BackendInfo> GetBackendInfoAsync(string backendType);
    
    /// <summary>
    /// Checks if a specific backend type is available.
    /// </summary>
    /// <param name="backendType">The backend type to check</param>
    /// <returns>A TryAsync containing true if available, false otherwise</returns>
    TryAsync<bool> IsBackendAvailableAsync(string backendType);
    
    /// <summary>
    /// Creates a Redis backend instance based on the configuration.
    /// </summary>
    /// <param name="configuration">The service configuration</param>
    /// <param name="logger">The logger instance</param>
    /// <returns>A TryAsync containing the backend creation result</returns>
    TryAsync<BackendCreationResult> CreateBackendAsync(ServiceConfiguration configuration, CustomLogger logger);
    
    /// <summary>
    /// Creates a Redis backend instance of a specific type.
    /// </summary>
    /// <param name="backendType">The backend type to create</param>
    /// <param name="configuration">The service configuration</param>
    /// <param name="logger">The logger instance</param>
    /// <returns>A TryAsync containing the backend creation result</returns>
    TryAsync<BackendCreationResult> CreateBackendAsync(string backendType, ServiceConfiguration configuration, CustomLogger logger);
    
    /// <summary>
    /// Validates that a backend can be created with the given configuration.
    /// </summary>
    /// <param name="configuration">The service configuration to validate</param>
    /// <returns>A TryAsync containing the validation result</returns>
    TryAsync<BackendOperationResult> ValidateBackendConfigurationAsync(ServiceConfiguration configuration);
    
    /// <summary>
    /// Gets the recommended backend type for the current system.
    /// </summary>
    /// <returns>A TryAsync containing the recommended backend type</returns>
    TryAsync<Option<string>> GetRecommendedBackendTypeAsync();
    
    /// <summary>
    /// Gets the fallback backend type if the primary backend is not available.
    /// </summary>
    /// <param name="primaryBackendType">The primary backend type</param>
    /// <returns>A TryAsync containing the fallback backend type</returns>
    TryAsync<Option<string>> GetFallbackBackendTypeAsync(string primaryBackendType);
    
    /// <summary>
    /// Registers a custom backend type.
    /// </summary>
    /// <param name="backendType">The backend type identifier</param>
    /// <param name="backendInfo">Information about the backend</param>
    /// <param name="creator">Function to create the backend instance</param>
    /// <returns>A TryAsync containing the registration result</returns>
    TryAsync<BackendOperationResult> RegisterBackendTypeAsync(
        string backendType, 
        BackendInfo backendInfo, 
        Func<ServiceConfiguration, CustomLogger, TryAsync<BackendCreationResult>> creator);
    
    /// <summary>
    /// Unregisters a custom backend type.
    /// </summary>
    /// <param name="backendType">The backend type to unregister</param>
    /// <returns>A TryAsync containing the unregistration result</returns>
    TryAsync<BackendOperationResult> UnregisterBackendTypeAsync(string backendType);
    
    /// <summary>
    /// Gets the factory statistics.
    /// </summary>
    /// <returns>A TryAsync containing the factory statistics</returns>
    TryAsync<BackendFactoryStatistics> GetStatisticsAsync();
}

/// <summary>
/// Represents statistics about the backend factory.
/// </summary>
public sealed record BackendFactoryStatistics(
    int TotalBackendTypes,
    int AvailableBackendTypes,
    int UnavailableBackendTypes,
    int CustomBackendTypes,
    DateTime LastUpdated,
    Option<TimeSpan> AverageCreationTime = default,
    Option<int> TotalCreations = default,
    Option<int> SuccessfulCreations = default,
    Option<int> FailedCreations = default
)
{
    /// <summary>
    /// Gets the success rate of backend creation.
    /// </summary>
    public Option<double> SuccessRate => 
        TotalCreations.Bind(total => 
            SuccessfulCreations.Map(successful => 
                total > 0 ? (double)successful / total : 0.0));
    
    /// <summary>
    /// Gets a summary of the factory statistics.
    /// </summary>
    public string Summary
    {
        get
        {
            var parts = new List<string>
            {
                $"Total: {TotalBackendTypes}",
                $"Available: {AvailableBackendTypes}",
                $"Unavailable: {UnavailableBackendTypes}"
            };
            
            if (CustomBackendTypes > 0)
                parts.Add($"Custom: {CustomBackendTypes}");
            
            SuccessRate.IfSome(rate => parts.Add($"Success Rate: {rate:P1}"));
            AverageCreationTime.IfSome(time => parts.Add($"Avg Creation: {time.TotalMilliseconds:F0}ms"));
            
            return string.Join(", ", parts);
        }
    }
}

/// <summary>
/// Represents the result of backend discovery.
/// </summary>
public sealed record BackendDiscoveryResult(
    bool IsSuccess,
    Seq<BackendInfo> AvailableBackends,
    Seq<BackendInfo> UnavailableBackends,
    string Message,
    DateTime Timestamp,
    Option<Exception> Exception = default
)
{
    /// <summary>
    /// Creates a successful discovery result.
    /// </summary>
    public static BackendDiscoveryResult Success(Seq<BackendInfo> availableBackends, Seq<BackendInfo> unavailableBackends, string message) =>
        new(true, availableBackends, unavailableBackends, message, DateTime.UtcNow, Option<Exception>.None);
    
    /// <summary>
    /// Creates a failed discovery result.
    /// </summary>
    public static BackendDiscoveryResult Failure(string message, Exception? exception = null) =>
        new(false, Seq<BackendInfo>(), Seq<BackendInfo>(), message, DateTime.UtcNow, exception ?? Option<Exception>.None);
    
    /// <summary>
    /// Gets a summary of the discovery result.
    /// </summary>
    public string Summary => $"{Message} - Available: {AvailableBackends.Count}, Unavailable: {UnavailableBackends.Count}";
}
