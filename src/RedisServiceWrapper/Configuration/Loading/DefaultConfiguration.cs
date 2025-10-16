using LanguageExt;
using static LanguageExt.Prelude;
using RedisServiceWrapper.Configuration;

namespace RedisServiceWrapper.Configuration.Loading;

/// <summary>
/// Generates default configurations for the Redis service.
/// Provides sensible defaults for WSL2 and Docker backends.
/// All functions are pure (no side effects).
/// </summary>
public static class DefaultConfiguration
{
    /// <summary>
    /// Gets the default configuration (WSL2 backend).
    /// </summary>
    /// <returns>Default ServiceConfiguration</returns>
    public static ServiceConfiguration GetDefault() =>
        GetDefaultWSL2();

    /// <summary>
    /// Gets default configuration for WSL2 backend.
    /// </summary>
    /// <returns>ServiceConfiguration configured for WSL2</returns>
    public static ServiceConfiguration GetDefaultWSL2() =>
        new ServiceConfiguration
        {
            BackendType = Constants.BackendTypeWSL2,
            Wsl = GetDefaultWslConfiguration(),
            Docker = new DockerConfiguration(), // Empty for WSL2
            Redis = GetDefaultRedisConfiguration(),
            Service = GetDefaultServiceSettings(),
            Monitoring = GetDefaultMonitoringConfiguration(),
            Performance = GetDefaultPerformanceConfiguration(),
            Advanced = GetDefaultAdvancedConfiguration()
        };

    /// <summary>
    /// Gets default configuration for Docker backend.
    /// </summary>
    /// <returns>ServiceConfiguration configured for Docker</returns>
    public static ServiceConfiguration GetDefaultDocker() =>
        new ServiceConfiguration
        {
            BackendType = Constants.BackendTypeDocker,
            Wsl = new WslConfiguration(), // Empty for Docker
            Docker = GetDefaultDockerConfiguration(),
            Redis = GetDefaultRedisConfiguration(),
            Service = GetDefaultServiceSettings(),
            Monitoring = GetDefaultMonitoringConfiguration(),
            Performance = GetDefaultPerformanceConfiguration(),
            Advanced = GetDefaultAdvancedConfiguration()
        };

    /// <summary>
    /// Merges partial configuration with defaults.
    /// Uses functional merge strategy - provided values override defaults.
    /// </summary>
    /// <param name="partial">Partial configuration to merge</param>
    /// <returns>Merged configuration with defaults filled in</returns>
    public static ServiceConfiguration MergeWithDefaults(ServiceConfiguration partial) =>
        partial with
        {
            BackendType = string.IsNullOrEmpty(partial.BackendType) ? Constants.BackendTypeWSL2 : partial.BackendType,
            Wsl = MergeWslConfiguration(partial.Wsl),
            Docker = MergeDockerConfiguration(partial.Docker),
            Redis = MergeRedisConfiguration(partial.Redis),
            Service = MergeServiceSettings(partial.Service),
            Monitoring = MergeMonitoringConfiguration(partial.Monitoring),
            Performance = MergePerformanceConfiguration(partial.Performance),
            Advanced = MergeAdvancedConfiguration(partial.Advanced)
        };

    /// <summary>
    /// Exports default configuration to a JSON file.
    /// </summary>
    /// <param name="path">Path to save the configuration file</param>
    /// <param name="backendType">Type of backend (WSL2 or Docker)</param>
    /// <returns>TryAsync containing Unit on success or error</returns>
    public static TryAsync<Unit> ExportDefaultConfiguration(
        string path, 
        string backendType = Constants.BackendTypeWSL2) =>
        TryAsync(async () =>
        {
            var config = backendType.Equals(Constants.BackendTypeWSL2, StringComparison.OrdinalIgnoreCase)
                ? GetDefaultWSL2()
                : GetDefaultDocker();

            var json = SerializeConfiguration(config);
            await File.WriteAllTextAsync(path, json);
            return unit;
        });

    #region Default Configuration Components

    /// <summary>
    /// Gets default WSL2 configuration.
    /// </summary>
    private static WslConfiguration GetDefaultWslConfiguration() =>
        new WslConfiguration();

    /// <summary>
    /// Gets default Docker configuration.
    /// </summary>
    private static DockerConfiguration GetDefaultDockerConfiguration() =>
        new DockerConfiguration();

    /// <summary>
    /// Gets default Redis configuration.
    /// </summary>
    private static RedisConfiguration GetDefaultRedisConfiguration() =>
        new RedisConfiguration();

    /// <summary>
    /// Gets default service settings.
    /// </summary>
    private static ServiceSettings GetDefaultServiceSettings() =>
        new ServiceSettings();

    /// <summary>
    /// Gets default monitoring configuration.
    /// </summary>
    private static MonitoringConfiguration GetDefaultMonitoringConfiguration() =>
        new MonitoringConfiguration();

    /// <summary>
    /// Gets default performance configuration.
    /// </summary>
    private static PerformanceConfiguration GetDefaultPerformanceConfiguration() =>
        new PerformanceConfiguration();

    /// <summary>
    /// Gets default advanced configuration.
    /// </summary>
    private static AdvancedConfiguration GetDefaultAdvancedConfiguration() =>
        new AdvancedConfiguration();

    #endregion

    #region Merge Methods

    /// <summary>
    /// Merges WSL configuration with defaults.
    /// </summary>
    private static WslConfiguration MergeWslConfiguration(WslConfiguration wsl) =>
        wsl; // Use constructor defaults

    /// <summary>
    /// Merges Docker configuration with defaults.
    /// </summary>
    private static DockerConfiguration MergeDockerConfiguration(DockerConfiguration docker) =>
        docker; // Use constructor defaults

    /// <summary>
    /// Merges Redis configuration with defaults.
    /// </summary>
    private static RedisConfiguration MergeRedisConfiguration(RedisConfiguration redis) =>
        redis; // Use constructor defaults

    /// <summary>
    /// Merges service settings with defaults.
    /// </summary>
    private static ServiceSettings MergeServiceSettings(ServiceSettings service) =>
        service; // Use constructor defaults

    /// <summary>
    /// Merges monitoring configuration with defaults.
    /// </summary>
    private static MonitoringConfiguration MergeMonitoringConfiguration(MonitoringConfiguration monitoring) =>
        monitoring; // Use constructor defaults

    /// <summary>
    /// Merges performance configuration with defaults.
    /// </summary>
    private static PerformanceConfiguration MergePerformanceConfiguration(PerformanceConfiguration performance) =>
        performance; // Use constructor defaults

    /// <summary>
    /// Merges advanced configuration with defaults.
    /// </summary>
    private static AdvancedConfiguration MergeAdvancedConfiguration(AdvancedConfiguration advanced) =>
        advanced; // Use constructor defaults

    #endregion

    #region Helper Methods

    /// <summary>
    /// Serializes configuration to JSON string.
    /// </summary>
    private static string SerializeConfiguration(ServiceConfiguration config)
    {
        var settings = new Newtonsoft.Json.JsonSerializerSettings
        {
            Formatting = Newtonsoft.Json.Formatting.Indented,
            NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
            DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore
        };

        return Newtonsoft.Json.JsonConvert.SerializeObject(config, settings);
    }

    #endregion
}