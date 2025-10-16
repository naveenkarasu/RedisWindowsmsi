using LanguageExt;
using static LanguageExt.Prelude;
using RedisServiceWrapper.Configuration.Loading;
using RedisServiceWrapper.Configuration.Validation;
using RedisServiceWrapper.Logging;
using System;
using System.Threading.Tasks;
using CustomLogger = RedisServiceWrapper.Logging.ILogger;
using CustomConfigurationManager = RedisServiceWrapper.Configuration.Loading.ConfigurationManager;

namespace RedisServiceWrapper.Configuration;

/// <summary>
/// High-level configuration service that provides a clean API for configuration management.
/// Integrates configuration loading, validation, caching, and change monitoring.
/// </summary>
public sealed class ConfigurationService : IDisposable
{
    private readonly CustomLogger _logger;
    private readonly ConfigurationFactory _factory;
    private readonly CustomConfigurationManager _manager;
    private readonly ConfigurationWatcher? _watcher;
    private readonly ConfigurationChangeAnalyzer _changeAnalyzer;
    private bool _disposed = false;

    /// <summary>
    /// Event fired when configuration changes are detected.
    /// </summary>
    public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

    /// <summary>
    /// Event fired when configuration validation fails.
    /// </summary>
    public event EventHandler<ConfigurationValidationFailedEventArgs>? ConfigurationValidationFailed;

    /// <summary>
    /// Initializes a new instance of the ConfigurationService.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="configPath">Configuration file path (optional)</param>
    /// <param name="enableFileWatching">Whether to enable file change monitoring</param>
    public ConfigurationService(
        CustomLogger logger, 
        string? configPath = null, 
        bool enableFileWatching = true)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _factory = new ConfigurationFactory(_logger, configPath);
        _manager = _factory.CreateConfigurationManager();
        _changeAnalyzer = new ConfigurationChangeAnalyzer(_logger);

        if (enableFileWatching)
        {
            _watcher = new ConfigurationWatcher(configPath ?? Constants.BackendConfigPath, _logger);
            _watcher.ConfigurationChanges.Subscribe(OnConfigurationFileChanged);
        }

        _logger.LogInfo("ConfigurationService initialized successfully.");
    }

    /// <summary>
    /// Gets the current configuration (cached or loaded from file).
    /// </summary>
    /// <returns>TryAsync containing the current configuration</returns>
    public TryAsync<ServiceConfiguration> GetCurrentConfiguration() =>
        TryAsync(async () =>
        {
            var cachedConfig = _manager.GetCachedConfiguration();
            if (cachedConfig.IsSome)
            {
                _logger.LogInfo("Returning cached configuration.");
                return cachedConfig.IfNone(() => throw new InvalidOperationException("Cached configuration is None"));
            }

            _logger.LogInfo("Loading configuration from file.");
            var loadResult = await _manager.LoadConfigurationOrDefault();
            return loadResult.IfFail(ex => throw ex);
        });

    /// <summary>
    /// Loads configuration from file with validation.
    /// </summary>
    /// <param name="configPath">Path to configuration file (optional)</param>
    /// <returns>TryAsync containing the loaded configuration</returns>
    public TryAsync<ServiceConfiguration> LoadConfiguration(string? configPath = null) =>
        TryAsync(async () =>
        {
            _logger.LogInfo($"Loading configuration from: {configPath ?? "default path"}");
            var loadResult = await _manager.LoadConfiguration(configPath);
            return loadResult.IfFail(ex => throw ex);
        });

    /// <summary>
    /// Saves configuration to file with validation.
    /// </summary>
    /// <param name="config">Configuration to save</param>
    /// <param name="configPath">Path to save configuration (optional)</param>
    /// <returns>TryAsync containing Unit on success</returns>
    public TryAsync<Unit> SaveConfiguration(ServiceConfiguration config, string? configPath = null) =>
        TryAsync(async () =>
        {
            _logger.LogInfo($"Saving configuration to: {configPath ?? "default path"}");
            var saveResult = await _manager.SaveConfiguration(config, configPath);
            return saveResult.IfFail(ex => throw ex);
        });

    /// <summary>
    /// Reloads configuration from file (bypasses cache).
    /// </summary>
    /// <param name="configPath">Path to configuration file (optional)</param>
    /// <returns>TryAsync containing the reloaded configuration</returns>
    public TryAsync<ServiceConfiguration> ReloadConfiguration(string? configPath = null) =>
        TryAsync(async () =>
        {
            _logger.LogInfo($"Reloading configuration from: {configPath ?? "default path"}");
            var reloadResult = await _manager.ReloadConfiguration(configPath);
            return reloadResult.IfFail(ex => throw ex);
        });

    /// <summary>
    /// Validates a configuration and returns detailed report.
    /// </summary>
    /// <param name="config">Configuration to validate</param>
    /// <returns>Validation report</returns>
    public ValidationReport ValidateConfiguration(ServiceConfiguration config)
    {
        _logger.LogInfo("Validating configuration.");
        var report = _factory.ValidateConfiguration(config);
        
        if (!report.Result.IsSuccess)
        {
            _logger.LogError($"Configuration validation failed: {report.Summary}");
            ConfigurationValidationFailed?.Invoke(this, new ConfigurationValidationFailedEventArgs(report));
        }
        else if (!report.Result.Warnings.IsEmpty)
        {
            _logger.LogWarning($"Configuration validation warnings: {report.Summary}");
        }
        else
        {
            _logger.LogSuccess("Configuration validation passed successfully.");
        }

        return report;
    }

    /// <summary>
    /// Validates configuration from file without loading it into cache.
    /// </summary>
    /// <param name="configPath">Path to configuration file</param>
    /// <returns>TryAsync containing validation report</returns>
    public TryAsync<ValidationReport> ValidateConfigurationFromFile(string configPath) =>
        TryAsync(async () =>
        {
            _logger.LogInfo($"Validating configuration from file: {configPath}");
            var validateResult = await _manager.ValidateConfigurationFromFile(configPath);
            return validateResult.IfFail(ex => throw ex);
        });

    /// <summary>
    /// Creates a default configuration for the specified backend type.
    /// </summary>
    /// <param name="backendType">Backend type (WSL2 or Docker)</param>
    /// <returns>Default configuration</returns>
    public ServiceConfiguration CreateDefaultConfiguration(string backendType = Constants.BackendTypeWSL2)
    {
        _logger.LogInfo($"Creating default configuration for backend: {backendType}");
        return _factory.CreateDefaultConfiguration(backendType);
    }

    /// <summary>
    /// Creates a configuration using the fluent builder pattern.
    /// </summary>
    /// <param name="builderAction">Builder action to configure the settings</param>
    /// <returns>Built configuration</returns>
    public ServiceConfiguration CreateConfigurationWithBuilder(Action<ConfigurationBuilder> builderAction)
    {
        _logger.LogInfo("Creating configuration using fluent builder.");
        return _factory.CreateConfigurationWithBuilder(builderAction);
    }

    /// <summary>
    /// Creates a validated configuration using the fluent builder pattern.
    /// </summary>
    /// <param name="builderAction">Builder action to configure the settings</param>
    /// <returns>Either containing the built configuration or validation errors</returns>
    public Either<Seq<string>, ServiceConfiguration> CreateValidatedConfigurationWithBuilder(Action<ConfigurationBuilder> builderAction)
    {
        _logger.LogInfo("Creating validated configuration using fluent builder.");
        return _factory.CreateValidatedConfigurationWithBuilder(builderAction);
    }

    /// <summary>
    /// Creates a configuration from environment variables.
    /// </summary>
    /// <returns>Configuration based on environment variables</returns>
    public ServiceConfiguration CreateConfigurationFromEnvironment()
    {
        _logger.LogInfo("Creating configuration from environment variables.");
        return _factory.CreateConfigurationFromEnvironment();
    }

    /// <summary>
    /// Gets configuration statistics and information.
    /// </summary>
    /// <returns>Configuration statistics</returns>
    public ConfigurationStatistics GetStatistics()
    {
        var stats = _factory.GetStatistics();
        _logger.LogInfo($"Configuration statistics: {stats.GetSummary()}");
        return stats;
    }

    /// <summary>
    /// Clears the configuration cache.
    /// </summary>
    public void ClearCache()
    {
        _logger.LogInfo("Clearing configuration cache.");
        _factory.ClearCache();
    }

    /// <summary>
    /// Checks if the configuration file exists.
    /// </summary>
    /// <param name="configPath">Path to configuration file (optional)</param>
    /// <returns>True if the file exists, false otherwise</returns>
    public bool ConfigurationFileExists(string? configPath = null)
    {
        var path = configPath ?? Constants.BackendConfigPath;
        var exists = System.IO.File.Exists(path);
        _logger.LogInfo($"Configuration file exists check: {path} = {exists}");
        return exists;
    }

    /// <summary>
    /// Gets the configuration file path.
    /// </summary>
    /// <returns>Configuration file path</returns>
    public string GetConfigurationFilePath() => Constants.BackendConfigPath;

    /// <summary>
    /// Handles configuration file change events.
    /// </summary>
    private async void OnConfigurationFileChanged(ConfigurationChangeEvent e)
    {
        try
        {
            _logger.LogInfo($"Configuration file changed: {e.ChangeType}");

            // Get current cached configuration for comparison
            var currentConfig = _manager.GetCachedConfiguration();
            if (currentConfig.IsNone)
            {
                _logger.LogWarning("No cached configuration available for change analysis.");
                return;
            }

            // Load new configuration
            var newConfigResult = await _manager.ReloadConfiguration();
            var newConfig = newConfigResult.IfFail(ex =>
            {
                _logger.LogError($"Failed to reload configuration after file change: {ex.Message}");
                return null;
            });

            if (newConfig == null) return;

            // Analyze changes
            var changeAnalysisResult = _changeAnalyzer.Analyze(currentConfig.IfNone(() => throw new InvalidOperationException()), newConfig);
            var changeAnalysis = changeAnalysisResult.Match(
                success => new ChangeAnalysis(success.ChangedProperties.ToList(), success.Warnings.ToList(), success.RequiresRestart),
                failure =>
                {
                    _logger.LogError($"Failed to analyze configuration changes: {failure.Message}");
                    return new ChangeAnalysis(new List<ChangedProperty>(), new List<ConfigurationWarning>(), false);
                }
            );

            // Fire configuration changed event
            ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs(
                e.ChangeType,
                currentConfig.IfNone(() => throw new InvalidOperationException()),
                newConfig,
                changeAnalysis
            ));

            _logger.LogInfo($"Configuration change analysis completed: {changeAnalysis.Summary}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error handling configuration file change: {ex.Message}");
        }
    }

    /// <summary>
    /// Disposes the configuration service and cleans up resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _watcher?.Dispose();
            _disposed = true;
            _logger.LogInfo("ConfigurationService disposed.");
        }
    }
}

/// <summary>
/// Event arguments for configuration change events.
/// </summary>
public sealed class ConfigurationChangedEventArgs : EventArgs
{
    /// <summary>
    /// Type of configuration change.
    /// </summary>
    public ConfigurationChangeType ChangeType { get; }

    /// <summary>
    /// Previous configuration.
    /// </summary>
    public ServiceConfiguration PreviousConfiguration { get; }

    /// <summary>
    /// New configuration.
    /// </summary>
    public ServiceConfiguration NewConfiguration { get; }

    /// <summary>
    /// Analysis of the changes.
    /// </summary>
    public ChangeAnalysis ChangeAnalysis { get; }

    /// <summary>
    /// Initializes a new instance of ConfigurationChangedEventArgs.
    /// </summary>
    public ConfigurationChangedEventArgs(
        ConfigurationChangeType changeType,
        ServiceConfiguration previousConfiguration,
        ServiceConfiguration newConfiguration,
        ChangeAnalysis changeAnalysis)
    {
        ChangeType = changeType;
        PreviousConfiguration = previousConfiguration;
        NewConfiguration = newConfiguration;
        ChangeAnalysis = changeAnalysis;
    }
}

/// <summary>
/// Event arguments for configuration validation failure events.
/// </summary>
public sealed class ConfigurationValidationFailedEventArgs : EventArgs
{
    /// <summary>
    /// Validation report containing the failure details.
    /// </summary>
    public ValidationReport ValidationReport { get; }

    /// <summary>
    /// Initializes a new instance of ConfigurationValidationFailedEventArgs.
    /// </summary>
    public ConfigurationValidationFailedEventArgs(ValidationReport validationReport)
    {
        ValidationReport = validationReport;
    }
}
