using LanguageExt;
using static LanguageExt.Prelude;
using Newtonsoft.Json.Linq;
using CustomLogger = RedisServiceWrapper.Logging.ILogger;

namespace RedisServiceWrapper.Configuration.Loading;

/// <summary>
/// Handles configuration versioning and migration for backward compatibility.
/// Supports schema evolution and automatic migration of older configuration files.
/// </summary>
public sealed class ConfigurationVersioning
{
    private readonly CustomLogger _logger;
    private readonly Dictionary<string, ConfigurationMigrator> _migrators;

    /// <summary>
    /// Current configuration schema version.
    /// </summary>
    public const string CurrentVersion = "1.0.0";

    /// <summary>
    /// Creates a ConfigurationVersioning instance.
    /// </summary>
    /// <param name="logger">Logger for versioning operations</param>
    public ConfigurationVersioning(CustomLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _migrators = new Dictionary<string, ConfigurationMigrator>();
        
        // Register built-in migrators
        RegisterBuiltInMigrators();
    }

    /// <summary>
    /// Migrates a configuration from an older version to the current version.
    /// </summary>
    /// <param name="configJson">Configuration JSON as JObject</param>
    /// <param name="fromVersion">Source version (if null, will be detected)</param>
    /// <returns>Try containing migrated configuration or error</returns>
    public Try<ServiceConfiguration> MigrateConfiguration(JObject configJson, string? fromVersion = null) =>
        Try(() =>
        {
            var detectedVersion = fromVersion ?? DetectVersion(configJson);
            
            _logger.LogInfo($"Migrating configuration from version {detectedVersion} to {CurrentVersion}");

            if (detectedVersion == CurrentVersion)
            {
                _logger.LogInfo("Configuration is already at current version");
                return DeserializeCurrentVersion(configJson);
            }

            // Perform migration chain
            var currentJson = configJson;
            var migrationPath = GetMigrationPath(detectedVersion, CurrentVersion);
            
            foreach (var targetVersion in migrationPath)
            {
                currentJson = MigrateToVersion(currentJson, targetVersion);
            }

            var result = DeserializeCurrentVersion(currentJson);
            _logger.LogSuccess($"Configuration successfully migrated from {detectedVersion} to {CurrentVersion}");
            return result;
        });

    /// <summary>
    /// Detects the version of a configuration JSON.
    /// </summary>
    /// <param name="configJson">Configuration JSON</param>
    /// <returns>Detected version string</returns>
    public string DetectVersion(JObject configJson)
    {
        // Check for explicit version field
        if (configJson.TryGetValue("SchemaVersion", out var versionToken))
        {
            return versionToken.Value<string>() ?? "1.0.0";
        }

        // Check for legacy version field
        if (configJson.TryGetValue("Version", out var legacyVersionToken))
        {
            return legacyVersionToken.Value<string>() ?? "1.0.0";
        }

        // Check for configuration structure to infer version
        return InferVersionFromStructure(configJson);
    }

    /// <summary>
    /// Validates that a configuration is compatible with the current version.
    /// </summary>
    /// <param name="config">Configuration to validate</param>
    /// <returns>Try containing validation result</returns>
    public Try<bool> ValidateVersionCompatibility(ServiceConfiguration config) =>
        Try(() =>
        {
            // For now, we'll assume all configurations are compatible
            // In the future, this could check for deprecated fields, etc.
            return true;
        });

    /// <summary>
    /// Gets the migration path from one version to another.
    /// </summary>
    /// <param name="fromVersion">Source version</param>
    /// <param name="toVersion">Target version</param>
    /// <returns>List of versions in migration order</returns>
    public Seq<string> GetMigrationPath(string fromVersion, string toVersion)
    {
        var versions = new[] { "1.0.0", "1.1.0", "1.2.0", "2.0.0" };
        var fromIndex = System.Array.IndexOf(versions, fromVersion);
        var toIndex = System.Array.IndexOf(versions, toVersion);

        if (fromIndex == -1 || toIndex == -1 || fromIndex >= toIndex)
            return toSeq(new string[] { });

        return toSeq(versions.Skip(fromIndex + 1).Take(toIndex - fromIndex));
    }

    /// <summary>
    /// Registers a custom migrator for a specific version.
    /// </summary>
    /// <param name="version">Target version</param>
    /// <param name="migrator">Migrator function</param>
    public Unit RegisterMigrator(string version, ConfigurationMigrator migrator)
    {
        _migrators[version] = migrator;
        _logger.LogInfo($"Registered migrator for version {version}");
        return unit;
    }

    #region Private Methods

    private void RegisterBuiltInMigrators()
    {
        // Register migrator for version 1.1.0
        RegisterMigrator("1.1.0", new ConfigurationMigrator(
            "1.1.0",
            "Add monitoring configuration",
            MigrateToV1_1_0
        ));

        // Register migrator for version 1.2.0
        RegisterMigrator("1.2.0", new ConfigurationMigrator(
            "1.2.0",
            "Add performance configuration",
            MigrateToV1_2_0
        ));

        // Register migrator for version 2.0.0
        RegisterMigrator("2.0.0", new ConfigurationMigrator(
            "2.0.0",
            "Major schema restructure",
            MigrateToV2_0_0
        ));
    }

    private string InferVersionFromStructure(JObject configJson)
    {
        // Check for presence of specific fields to infer version
        if (configJson.ContainsKey("Advanced"))
        {
            return "1.2.0"; // Advanced config was added in 1.2.0
        }
        
        if (configJson.ContainsKey("Performance"))
        {
            return "1.1.0"; // Performance config was added in 1.1.0
        }
        
        if (configJson.ContainsKey("Monitoring"))
        {
            return "1.0.0"; // Basic monitoring was in 1.0.0
        }

        // Default to 1.0.0 for unknown structures
        return "1.0.0";
    }

    private JObject MigrateToVersion(JObject configJson, string targetVersion)
    {
        if (!_migrators.TryGetValue(targetVersion, out var migrator))
        {
            _logger.LogWarning($"No migrator found for version {targetVersion}");
            return configJson;
        }

        _logger.LogInfo($"Applying migration to version {targetVersion}: {migrator.Description}");
        return migrator.Migrate(configJson);
    }

    private ServiceConfiguration DeserializeCurrentVersion(JObject configJson)
    {
        try
        {
            var config = configJson.ToObject<ServiceConfiguration>();
            if (config == null)
                throw new InvalidOperationException("Failed to deserialize configuration");

            return config;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to deserialize configuration: {ex.Message}", ex);
        }
    }

    #endregion

    #region Built-in Migrators

    private JObject MigrateToV1_1_0(JObject configJson)
    {
        // Add performance configuration if missing
        if (!configJson.ContainsKey("Performance"))
        {
            configJson["Performance"] = JObject.FromObject(new
            {
                AutoRestartOnCrash = true,
                RestartDelaySeconds = 5,
                MaxMemoryMb = 512,
                MemoryErrorThreshold = 95,
                EnableSlowLogMonitoring = true,
                SlowLogThreshold = 10000
            });
        }

        // Update version
        configJson["SchemaVersion"] = "1.1.0";
        return configJson;
    }

    private JObject MigrateToV1_2_0(JObject configJson)
    {
        // Add advanced configuration if missing
        if (!configJson.ContainsKey("Advanced"))
        {
            configJson["Advanced"] = JObject.FromObject(new
            {
                EnvironmentVariables = new Dictionary<string, string>(),
                CustomScripts = new string[0],
                DebugMode = false,
                LogLevel = "Info"
            });
        }

        // Update version
        configJson["SchemaVersion"] = "1.2.0";
        return configJson;
    }

    private JObject MigrateToV2_0_0(JObject configJson)
    {
        // Major restructure - this is a placeholder for future major changes
        // For now, just update the version
        configJson["SchemaVersion"] = "2.0.0";
        return configJson;
    }

    #endregion
}

/// <summary>
/// Represents a configuration migrator for a specific version.
/// </summary>
public sealed record ConfigurationMigrator(
    string Version,
    string Description,
    Func<JObject, JObject> Migrate
)
{
    /// <summary>
    /// Returns a string representation of the migrator.
    /// </summary>
    public override string ToString() =>
        $"Migrator[{Version}]: {Description}";
}

/// <summary>
/// Configuration schema information.
/// </summary>
public sealed record ConfigurationSchema(
    string Version,
    DateTime CreatedDate,
    string Description,
    Seq<string> BreakingChanges
)
{
    /// <summary>
    /// Gets the current schema version.
    /// </summary>
    public static ConfigurationSchema Current() =>
        new ConfigurationSchema(
            Version: ConfigurationVersioning.CurrentVersion,
            CreatedDate: DateTime.UtcNow,
            Description: "Current Redis Service Wrapper configuration schema",
            BreakingChanges: toSeq(new string[] { })
        );

    /// <summary>
    /// Returns a string representation of the schema.
    /// </summary>
    public override string ToString() =>
        $"Schema[{Version}]: {Description}";
}

/// <summary>
/// Migration result information.
/// </summary>
public sealed record MigrationResult(
    bool Success,
    string FromVersion,
    string ToVersion,
    Seq<string> AppliedMigrations,
    Seq<string> Warnings
)
{
    /// <summary>
    /// Returns a string representation of the migration result.
    /// </summary>
    public override string ToString() =>
        $"Migration[{FromVersion} -> {ToVersion}]: {(Success ? "Success" : "Failed")} | {AppliedMigrations.Count} migrations | {Warnings.Count} warnings";
}

/// <summary>
/// Factory methods for creating ConfigurationVersioning instances.
/// </summary>
public static class ConfigurationVersioningFactory
{
    /// <summary>
    /// Creates a ConfigurationVersioning with the specified logger.
    /// </summary>
    public static Try<ConfigurationVersioning> Create(CustomLogger logger) =>
        Try(() => new ConfigurationVersioning(logger));
}

/// <summary>
/// Extension methods for ConfigurationVersioning.
/// </summary>
public static class ConfigurationVersioningExtensions
{
    /// <summary>
    /// Migrates configuration and logs the result.
    /// </summary>
    public static Try<ServiceConfiguration> MigrateAndLog(
        this ConfigurationVersioning versioning,
        JObject configJson,
        string? fromVersion = null)
    {
        return versioning.MigrateConfiguration(configJson, fromVersion)
            .Map(config =>
            {
                // Log successful migration
                return config;
            });
    }

    /// <summary>
    /// Checks if a configuration needs migration.
    /// </summary>
    public static Try<bool> NeedsMigration(
        this ConfigurationVersioning versioning,
        JObject configJson)
    {
        return Try(() =>
        {
            var detectedVersion = versioning.DetectVersion(configJson);
            return detectedVersion != ConfigurationVersioning.CurrentVersion;
        });
    }
}
