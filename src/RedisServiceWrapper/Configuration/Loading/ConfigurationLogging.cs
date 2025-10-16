using LanguageExt;
using static LanguageExt.Prelude;
using CustomLogger = RedisServiceWrapper.Logging.ILogger;
using CustomLogLevel = RedisServiceWrapper.Logging.LogLevel;
using CustomLogEntry = RedisServiceWrapper.Logging.LogEntry;

namespace RedisServiceWrapper.Configuration.Loading;

/// <summary>
/// Provides comprehensive logging utilities for configuration management.
/// Ensures sensitive data is properly sanitized before logging.
/// </summary>
public static class ConfigurationLogging
{
    /// <summary>
    /// Logs configuration loading events with sanitized data.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="configPath">Path to configuration file</param>
    /// <param name="config">Configuration to log (will be sanitized)</param>
    /// <param name="operation">Operation being performed</param>
    public static Unit LogConfigurationOperation(
        CustomLogger logger,
        string configPath,
        ServiceConfiguration config,
        string operation)
    {
        var sanitizedConfig = SanitizeConfiguration(config);
        var configSummary = GetConfigurationSummary(sanitizedConfig);
        
        logger.LogInfo($"Configuration {operation}: {Path.GetFileName(configPath)}");
        logger.LogDebug($"Configuration summary: {configSummary}");
        
        return unit;
    }

    /// <summary>
    /// Logs configuration change events with detailed analysis.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="change">Configuration change analysis</param>
    /// <param name="configPath">Path to configuration file</param>
    public static Unit LogConfigurationChange(
        CustomLogger logger,
        ConfigurationChange change,
        string configPath)
    {
        var severity = change.ChangeSeverity switch
        {
            ChangeSeverity.Low => "INFO",
            ChangeSeverity.Medium => "WARN",
            ChangeSeverity.High => "WARN",
            ChangeSeverity.Critical => "ERROR",
            _ => "INFO"
        };

        var message = $"Configuration change detected: {severity} | {change.ChangedProperties.Count} changes | {change.Warnings.Count} warnings";
        
        if (change.RequiresRestart)
        {
            logger.LogWarning($"Configuration change requires service restart: {message}");
        }
        else
        {
            logger.LogInfo($"Configuration change is safe: {message}");
        }

        // Log individual changes
        foreach (var propertyChange in change.ChangedProperties)
        {
            logger.LogDebug($"  Changed: {propertyChange.PropertyPath} = '{propertyChange.NewValue}'");
        }

        // Log warnings
        foreach (var warning in change.Warnings)
        {
            var logLevel = warning.Severity switch
            {
                WarningSeverity.Info => CustomLogLevel.Info,
                WarningSeverity.Low => CustomLogLevel.Info,
                WarningSeverity.Medium => CustomLogLevel.Warning,
                WarningSeverity.High => CustomLogLevel.Warning,
                WarningSeverity.Critical => CustomLogLevel.Error,
                _ => CustomLogLevel.Info
            };

            logger.LogInfo(warning.Message);
        }

        return unit;
    }

    /// <summary>
    /// Logs configuration validation results.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="isValid">Whether configuration is valid</param>
    /// <param name="errors">Validation errors (if any)</param>
    /// <param name="configPath">Path to configuration file</param>
    public static Unit LogValidationResult(
        CustomLogger logger,
        bool isValid,
        Seq<string> errors,
        string configPath)
    {
        if (isValid)
        {
            logger.LogSuccess($"Configuration validation passed: {Path.GetFileName(configPath)}");
        }
        else
        {
            logger.LogError($"Configuration validation failed: {Path.GetFileName(configPath)}");
            foreach (var error in errors)
            {
                logger.LogError($"  Validation error: {error}");
            }
        }

        return unit;
    }

    /// <summary>
    /// Logs secret resolution events (without exposing actual secrets).
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="secretType">Type of secret being resolved</param>
    /// <param name="secretName">Name of the secret</param>
    /// <param name="success">Whether resolution was successful</param>
    /// <param name="error">Error message if resolution failed</param>
    public static Unit LogSecretResolution(
        CustomLogger logger,
        string secretType,
        string secretName,
        bool success,
        string? error = null)
    {
        if (success)
        {
            logger.LogDebug($"Secret resolved successfully: {secretType}:{secretName}");
        }
        else
        {
            logger.LogWarning($"Secret resolution failed: {secretType}:{secretName} - {error}");
        }

        return unit;
    }

    /// <summary>
    /// Logs configuration cache operations.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="operation">Cache operation</param>
    /// <param name="configPath">Path to configuration file</param>
    /// <param name="cacheHit">Whether it was a cache hit</param>
    public static Unit LogCacheOperation(
        CustomLogger logger,
        string operation,
        string configPath,
        bool cacheHit)
    {
        var hitMiss = cacheHit ? "HIT" : "MISS";
        logger.LogDebug($"Configuration cache {operation}: {hitMiss} for {Path.GetFileName(configPath)}");
        return unit;
    }

    /// <summary>
    /// Logs configuration file watcher events.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="changeType">Type of file change</param>
    /// <param name="configPath">Path to configuration file</param>
    /// <param name="reason">Reason for the change</param>
    public static Unit LogFileWatcherEvent(
        CustomLogger logger,
        ConfigurationChangeType changeType,
        string configPath,
        string reason)
    {
        var message = $"Configuration file {changeType.ToString().ToLower()}: {Path.GetFileName(configPath)} - {reason}";
        
        switch (changeType)
        {
            case ConfigurationChangeType.Modified:
                logger.LogInfo(message);
                break;
            case ConfigurationChangeType.Created:
                logger.LogInfo(message);
                break;
            case ConfigurationChangeType.Deleted:
                logger.LogWarning(message);
                break;
            case ConfigurationChangeType.Renamed:
                logger.LogInfo(message);
                break;
        }

        return unit;
    }

    /// <summary>
    /// Logs configuration migration events.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="fromVersion">Source version</param>
    /// <param name="toVersion">Target version</param>
    /// <param name="success">Whether migration was successful</param>
    /// <param name="appliedMigrations">List of applied migrations</param>
    public static Unit LogMigrationEvent(
        CustomLogger logger,
        string fromVersion,
        string toVersion,
        bool success,
        Seq<string> appliedMigrations)
    {
        if (success)
        {
            logger.LogSuccess($"Configuration migrated successfully: {fromVersion} -> {toVersion}");
            foreach (var migration in appliedMigrations)
            {
                logger.LogInfo($"  Applied migration: {migration}");
            }
        }
        else
        {
            logger.LogError($"Configuration migration failed: {fromVersion} -> {toVersion}");
        }

        return unit;
    }

    #region Private Helper Methods

    /// <summary>
    /// Sanitizes configuration by removing sensitive data.
    /// </summary>
    private static ServiceConfiguration SanitizeConfiguration(ServiceConfiguration config) =>
        config with
        {
            Redis = config.Redis with
            {
                Password = "[REDACTED]"
            }
            // Add more sanitization as needed
        };

    /// <summary>
    /// Gets a summary of the configuration for logging.
    /// </summary>
    private static string GetConfigurationSummary(ServiceConfiguration config) =>
        $"Backend={config.BackendType}, Redis.Port={config.Redis.Port}, Service={config.Service.ServiceName}";

    #endregion
}

/// <summary>
/// Extension methods for configuration logging.
/// </summary>
public static class ConfigurationLoggingExtensions
{
    /// <summary>
    /// Logs a configuration operation with automatic sanitization.
    /// </summary>
    public static Unit LogConfigOperation(
        this CustomLogger logger,
        string operation,
        string configPath,
        ServiceConfiguration config) =>
        ConfigurationLogging.LogConfigurationOperation(logger, configPath, config, operation);

    /// <summary>
    /// Logs a configuration change with automatic analysis.
    /// </summary>
    public static Unit LogConfigChange(
        this CustomLogger logger,
        ConfigurationChange change,
        string configPath) =>
        ConfigurationLogging.LogConfigurationChange(logger, change, configPath);

    /// <summary>
    /// Logs validation results with automatic formatting.
    /// </summary>
    public static Unit LogValidation(
        this CustomLogger logger,
        bool isValid,
        Seq<string> errors,
        string configPath) =>
        ConfigurationLogging.LogValidationResult(logger, isValid, errors, configPath);

    /// <summary>
    /// Logs secret resolution with automatic sanitization.
    /// </summary>
    public static Unit LogSecret(
        this CustomLogger logger,
        string secretType,
        string secretName,
        bool success,
        string? error = null) =>
        ConfigurationLogging.LogSecretResolution(logger, secretType, secretName, success, error);

    /// <summary>
    /// Logs cache operations with automatic formatting.
    /// </summary>
    public static Unit LogCache(
        this CustomLogger logger,
        string operation,
        string configPath,
        bool cacheHit) =>
        ConfigurationLogging.LogCacheOperation(logger, operation, configPath, cacheHit);

    /// <summary>
    /// Logs file watcher events with automatic formatting.
    /// </summary>
    public static Unit LogFileEvent(
        this CustomLogger logger,
        ConfigurationChangeType changeType,
        string configPath,
        string reason) =>
        ConfigurationLogging.LogFileWatcherEvent(logger, changeType, configPath, reason);

    /// <summary>
    /// Logs migration events with automatic formatting.
    /// </summary>
    public static Unit LogMigration(
        this CustomLogger logger,
        string fromVersion,
        string toVersion,
        bool success,
        Seq<string> appliedMigrations) =>
        ConfigurationLogging.LogMigrationEvent(logger, fromVersion, toVersion, success, appliedMigrations);
}
