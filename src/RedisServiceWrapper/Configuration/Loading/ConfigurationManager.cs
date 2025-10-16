using LanguageExt;
using static LanguageExt.Prelude;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RedisServiceWrapper.Configuration;
using RedisServiceWrapper.Configuration.Validation;
using CustomLogger = RedisServiceWrapper.Logging.ILogger;

namespace RedisServiceWrapper.Configuration.Loading;

/// <summary>
/// Configuration manager for loading, parsing, and managing Redis service configuration.
/// Uses functional programming principles with Try/Either for error handling.
/// Thread-safe and immutable once loaded.
/// </summary>
public sealed class ConfigurationManager
{
    private readonly CustomLogger _logger;
    private readonly ConfigurationCache _cache;
    private readonly string _defaultConfigPath;
    private readonly ConfigurationValidator _validator;

    /// <summary>
    /// Creates a ConfigurationManager instance.
    /// </summary>
    /// <param name="logger">Logger for configuration operations</param>
    /// <param name="cache">Configuration cache (can be shared)</param>
    /// <param name="defaultConfigPath">Default path to backend.json</param>
    /// <param name="validator">Configuration validator (optional, uses default if null)</param>
    public ConfigurationManager(
        CustomLogger logger,
        ConfigurationCache? cache = null,
        string? defaultConfigPath = null,
        ConfigurationValidator? validator = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? new ConfigurationCache();
        _defaultConfigPath = defaultConfigPath ?? Constants.BackendConfigPath;
        _validator = validator ?? new ConfigurationValidator();
    }

    /// <summary>
    /// Loads configuration from JSON file with caching.
    /// </summary>
    /// <param name="configPath">Path to configuration file (optional, uses default if null)</param>
    /// <returns>TryAsync containing the loaded configuration or error</returns>
    public TryAsync<ServiceConfiguration> LoadConfiguration(string? configPath = null) =>
        TryAsync(async () =>
        {
            var path = configPath ?? _defaultConfigPath;
            
            _logger.LogInfo($"Loading configuration from: {path}");

            // Try cache first
            var cachedResult = await _cache.GetOrLoadAsync(path, () => LoadFromFile(path));
            var cached = cachedResult.IfFail(ex => throw ex);
            
            _logger.LogSuccess($"Configuration loaded successfully from: {path}");
            return cached;
        });

    /// <summary>
    /// Loads configuration or returns default if file doesn't exist.
    /// </summary>
    /// <param name="configPath">Path to configuration file</param>
    /// <returns>TryAsync containing configuration (loaded or default)</returns>
    public TryAsync<ServiceConfiguration> LoadConfigurationOrDefault(string? configPath = null) =>
        TryAsync(async () =>
        {
            var path = configPath ?? _defaultConfigPath;
            
            if (!File.Exists(path))
            {
                _logger.LogWarning($"Configuration file not found: {path}. Using default configuration.");
                var defaultConfig = DefaultConfiguration.GetDefault();
                await _cache.SetAsync(defaultConfig);
                return defaultConfig;
            }

            var loadResult = await LoadConfiguration(path);
            return loadResult.IfFail(ex => throw ex);
        });

    /// <summary>
    /// Loads configuration from file without caching (for testing or one-time loads).
    /// </summary>
    /// <param name="configPath">Path to configuration file</param>
    /// <returns>TryAsync containing the loaded configuration</returns>
    public TryAsync<ServiceConfiguration> LoadFromFile(string configPath) =>
        TryAsync(async () =>
        {
            // Read JSON file
            var jsonResult = await ReadJsonFileAsync(configPath);
            var json = jsonResult.IfFail(ex => throw ex);
            
            // Parse JSON to JObject
            var jObjectResult = ParseJson(json);
            var jObject = jObjectResult.IfFail(ex => throw ex);
            
            // Deserialize to ServiceConfiguration
            var configResult = DeserializeConfiguration(jObject);
            var config = configResult.IfFail(ex => throw ex);
            
            // Validate configuration
            var validationResult = _validator.Validate(config);
            if (!validationResult.IsSuccess)
            {
                var errorMessage = $"Configuration validation failed: {validationResult.GetSummary()}";
                _logger.LogError(errorMessage);
                throw new ValidationException(validationResult.Errors);
            }
            
            // Log warnings if any
            if (!validationResult.Warnings.IsEmpty)
            {
                _logger.LogWarning($"Configuration validation warnings: {validationResult.GetSummary()}");
            }
            
            return config;
        });

    /// <summary>
    /// Saves configuration to JSON file.
    /// </summary>
    /// <param name="config">Configuration to save</param>
    /// <param name="configPath">Path to save configuration (optional)</param>
    /// <returns>TryAsync containing Unit on success or error</returns>
    public TryAsync<Unit> SaveConfiguration(ServiceConfiguration config, string? configPath = null) =>
        TryAsync(async () =>
        {
            var path = configPath ?? _defaultConfigPath;
            
            _logger.LogInfo($"Saving configuration to: {path}");

            // Validate configuration before saving
            var validationResult = _validator.Validate(config);
            if (!validationResult.IsSuccess)
            {
                var errorMessage = $"Cannot save invalid configuration: {validationResult.GetSummary()}";
                _logger.LogError(errorMessage);
                throw new ValidationException(validationResult.Errors);
            }
            
            // Log warnings if any
            if (!validationResult.Warnings.IsEmpty)
            {
                _logger.LogWarning($"Configuration validation warnings: {validationResult.GetSummary()}");
            }

            // Serialize to JSON
            var json = SerializeConfiguration(config);
            
            // Write to file
            await WriteJsonFileAsync(path, json);
            
            // Update cache
            await _cache.SetAsync(config);
            
            _logger.LogSuccess($"Configuration saved successfully to: {path}");
            return unit;
        });

    /// <summary>
    /// Reloads configuration from file (bypasses cache).
    /// </summary>
    /// <param name="configPath">Path to configuration file (optional)</param>
    /// <returns>TryAsync containing the reloaded configuration</returns>
    public TryAsync<ServiceConfiguration> ReloadConfiguration(string? configPath = null) =>
        TryAsync(async () =>
        {
            var path = configPath ?? _defaultConfigPath;
            
            _logger.LogInfo($"Reloading configuration from: {path}");
            
            // Invalidate cache
            _cache.Invalidate();
            
            // Load fresh from file
            var configResult = await LoadFromFile(path);
            var config = configResult.IfFail(ex => throw ex);
            
            _logger.LogSuccess($"Configuration reloaded successfully from: {path}");
            return config;
        });

    /// <summary>
    /// Gets the current cached configuration (if any).
    /// </summary>
    /// <returns>Option containing cached configuration or None</returns>
    public Option<ServiceConfiguration> GetCachedConfiguration() =>
        _cache.Get();

    /// <summary>
    /// Validates a configuration and returns detailed validation report.
    /// </summary>
    /// <param name="config">Configuration to validate</param>
    /// <returns>Validation report with detailed results</returns>
    public ValidationReport ValidateConfiguration(ServiceConfiguration config) =>
        _validator.ValidateWithReport(config);

    /// <summary>
    /// Validates configuration from file without loading it into cache.
    /// </summary>
    /// <param name="configPath">Path to configuration file</param>
    /// <returns>TryAsync containing validation report</returns>
    public TryAsync<ValidationReport> ValidateConfigurationFromFile(string configPath) =>
        TryAsync(async () =>
        {
            // Load configuration without caching
            var configResult = await LoadFromFile(configPath);
            var config = configResult.IfFail(ex => throw ex);
            
            // Return validation report
            return _validator.ValidateWithReport(config);
        });

    #region Private Helper Methods

    /// <summary>
    /// Reads JSON content from file.
    /// </summary>
    private TryAsync<string> ReadJsonFileAsync(string path) =>
        TryAsync(async () =>
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Configuration file not found: {path}");

            var content = await File.ReadAllTextAsync(path);
            
            if (string.IsNullOrWhiteSpace(content))
                throw new InvalidOperationException($"Configuration file is empty: {path}");

            return content;
        });

    /// <summary>
    /// Writes JSON content to file.
    /// </summary>
    private TryAsync<Unit> WriteJsonFileAsync(string path, string json) =>
        TryAsync(async () =>
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(path, json);
            return unit;
        });

    /// <summary>
    /// Parses JSON string to JObject.
    /// </summary>
    private Try<JObject> ParseJson(string json) =>
        Try(() =>
        {
            try
            {
                var jObject = JObject.Parse(json);
                return jObject;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Invalid JSON format: {ex.Message}", ex);
            }
        });

    /// <summary>
    /// Deserializes JObject to ServiceConfiguration.
    /// </summary>
    private Try<ServiceConfiguration> DeserializeConfiguration(JObject jObject) =>
        Try(() =>
        {
            try
            {
                var config = jObject.ToObject<ServiceConfiguration>();
                if (config == null)
                    throw new InvalidOperationException("Failed to deserialize configuration - result is null");

                return config;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Failed to deserialize configuration: {ex.Message}", ex);
            }
        });

    /// <summary>
    /// Serializes ServiceConfiguration to JSON string.
    /// </summary>
    private string SerializeConfiguration(ServiceConfiguration config)
    {
        var settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };

        return JsonConvert.SerializeObject(config, settings);
    }

    #endregion
}