using LanguageExt;
using static LanguageExt.Prelude;

namespace RedisServiceWrapper.Configuration;

/// <summary>
/// Fluent builder pattern for creating ServiceConfiguration instances.
/// Provides a clean, readable API for configuration creation and modification.
/// </summary>
public sealed class ConfigurationBuilder
{
    private ServiceConfiguration _config;

    /// <summary>
    /// Initializes a new instance of the ConfigurationBuilder with default configuration.
    /// </summary>
    public ConfigurationBuilder()
    {
        _config = new ServiceConfiguration();
    }

    /// <summary>
    /// Initializes a new instance of the ConfigurationBuilder with an existing configuration.
    /// </summary>
    /// <param name="existingConfig">Existing configuration to build upon</param>
    public ConfigurationBuilder(ServiceConfiguration existingConfig)
    {
        _config = existingConfig;
    }

    /// <summary>
    /// Sets the schema version for the configuration.
    /// </summary>
    /// <param name="version">Schema version (e.g., "1.0.0")</param>
    /// <returns>This builder instance for method chaining</returns>
    public ConfigurationBuilder WithSchemaVersion(string version)
    {
        _config = _config with { SchemaVersion = version };
        return this;
    }

    /// <summary>
    /// Sets the backend type for the configuration.
    /// </summary>
    /// <param name="backendType">Backend type ("WSL2" or "Docker")</param>
    /// <returns>This builder instance for method chaining</returns>
    public ConfigurationBuilder WithBackendType(string backendType)
    {
        _config = _config with { BackendType = backendType };
        return this;
    }

    /// <summary>
    /// Sets the backend type to WSL2.
    /// </summary>
    /// <returns>This builder instance for method chaining</returns>
    public ConfigurationBuilder WithWSL2Backend()
    {
        return WithBackendType(Constants.BackendTypeWSL2);
    }

    /// <summary>
    /// Sets the backend type to Docker.
    /// </summary>
    /// <returns>This builder instance for method chaining</returns>
    public ConfigurationBuilder WithDockerBackend()
    {
        return WithBackendType(Constants.BackendTypeDocker);
    }

    /// <summary>
    /// Configures WSL2-specific settings.
    /// </summary>
    /// <param name="configureWsl">Action to configure WSL settings</param>
    /// <returns>This builder instance for method chaining</returns>
    public ConfigurationBuilder WithWSL2(Action<WslConfigurationBuilder> configureWsl)
    {
        var wslBuilder = new WslConfigurationBuilder(_config.Wsl);
        configureWsl(wslBuilder);
        _config = _config with { Wsl = wslBuilder.Build() };
        return this;
    }

    /// <summary>
    /// Configures Docker-specific settings.
    /// </summary>
    /// <param name="configureDocker">Action to configure Docker settings</param>
    /// <returns>This builder instance for method chaining</returns>
    public ConfigurationBuilder WithDocker(Action<DockerConfigurationBuilder> configureDocker)
    {
        var dockerBuilder = new DockerConfigurationBuilder(_config.Docker);
        configureDocker(dockerBuilder);
        _config = _config with { Docker = dockerBuilder.Build() };
        return this;
    }

    /// <summary>
    /// Configures Redis-specific settings.
    /// </summary>
    /// <param name="configureRedis">Action to configure Redis settings</param>
    /// <returns>This builder instance for method chaining</returns>
    public ConfigurationBuilder WithRedis(Action<RedisConfigurationBuilder> configureRedis)
    {
        var redisBuilder = new RedisConfigurationBuilder(_config.Redis);
        configureRedis(redisBuilder);
        _config = _config with { Redis = redisBuilder.Build() };
        return this;
    }

    /// <summary>
    /// Configures Windows Service settings.
    /// </summary>
    /// <param name="configureService">Action to configure service settings</param>
    /// <returns>This builder instance for method chaining</returns>
    public ConfigurationBuilder WithService(Action<ServiceSettingsBuilder> configureService)
    {
        var serviceBuilder = new ServiceSettingsBuilder(_config.Service);
        configureService(serviceBuilder);
        _config = _config with { Service = serviceBuilder.Build() };
        return this;
    }

    /// <summary>
    /// Configures monitoring settings.
    /// </summary>
    /// <param name="configureMonitoring">Action to configure monitoring settings</param>
    /// <returns>This builder instance for method chaining</returns>
    public ConfigurationBuilder WithMonitoring(Action<MonitoringConfigurationBuilder> configureMonitoring)
    {
        var monitoringBuilder = new MonitoringConfigurationBuilder(_config.Monitoring);
        configureMonitoring(monitoringBuilder);
        _config = _config with { Monitoring = monitoringBuilder.Build() };
        return this;
    }

    /// <summary>
    /// Configures performance settings.
    /// </summary>
    /// <param name="configurePerformance">Action to configure performance settings</param>
    /// <returns>This builder instance for method chaining</returns>
    public ConfigurationBuilder WithPerformance(Action<PerformanceConfigurationBuilder> configurePerformance)
    {
        var performanceBuilder = new PerformanceConfigurationBuilder(_config.Performance);
        configurePerformance(performanceBuilder);
        _config = _config with { Performance = performanceBuilder.Build() };
        return this;
    }

    /// <summary>
    /// Configures advanced settings.
    /// </summary>
    /// <param name="configureAdvanced">Action to configure advanced settings</param>
    /// <returns>This builder instance for method chaining</returns>
    public ConfigurationBuilder WithAdvanced(Action<AdvancedConfigurationBuilder> configureAdvanced)
    {
        var advancedBuilder = new AdvancedConfigurationBuilder(_config.Advanced);
        configureAdvanced(advancedBuilder);
        _config = _config with { Advanced = advancedBuilder.Build() };
        return this;
    }

    /// <summary>
    /// Configures metadata settings.
    /// </summary>
    /// <param name="configureMetadata">Action to configure metadata settings</param>
    /// <returns>This builder instance for method chaining</returns>
    public ConfigurationBuilder WithMetadata(Action<MetadataConfigurationBuilder> configureMetadata)
    {
        var metadataBuilder = new MetadataConfigurationBuilder(_config.Metadata);
        configureMetadata(metadataBuilder);
        _config = _config with { Metadata = metadataBuilder.Build() };
        return this;
    }

    /// <summary>
    /// Builds the final ServiceConfiguration instance.
    /// </summary>
    /// <returns>The configured ServiceConfiguration</returns>
    public ServiceConfiguration Build() => _config;

    /// <summary>
    /// Builds and validates the final ServiceConfiguration instance.
    /// </summary>
    /// <returns>Either containing the validated configuration or validation errors</returns>
    public Either<Seq<string>, ServiceConfiguration> BuildAndValidate() =>
        _config.Validate();
}

/// <summary>
/// Fluent builder for WSL2 configuration.
/// </summary>
public sealed class WslConfigurationBuilder
{
    private WslConfiguration _config;

    public WslConfigurationBuilder(WslConfiguration config)
    {
        _config = config;
    }

    public WslConfigurationBuilder WithDistribution(string distribution)
    {
        _config = _config with { Distribution = distribution };
        return this;
    }

    public WslConfigurationBuilder WithRedisPath(string redisPath)
    {
        _config = _config with { RedisPath = redisPath };
        return this;
    }

    public WslConfigurationBuilder WithRedisCliPath(string redisCliPath)
    {
        _config = _config with { RedisCliPath = redisCliPath };
        return this;
    }

    public WslConfigurationBuilder WithConfigPath(string configPath)
    {
        _config = _config with { ConfigPath = configPath };
        return this;
    }

    public WslConfigurationBuilder WithDataPath(string dataPath)
    {
        _config = _config with { DataPath = dataPath };
        return this;
    }

    public WslConfigurationBuilder WithLogPath(string logPath)
    {
        _config = _config with { LogPath = logPath };
        return this;
    }

    public WslConfigurationBuilder WithPidFile(string pidFile)
    {
        _config = _config with { PidFile = pidFile };
        return this;
    }

    public WslConfigurationBuilder WithWindowsDataPath(string windowsDataPath)
    {
        _config = _config with { WindowsDataPath = windowsDataPath };
        return this;
    }

    public WslConfigurationBuilder WithWindowsConfigPath(string windowsConfigPath)
    {
        _config = _config with { WindowsConfigPath = windowsConfigPath };
        return this;
    }

    public WslConfigurationBuilder WithAutoStartOnBoot(bool autoStartOnBoot)
    {
        _config = _config with { AutoStartOnBoot = autoStartOnBoot };
        return this;
    }

    public WslConfigurationBuilder WithHealthCheckInterval(int healthCheckInterval)
    {
        _config = _config with { HealthCheckInterval = healthCheckInterval };
        return this;
    }

    public WslConfiguration Build() => _config;
}

/// <summary>
/// Fluent builder for Docker configuration.
/// </summary>
public sealed class DockerConfigurationBuilder
{
    private DockerConfiguration _config;

    public DockerConfigurationBuilder(DockerConfiguration config)
    {
        _config = config;
    }

    public DockerConfigurationBuilder WithImageName(string imageName)
    {
        _config = _config with { ImageName = imageName };
        return this;
    }

    public DockerConfigurationBuilder WithContainerName(string containerName)
    {
        _config = _config with { ContainerName = containerName };
        return this;
    }

    public DockerConfigurationBuilder WithPortMapping(string portMapping)
    {
        _config = _config with { PortMapping = portMapping };
        return this;
    }

    public DockerConfigurationBuilder WithVolumeMappings(params string[] volumeMappings)
    {
        _config = _config with { VolumeMappings = toSeq(volumeMappings) };
        return this;
    }

    public DockerConfigurationBuilder WithNetworkMode(string networkMode)
    {
        _config = _config with { NetworkMode = networkMode };
        return this;
    }

    public DockerConfigurationBuilder WithRestartPolicy(string restartPolicy)
    {
        _config = _config with { RestartPolicy = restartPolicy };
        return this;
    }

    public DockerConfigurationBuilder WithAutoStartOnBoot(bool autoStartOnBoot)
    {
        _config = _config with { AutoStartOnBoot = autoStartOnBoot };
        return this;
    }

    public DockerConfigurationBuilder WithHealthCheckInterval(int healthCheckInterval)
    {
        _config = _config with { HealthCheckInterval = healthCheckInterval };
        return this;
    }

    public DockerConfigurationBuilder WithResourceLimits(Action<ResourceLimitsBuilder> configureLimits)
    {
        var limitsBuilder = new ResourceLimitsBuilder(_config.ResourceLimits);
        configureLimits(limitsBuilder);
        _config = _config with { ResourceLimits = limitsBuilder.Build() };
        return this;
    }

    public DockerConfiguration Build() => _config;
}

/// <summary>
/// Fluent builder for Docker resource limits.
/// </summary>
public sealed class ResourceLimitsBuilder
{
    private ResourceLimits _config;

    public ResourceLimitsBuilder(ResourceLimits config)
    {
        _config = config;
    }

    public ResourceLimitsBuilder WithMemory(string memory)
    {
        _config = _config with { Memory = memory };
        return this;
    }

    public ResourceLimitsBuilder WithCpus(string cpus)
    {
        _config = _config with { Cpus = cpus };
        return this;
    }

    public ResourceLimits Build() => _config;
}

/// <summary>
/// Fluent builder for Redis configuration.
/// </summary>
public sealed class RedisConfigurationBuilder
{
    private RedisConfiguration _config;

    public RedisConfigurationBuilder(RedisConfiguration config)
    {
        _config = config;
    }

    public RedisConfigurationBuilder WithPort(int port)
    {
        _config = _config with { Port = port };
        return this;
    }

    public RedisConfigurationBuilder WithBindAddress(string bindAddress)
    {
        _config = _config with { BindAddress = bindAddress };
        return this;
    }

    public RedisConfigurationBuilder WithMaxMemory(string maxMemory)
    {
        _config = _config with { MaxMemory = maxMemory };
        return this;
    }

    public RedisConfigurationBuilder WithMaxMemoryPolicy(string maxMemoryPolicy)
    {
        _config = _config with { MaxMemoryPolicy = maxMemoryPolicy };
        return this;
    }

    public RedisConfigurationBuilder WithPersistence(bool enablePersistence)
    {
        _config = _config with { EnablePersistence = enablePersistence };
        return this;
    }

    public RedisConfigurationBuilder WithPersistenceMode(string persistenceMode)
    {
        _config = _config with { PersistenceMode = persistenceMode };
        return this;
    }

    public RedisConfigurationBuilder WithAOF(bool enableAOF)
    {
        _config = _config with { EnableAOF = enableAOF };
        return this;
    }

    public RedisConfigurationBuilder WithAuthentication(bool requirePassword, string password = "")
    {
        _config = _config with { RequirePassword = requirePassword, Password = password };
        return this;
    }

    public RedisConfigurationBuilder WithLogLevel(string logLevel)
    {
        _config = _config with { LogLevel = logLevel };
        return this;
    }

    public RedisConfiguration Build() => _config;
}

/// <summary>
/// Fluent builder for Windows Service settings.
/// </summary>
public sealed class ServiceSettingsBuilder
{
    private ServiceSettings _config;

    public ServiceSettingsBuilder(ServiceSettings config)
    {
        _config = config;
    }

    public ServiceSettingsBuilder WithServiceName(string serviceName)
    {
        _config = _config with { ServiceName = serviceName };
        return this;
    }

    public ServiceSettingsBuilder WithDisplayName(string displayName)
    {
        _config = _config with { DisplayName = displayName };
        return this;
    }

    public ServiceSettingsBuilder WithDescription(string description)
    {
        _config = _config with { Description = description };
        return this;
    }

    public ServiceSettingsBuilder WithStartType(string startType)
    {
        _config = _config with { StartType = startType };
        return this;
    }

    public ServiceSettingsBuilder WithDelayedAutoStart(bool delayedAutoStart)
    {
        _config = _config with { DelayedAutoStart = delayedAutoStart };
        return this;
    }

    public ServiceSettingsBuilder WithFailureActions(Action<FailureActionsSettingsBuilder> configureFailureActions)
    {
        var failureActionsBuilder = new FailureActionsSettingsBuilder(_config.FailureActions);
        configureFailureActions(failureActionsBuilder);
        _config = _config with { FailureActions = failureActionsBuilder.Build() };
        return this;
    }

    public ServiceSettings Build() => _config;
}

/// <summary>
/// Fluent builder for service failure actions.
/// </summary>
public sealed class FailureActionsSettingsBuilder
{
    private FailureActionsSettings _config;

    public FailureActionsSettingsBuilder(FailureActionsSettings config)
    {
        _config = config;
    }

    public FailureActionsSettingsBuilder WithResetPeriod(int resetPeriod)
    {
        _config = _config with { ResetPeriod = resetPeriod };
        return this;
    }

    public FailureActionsSettingsBuilder WithRestartDelay(int restartDelay)
    {
        _config = _config with { RestartDelay = restartDelay };
        return this;
    }

    public FailureActionsSettingsBuilder WithActions(params ServiceAction[] actions)
    {
        _config = _config with { Actions = toSeq(actions) };
        return this;
    }

    public FailureActionsSettings Build() => _config;
}

/// <summary>
/// Fluent builder for monitoring configuration.
/// </summary>
public sealed class MonitoringConfigurationBuilder
{
    private MonitoringConfiguration _config;

    public MonitoringConfigurationBuilder(MonitoringConfiguration config)
    {
        _config = config;
    }

    public MonitoringConfigurationBuilder WithHealthCheck(bool enableHealthCheck)
    {
        _config = _config with { EnableHealthCheck = enableHealthCheck };
        return this;
    }

    public MonitoringConfigurationBuilder WithHealthCheckInterval(int healthCheckInterval)
    {
        _config = _config with { HealthCheckInterval = healthCheckInterval };
        return this;
    }

    public MonitoringConfigurationBuilder WithHealthCheckTimeout(int healthCheckTimeout)
    {
        _config = _config with { HealthCheckTimeout = healthCheckTimeout };
        return this;
    }

    public MonitoringConfigurationBuilder WithWindowsEventLog(bool enableWindowsEventLog)
    {
        _config = _config with { EnableWindowsEventLog = enableWindowsEventLog };
        return this;
    }

    public MonitoringConfigurationBuilder WithEventLogSource(string eventLogSource)
    {
        _config = _config with { EventLogSource = eventLogSource };
        return this;
    }

    public MonitoringConfigurationBuilder WithFileLogging(bool enableFileLogging)
    {
        _config = _config with { EnableFileLogging = enableFileLogging };
        return this;
    }

    public MonitoringConfigurationBuilder WithLogFilePath(string logFilePath)
    {
        _config = _config with { LogFilePath = logFilePath };
        return this;
    }

    public MonitoringConfigurationBuilder WithLogLevel(string logLevel)
    {
        _config = _config with { LogLevel = logLevel };
        return this;
    }

    public MonitoringConfigurationBuilder WithMaxLogSizeMB(int maxLogSizeMB)
    {
        _config = _config with { MaxLogSizeMB = maxLogSizeMB };
        return this;
    }

    public MonitoringConfigurationBuilder WithMaxLogFiles(int maxLogFiles)
    {
        _config = _config with { MaxLogFiles = maxLogFiles };
        return this;
    }

    public MonitoringConfiguration Build() => _config;
}

/// <summary>
/// Fluent builder for performance configuration.
/// </summary>
public sealed class PerformanceConfigurationBuilder
{
    private PerformanceConfiguration _config;

    public PerformanceConfigurationBuilder(PerformanceConfiguration config)
    {
        _config = config;
    }

    public PerformanceConfigurationBuilder WithAutoRestart(bool enableAutoRestart)
    {
        _config = _config with { EnableAutoRestart = enableAutoRestart };
        return this;
    }

    public PerformanceConfigurationBuilder WithMaxRestartAttempts(int maxRestartAttempts)
    {
        _config = _config with { MaxRestartAttempts = maxRestartAttempts };
        return this;
    }

    public PerformanceConfigurationBuilder WithRestartCooldown(int restartCooldown)
    {
        _config = _config with { RestartCooldown = restartCooldown };
        return this;
    }

    public PerformanceConfigurationBuilder WithMemoryWarningThreshold(int memoryWarningThreshold)
    {
        _config = _config with { MemoryWarningThreshold = memoryWarningThreshold };
        return this;
    }

    public PerformanceConfigurationBuilder WithMemoryErrorThreshold(int memoryErrorThreshold)
    {
        _config = _config with { MemoryErrorThreshold = memoryErrorThreshold };
        return this;
    }

    public PerformanceConfigurationBuilder WithSlowLogMonitoring(bool enableSlowLogMonitoring)
    {
        _config = _config with { EnableSlowLogMonitoring = enableSlowLogMonitoring };
        return this;
    }

    public PerformanceConfigurationBuilder WithSlowLogThreshold(int slowLogThreshold)
    {
        _config = _config with { SlowLogThreshold = slowLogThreshold };
        return this;
    }

    public PerformanceConfiguration Build() => _config;
}

/// <summary>
/// Fluent builder for advanced configuration.
/// </summary>
public sealed class AdvancedConfigurationBuilder
{
    private AdvancedConfiguration _config;

    public AdvancedConfigurationBuilder(AdvancedConfiguration config)
    {
        _config = config;
    }

    public AdvancedConfigurationBuilder WithCustomStartupArgs(params string[] customStartupArgs)
    {
        _config = _config with { CustomStartupArgs = toSeq(customStartupArgs) };
        return this;
    }

    public AdvancedConfigurationBuilder WithEnvironmentVariables(params (string key, string value)[] environmentVariables)
    {
        var envVars = environmentVariables.ToDictionary(kvp => kvp.key, kvp => kvp.value);
        _config = _config with { EnvironmentVariables = toMap(envVars) };
        return this;
    }

    public AdvancedConfigurationBuilder WithPreStartScript(string preStartScript)
    {
        _config = _config with { PreStartScript = preStartScript };
        return this;
    }

    public AdvancedConfigurationBuilder WithPostStartScript(string postStartScript)
    {
        _config = _config with { PostStartScript = postStartScript };
        return this;
    }

    public AdvancedConfigurationBuilder WithPreStopScript(string preStopScript)
    {
        _config = _config with { PreStopScript = preStopScript };
        return this;
    }

    public AdvancedConfigurationBuilder WithPostStopScript(string postStopScript)
    {
        _config = _config with { PostStopScript = postStopScript };
        return this;
    }

    public AdvancedConfiguration Build() => _config;
}

/// <summary>
/// Fluent builder for metadata configuration.
/// </summary>
public sealed class MetadataConfigurationBuilder
{
    private MetadataConfiguration _config;

    public MetadataConfigurationBuilder(MetadataConfiguration config)
    {
        _config = config;
    }

    public MetadataConfigurationBuilder WithConfigVersion(string configVersion)
    {
        _config = _config with { ConfigVersion = configVersion };
        return this;
    }

    public MetadataConfigurationBuilder WithCreatedBy(string createdBy)
    {
        _config = _config with { CreatedBy = createdBy };
        return this;
    }

    public MetadataConfigurationBuilder WithCreatedDate(DateTime createdDate)
    {
        _config = _config with { CreatedDate = createdDate };
        return this;
    }

    public MetadataConfigurationBuilder WithLastModifiedDate(DateTime lastModifiedDate)
    {
        _config = _config with { LastModifiedDate = lastModifiedDate };
        return this;
    }

    public MetadataConfigurationBuilder WithNotes(string notes)
    {
        _config = _config with { Notes = notes };
        return this;
    }

    public MetadataConfiguration Build() => _config;
}
