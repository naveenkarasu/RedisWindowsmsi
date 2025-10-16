using LanguageExt;
using static LanguageExt.Prelude;
using RedisServiceWrapper.Configuration.Loading;
using RedisServiceWrapper.Configuration.Validation;
using RedisServiceWrapper.Logging;
using System;
using System.Collections.Generic;
using CustomLogger = RedisServiceWrapper.Logging.ILogger;
using CustomConfigurationManager = RedisServiceWrapper.Configuration.Loading.ConfigurationManager;

namespace RedisServiceWrapper.Configuration;

/// <summary>
/// Factory for creating and managing configuration-related services.
/// Provides dependency injection support and easy access to configuration management.
/// </summary>
public sealed class ConfigurationFactory
{
    private readonly CustomLogger _logger;
    private readonly ConfigurationCache _cache;
    private readonly ConfigurationValidator _validator;
    private readonly string _defaultConfigPath;

    /// <summary>
    /// Initializes a new instance of the ConfigurationFactory.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="defaultConfigPath">Default configuration file path</param>
    public ConfigurationFactory(CustomLogger logger, string? defaultConfigPath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _defaultConfigPath = defaultConfigPath ?? Constants.BackendConfigPath;
        _cache = new ConfigurationCache();
        _validator = new ConfigurationValidator();
    }

    /// <summary>
    /// Creates a ConfigurationManager instance with default settings.
    /// </summary>
    /// <returns>New ConfigurationManager instance</returns>
    public CustomConfigurationManager CreateConfigurationManager() =>
        new CustomConfigurationManager(_logger, _cache, _defaultConfigPath, _validator);

    /// <summary>
    /// Creates a ConfigurationManager instance with custom settings.
    /// </summary>
    /// <param name="configPath">Custom configuration file path</param>
    /// <param name="useCustomValidator">Whether to use a custom validator</param>
    /// <returns>New ConfigurationManager instance</returns>
    public CustomConfigurationManager CreateConfigurationManager(string? configPath = null, bool useCustomValidator = false)
    {
        var validator = useCustomValidator ? new ConfigurationValidator() : _validator;
        return new CustomConfigurationManager(_logger, _cache, configPath ?? _defaultConfigPath, validator);
    }

    /// <summary>
    /// Creates a ConfigurationManager instance with custom validator.
    /// </summary>
    /// <param name="customValidator">Custom validator to use</param>
    /// <param name="configPath">Custom configuration file path</param>
    /// <returns>New ConfigurationManager instance</returns>
    public CustomConfigurationManager CreateConfigurationManager(ConfigurationValidator customValidator, string? configPath = null) =>
        new CustomConfigurationManager(_logger, _cache, configPath ?? _defaultConfigPath, customValidator);

    /// <summary>
    /// Gets the shared configuration cache.
    /// </summary>
    /// <returns>Shared ConfigurationCache instance</returns>
    public ConfigurationCache GetSharedCache() => _cache;

    /// <summary>
    /// Gets the default configuration validator.
    /// </summary>
    /// <returns>Default ConfigurationValidator instance</returns>
    public ConfigurationValidator GetDefaultValidator() => _validator;

    /// <summary>
    /// Creates a new configuration validator with custom validators.
    /// </summary>
    /// <param name="validators">Custom validators to use</param>
    /// <returns>New ConfigurationValidator instance</returns>
    public ConfigurationValidator CreateCustomValidator(params IConfigurationValidator<ServiceConfiguration>[] validators) =>
        new ConfigurationValidator(validators);

    /// <summary>
    /// Creates a default configuration instance.
    /// </summary>
    /// <param name="backendType">Backend type (WSL2 or Docker)</param>
    /// <returns>Default ServiceConfiguration instance</returns>
    public ServiceConfiguration CreateDefaultConfiguration(string backendType = Constants.BackendTypeWSL2)
    {
        var config = DefaultConfiguration.GetDefault();
        
        // Override backend type if specified
        if (backendType != Constants.BackendTypeWSL2)
        {
            config = config with { BackendType = backendType };
        }

        return config;
    }

    /// <summary>
    /// Creates a configuration using the fluent builder pattern.
    /// </summary>
    /// <param name="builderAction">Builder action to configure the settings</param>
    /// <returns>Built ServiceConfiguration instance</returns>
    public ServiceConfiguration CreateConfigurationWithBuilder(Action<ConfigurationBuilder> builderAction)
    {
        var builder = new ConfigurationBuilder();
        builderAction(builder);
        return builder.Build();
    }

    /// <summary>
    /// Creates a configuration using the fluent builder pattern with validation.
    /// </summary>
    /// <param name="builderAction">Builder action to configure the settings</param>
    /// <returns>Either containing the built configuration or validation errors</returns>
    public Either<Seq<string>, ServiceConfiguration> CreateValidatedConfigurationWithBuilder(Action<ConfigurationBuilder> builderAction)
    {
        var builder = new ConfigurationBuilder();
        builderAction(builder);
        return builder.BuildAndValidate();
    }

    /// <summary>
    /// Validates a configuration and returns detailed report.
    /// </summary>
    /// <param name="config">Configuration to validate</param>
    /// <returns>Validation report</returns>
    public ValidationReport ValidateConfiguration(ServiceConfiguration config) =>
        _validator.ValidateWithReport(config);

    /// <summary>
    /// Creates a configuration from a dictionary of key-value pairs.
    /// </summary>
    /// <param name="settings">Dictionary of configuration settings</param>
    /// <returns>ServiceConfiguration instance</returns>
    public ServiceConfiguration CreateConfigurationFromDictionary(Dictionary<string, object> settings)
    {
        var builder = new ConfigurationBuilder();

        foreach (var (key, value) in settings)
        {
            ApplySettingToBuilder(builder, key, value);
        }

        return builder.Build();
    }

    /// <summary>
    /// Creates a configuration from environment variables.
    /// </summary>
    /// <returns>ServiceConfiguration instance based on environment variables</returns>
    public ServiceConfiguration CreateConfigurationFromEnvironment()
    {
        var builder = new ConfigurationBuilder();

        // Read environment variables and apply to builder
        var backendType = Environment.GetEnvironmentVariable("REDIS_BACKEND_TYPE") ?? Constants.BackendTypeWSL2;
        builder.WithBackendType(backendType);

        if (int.TryParse(Environment.GetEnvironmentVariable("REDIS_PORT"), out var port))
        {
            builder.WithRedis(r => r.WithPort(port));
        }

        var password = Environment.GetEnvironmentVariable("REDIS_PASSWORD");
        if (bool.TryParse(Environment.GetEnvironmentVariable("REDIS_REQUIRE_PASSWORD"), out var requirePassword))
        {
            builder.WithRedis(r => r.WithAuthentication(requirePassword, password ?? ""));
        }
        else if (!string.IsNullOrEmpty(password))
        {
            builder.WithRedis(r => r.WithAuthentication(true, password));
        }

        return builder.Build();
    }

    /// <summary>
    /// Gets configuration statistics and information.
    /// </summary>
    /// <returns>Configuration statistics</returns>
    public ConfigurationStatistics GetStatistics()
    {
        var cachedConfig = _cache.Get();
        return new ConfigurationStatistics(
            HasCachedConfiguration: cachedConfig.IsSome,
            DefaultConfigPath: _defaultConfigPath,
            CacheHitCount: 0, // TODO: Add cache statistics to ConfigurationCache
            CacheMissCount: 0, // TODO: Add cache statistics to ConfigurationCache
            LastCacheUpdate: null // TODO: Add last update time to ConfigurationCache
        );
    }

    /// <summary>
    /// Clears the configuration cache.
    /// </summary>
    public void ClearCache() => _cache.Invalidate();

    /// <summary>
    /// Applies a setting to the configuration builder based on the key path.
    /// </summary>
    private void ApplySettingToBuilder(ConfigurationBuilder builder, string key, object value)
    {
        try
        {
            switch (key.ToLowerInvariant())
            {
                case "backendtype":
                    builder.WithBackendType(value.ToString() ?? Constants.BackendTypeWSL2);
                    break;

                case "redis.port":
                    if (int.TryParse(value.ToString(), out var port))
                        builder.WithRedis(r => r.WithPort(port));
                    break;

                case "redis.bindaddress":
                    builder.WithRedis(r => r.WithBindAddress(value.ToString() ?? "127.0.0.1"));
                    break;

                case "redis.requirepassword":
                    if (bool.TryParse(value.ToString(), out var requirePassword))
                    {
                        var password = Environment.GetEnvironmentVariable("REDIS_PASSWORD") ?? "";
                        builder.WithRedis(r => r.WithAuthentication(requirePassword, password));
                    }
                    break;

                case "redis.password":
                    var currentPassword = value.ToString() ?? "";
                    builder.WithRedis(r => r.WithAuthentication(!string.IsNullOrEmpty(currentPassword), currentPassword));
                    break;

                case "service.servicename":
                    builder.WithService(s => s.WithServiceName(value.ToString() ?? Constants.ServiceName));
                    break;

                case "service.displayname":
                    builder.WithService(s => s.WithDisplayName(value.ToString() ?? Constants.ServiceDisplayName));
                    break;

                case "monitoring.enablehealthcheck":
                    if (bool.TryParse(value.ToString(), out var enableHealthCheck))
                        builder.WithMonitoring(m => m.WithHealthCheck(enableHealthCheck));
                    break;

                case "performance.enableautorestart":
                    if (bool.TryParse(value.ToString(), out var enableAutoRestart))
                        builder.WithPerformance(p => p.WithAutoRestart(enableAutoRestart));
                    break;

                default:
                    _logger.LogWarning($"Unknown configuration key: {key}");
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error applying configuration setting {key}: {ex.Message}");
        }
    }
}

/// <summary>
/// Statistics about configuration management.
/// </summary>
public sealed record ConfigurationStatistics(
    bool HasCachedConfiguration,
    string DefaultConfigPath,
    int CacheHitCount,
    int CacheMissCount,
    DateTime? LastCacheUpdate
)
{
    /// <summary>
    /// Gets the cache hit ratio.
    /// </summary>
    public double CacheHitRatio
    {
        get
        {
            var total = CacheHitCount + CacheMissCount;
            return total > 0 ? (double)CacheHitCount / total : 0.0;
        }
    }

    /// <summary>
    /// Gets a summary of the statistics.
    /// </summary>
    public string GetSummary() =>
        $"Cache: {CacheHitCount} hits, {CacheMissCount} misses ({(CacheHitRatio * 100):F1}% hit ratio), " +
        $"Cached: {HasCachedConfiguration}, Last Update: {LastCacheUpdate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Never"}";
}
