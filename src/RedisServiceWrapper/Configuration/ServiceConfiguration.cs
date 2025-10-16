using LanguageExt;
using static LanguageExt.Prelude;
using Newtonsoft.Json;
using System;

namespace RedisServiceWrapper.Configuration;

/// <summary>
/// Custom JSON converter for sensitive data that redacts values in serialization.
/// </summary>
public class SensitiveDataConverter : JsonConverter<string>
{
    public override void WriteJson(JsonWriter writer, string? value, JsonSerializer serializer)
    {
        // Always write as redacted for security
        writer.WriteValue("[REDACTED]");
    }

    public override string? ReadJson(JsonReader reader, Type objectType, string? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        // Read the actual value during deserialization
        return reader.Value?.ToString();
    }

    public override bool CanRead => true;
    public override bool CanWrite => true;
}

/// <summary>
/// Main service configuration - immutable record.
/// Maps to backend.json structure using functional principles.
/// </summary>
[JsonObject(MemberSerialization.OptIn)]
public sealed record ServiceConfiguration
{
    /// <summary>
    /// Schema version for configuration compatibility and migration.
    /// </summary>
    [JsonProperty("schemaVersion")]
    public string SchemaVersion { get; init; } = "1.0.0";
    
    /// <summary>
    /// The backend type to use for Redis execution.
    /// Valid values: "WSL2" or "Docker".
    /// </summary>
    [JsonProperty("backendType")]
    public string BackendType { get; init; } = Constants.BackendTypeWSL2;
    
    /// <summary>
    /// WSL2-specific configuration settings.
    /// Only used when BackendType is "WSL2".
    /// </summary>
    [JsonProperty("wsl")]
    public WslConfiguration Wsl { get; init; } = new();
    
    /// <summary>
    /// Docker-specific configuration settings.
    /// Only used when BackendType is "Docker".
    /// </summary>
    [JsonProperty("docker")]
    public DockerConfiguration Docker { get; init; } = new();
    
    /// <summary>
    /// Redis server configuration settings.
    /// Applied regardless of backend type.
    /// </summary>
    [JsonProperty("redis")]
    public RedisConfiguration Redis { get; init; } = new();
    
    /// <summary>
    /// Windows Service configuration settings.
    /// Controls how the service behaves in Windows.
    /// </summary>
    [JsonProperty("service")]
    public ServiceSettings Service { get; init; } = new();
    
    /// <summary>
    /// Health monitoring and alerting configuration.
    /// Controls health checks and failure detection.
    /// </summary>
    [JsonProperty("monitoring")]
    public MonitoringConfiguration Monitoring { get; init; } = new();
    
    /// <summary>
    /// Performance and resource management settings.
    /// Controls memory limits and restart behavior.
    /// </summary>
    [JsonProperty("performance")]
    public PerformanceConfiguration Performance { get; init; } = new();
    
    /// <summary>
    /// Advanced configuration options.
    /// Logging, debugging, and additional Redis arguments.
    /// </summary>
    [JsonProperty("advanced")]
    public AdvancedConfiguration Advanced { get; init; } = new();
    
    /// <summary>
    /// Metadata about the configuration file.
    /// Creation date, version, and other metadata.
    /// </summary>
    [JsonProperty("metadata")]
    public MetadataConfiguration Metadata { get; init; } = new();

    /// <summary>
    /// Validates the configuration using functional Either pattern.
    /// Returns Right(config) if valid, Left(errors) if invalid.
    /// </summary>
    public Either<Seq<string>, ServiceConfiguration> Validate() =>
        ValidateBackendType()
            .Bind(_ => ValidatePaths())
            .Bind(_ => ValidateRedisSettings())
            .Map(_ => this);

    /// <summary>
    /// Checks if the configuration is using WSL2 backend.
    /// </summary>
    public bool IsWSL2Backend() => BackendType == Constants.BackendTypeWSL2;

    /// <summary>
    /// Checks if the configuration is using Docker backend.
    /// </summary>
    public bool IsDockerBackend() => BackendType == Constants.BackendTypeDocker;

    /// <summary>
    /// Gets the appropriate backend configuration based on BackendType.
    /// Returns None if backend type is invalid.
    /// </summary>
    public Option<object> GetBackendConfiguration() =>
        BackendType switch
        {
            Constants.BackendTypeWSL2 => Some<object>(Wsl),
            Constants.BackendTypeDocker => Some<object>(Docker),
            _ => None
        };

    /// <summary>
    /// Checks if Redis authentication is required.
    /// </summary>
    public bool RequiresAuthentication() => Redis.RequirePassword;

    /// <summary>
    /// Checks if the configuration has any custom startup arguments.
    /// </summary>
    public bool HasCustomStartupArgs() => Advanced.HasCustomStartupArgs();

    /// <summary>
    /// Checks if the configuration has any environment variables.
    /// </summary>
    public bool HasEnvironmentVariables() => Advanced.HasEnvironmentVariables();

    /// <summary>
    /// Gets a summary of the configuration for logging purposes.
    /// Excludes sensitive information like passwords.
    /// </summary>
    public string GetConfigurationSummary() =>
        $"Backend: {BackendType}, Port: {Redis.Port}, Auth: {RequiresAuthentication()}, " +
        $"HealthCheck: {Monitoring.EnableHealthCheck}, AutoRestart: {Performance.EnableAutoRestart}";

    private Either<Seq<string>, Unit> ValidateBackendType()
    {
        var validTypes = Seq(Constants.BackendTypeWSL2, Constants.BackendTypeDocker);
        return validTypes.Contains(BackendType)
            ? Right<Seq<string>, Unit>(unit)
            : Left<Seq<string>, Unit>(Seq1($"Invalid backend type: {BackendType}. Must be WSL2 or Docker."));
    }

    private Either<Seq<string>, Unit> ValidatePaths() =>
        Right<Seq<string>, Unit>(unit); // TODO: Add path validation

    private Either<Seq<string>, Unit> ValidateRedisSettings()
    {
        var errors = new List<string>();

        if (Redis.Port < 1 || Redis.Port > 65535)
            errors.Add($"Invalid Redis port: {Redis.Port}");

        return errors.Count == 0
            ? Right<Seq<string>, Unit>(unit)
            : Left<Seq<string>, Unit>(errors.ToSeq());
    }
}

/// <summary>
/// WSL2 configuration - immutable record.
/// Contains all settings specific to running Redis within WSL2.
/// </summary>
[JsonObject(MemberSerialization.OptIn)]
public sealed record WslConfiguration
{
    /// <summary>
    /// The WSL2 distribution to use (e.g., "Ubuntu", "Debian").
    /// Must be installed and available on the system.
    /// </summary>
    [JsonProperty("distribution")]
    public string Distribution { get; init; } = Constants.DefaultWSLDistribution;
    
    /// <summary>
    /// Path to the Redis server executable within WSL2.
    /// Default: "/usr/bin/redis-server"
    /// </summary>
    [JsonProperty("redisPath")]
    public string RedisPath { get; init; } = Constants.DefaultWSLRedisServerPath;
    
    /// <summary>
    /// Path to the Redis CLI executable within WSL2.
    /// Default: "/usr/bin/redis-cli"
    /// </summary>
    [JsonProperty("redisCliPath")]
    public string RedisCliPath { get; init; } = Constants.DefaultWSLRedisCliPath;
    
    /// <summary>
    /// Path to the Redis configuration file within WSL2.
    /// Default: "/etc/redis/redis.conf"
    /// </summary>
    public string ConfigPath { get; init; } = "/etc/redis/redis.conf";
    
    /// <summary>
    /// Path to the Redis data directory within WSL2.
    /// Default: "/var/lib/redis"
    /// </summary>
    public string DataPath { get; init; } = "/var/lib/redis";
    
    /// <summary>
    /// Path to the Redis log file within WSL2.
    /// Default: "/var/log/redis/redis-server.log"
    /// </summary>
    public string LogPath { get; init; } = "/var/log/redis/redis-server.log";
    
    /// <summary>
    /// Path to the Redis PID file within WSL2.
    /// Default: "/var/run/redis/redis-server.pid"
    /// </summary>
    public string PidFile { get; init; } = "/var/run/redis/redis-server.pid";
    
    /// <summary>
    /// Windows path that maps to the WSL2 data directory.
    /// Used for data persistence across WSL2 restarts.
    /// </summary>
    public string WindowsDataPath { get; init; } = Constants.DataDirectory;
    
    /// <summary>
    /// Windows path that maps to the WSL2 configuration file.
    /// Used for configuration management from Windows.
    /// </summary>
    public string WindowsConfigPath { get; init; } = Constants.RedisConfigPath;
    
    /// <summary>
    /// Whether to automatically start Redis when WSL2 boots.
    /// Default: true
    /// </summary>
    public bool AutoStartOnBoot { get; init; } = true;
    
    /// <summary>
    /// Interval in seconds between health checks for WSL2 Redis.
    /// Default: 30 seconds
    /// </summary>
    public int HealthCheckInterval { get; init; } = Constants.HealthCheckIntervalSeconds;

    /// <summary>
    /// Checks if the WSL distribution is valid (not empty).
    /// </summary>
    public bool HasValidDistribution() => !string.IsNullOrWhiteSpace(Distribution);

    /// <summary>
    /// Checks if the Redis path is valid (not empty).
    /// </summary>
    public bool HasValidRedisPath() => !string.IsNullOrWhiteSpace(RedisPath);

    /// <summary>
    /// Checks if the Redis CLI path is valid (not empty).
    /// </summary>
    public bool HasValidRedisCliPath() => !string.IsNullOrWhiteSpace(RedisCliPath);

    /// <summary>
    /// Gets the Windows path for data directory.
    /// </summary>
    public string GetWindowsDataPath() => WindowsDataPath;

    /// <summary>
    /// Gets the Windows path for configuration file.
    /// </summary>
    public string GetWindowsConfigPath() => WindowsConfigPath;

    /// <summary>
    /// Gets a summary of WSL configuration for logging.
    /// </summary>
    public string GetWslSummary() =>
        $"Distribution: {Distribution}, RedisPath: {RedisPath}, AutoStart: {AutoStartOnBoot}";
}

/// <summary>
/// Docker configuration - immutable record.
/// Contains all settings specific to running Redis within Docker containers.
/// </summary>
public sealed record DockerConfiguration
{
    /// <summary>
    /// Docker image name to use for Redis container.
    /// Default: "redis:latest"
    /// </summary>
    public string ImageName { get; init; } = Constants.DefaultDockerImage;
    
    /// <summary>
    /// Name for the Docker container.
    /// Default: "redis-service"
    /// </summary>
    public string ContainerName { get; init; } = Constants.DefaultDockerContainerName;
    
    /// <summary>
    /// Port mapping in format "host:container".
    /// Default: "6379:6379"
    /// </summary>
    public string PortMapping { get; init; } = "6379:6379";
    
    /// <summary>
    /// Volume mappings in format "host:container".
    /// Maps Windows directories to container paths.
    /// </summary>
    public Seq<string> VolumeMappings { get; init; } = Seq(
        $"{Constants.DataDirectory}:/data",
        $"{Constants.RedisConfigPath}:/usr/local/etc/redis/redis.conf"
    );
    
    /// <summary>
    /// Docker network mode for the container.
    /// Default: "default"
    /// </summary>
    public string NetworkMode { get; init; } = "default";
    
    /// <summary>
    /// Docker restart policy for the container.
    /// Default: "unless-stopped"
    /// </summary>
    public string RestartPolicy { get; init; } = "unless-stopped";
    
    /// <summary>
    /// Whether to automatically start the container on system boot.
    /// Default: true
    /// </summary>
    public bool AutoStartOnBoot { get; init; } = true;
    
    /// <summary>
    /// Interval in seconds between health checks for Docker Redis.
    /// Default: 30 seconds
    /// </summary>
    public int HealthCheckInterval { get; init; } = Constants.HealthCheckIntervalSeconds;
    
    /// <summary>
    /// Resource limits for the Docker container.
    /// Controls memory and CPU usage.
    /// </summary>
    public ResourceLimits ResourceLimits { get; init; } = new();

    /// <summary>
    /// Checks if the Docker image name is valid (not empty).
    /// </summary>
    public bool HasValidImageName() => !string.IsNullOrWhiteSpace(ImageName);

    /// <summary>
    /// Checks if the container name is valid (not empty).
    /// </summary>
    public bool HasValidContainerName() => !string.IsNullOrWhiteSpace(ContainerName);

    /// <summary>
    /// Checks if the port mapping is valid (not empty).
    /// </summary>
    public bool HasValidPortMapping() => !string.IsNullOrWhiteSpace(PortMapping);

    /// <summary>
    /// Checks if volume mappings are configured.
    /// </summary>
    public bool HasVolumeMappings() => !VolumeMappings.IsEmpty;

    /// <summary>
    /// Gets the number of volume mappings.
    /// </summary>
    public int GetVolumeMappingCount() => VolumeMappings.Count;

    /// <summary>
    /// Gets a summary of Docker configuration for logging.
    /// </summary>
    public string GetDockerSummary() =>
        $"Image: {ImageName}, Container: {ContainerName}, Port: {PortMapping}, " +
        $"Volumes: {GetVolumeMappingCount()}, AutoStart: {AutoStartOnBoot}";
}

/// <summary>
/// Docker resource limits - immutable record.
/// Controls memory and CPU allocation for Docker containers.
/// </summary>
public sealed record ResourceLimits
{
    /// <summary>
    /// Memory limit for the container (e.g., "512m", "1g").
    /// Default: "512m"
    /// </summary>
    public string Memory { get; init; } = "512m";
    
    /// <summary>
    /// CPU limit for the container (e.g., "1.0", "0.5").
    /// Default: "1.0"
    /// </summary>
    public string Cpus { get; init; } = "1.0";
}

/// <summary>
/// Redis configuration - immutable record.
/// Contains all Redis server-specific settings.
/// </summary>
[JsonObject(MemberSerialization.OptIn)]
public sealed record RedisConfiguration
{
    /// <summary>
    /// Port number for Redis server to listen on.
    /// Default: 6379
    /// </summary>
    [JsonProperty("port")]
    public int Port { get; init; } = Constants.DefaultRedisPort;
    
    /// <summary>
    /// IP address for Redis server to bind to.
    /// Default: "127.0.0.1"
    /// </summary>
    [JsonProperty("bindAddress")]
    public string BindAddress { get; init; } = Constants.DefaultBindAddress;
    
    /// <summary>
    /// Maximum memory Redis can use (e.g., "512mb", "1gb").
    /// Default: "256mb"
    /// </summary>
    [JsonProperty("maxMemory")]
    public string MaxMemory { get; init; } = Constants.DefaultMaxMemory;
    
    /// <summary>
    /// Memory eviction policy when max memory is reached.
    /// Default: "allkeys-lru"
    /// </summary>
    public string MaxMemoryPolicy { get; init; } = "allkeys-lru";
    
    /// <summary>
    /// Whether to enable Redis persistence to disk.
    /// Default: true
    /// </summary>
    public bool EnablePersistence { get; init; } = true;
    
    /// <summary>
    /// Persistence mode: "rdb", "aof", or "both".
    /// Default: "rdb"
    /// </summary>
    public string PersistenceMode { get; init; } = "rdb";
    
    /// <summary>
    /// Whether to enable Append Only File (AOF) persistence.
    /// Default: false
    /// </summary>
    public bool EnableAOF { get; init; } = false;
    
    /// <summary>
    /// Whether Redis requires a password for authentication.
    /// Default: false
    /// </summary>
    public bool RequirePassword { get; init; } = false;
    
    /// <summary>
    /// Redis password for authentication (if required).
    /// Default: empty string
    /// </summary>
    [JsonProperty("password")]
    [JsonConverter(typeof(SensitiveDataConverter))]
    public string Password { get; init; } = string.Empty;
    
    /// <summary>
    /// Redis log level: "debug", "verbose", "notice", "warning".
    /// Default: "notice"
    /// </summary>
    public string LogLevel { get; init; } = "notice";

    /// <summary>
    /// Gets password as Option<string> (functional approach to nullable).
    /// </summary>
    public Option<string> GetPassword() =>
        RequirePassword && !string.IsNullOrWhiteSpace(Password)
            ? Some(Password)
            : None;

    /// <summary>
    /// Checks if the port is valid (1-65535).
    /// </summary>
    public bool HasValidPort() => Port >= 1 && Port <= 65535;

    /// <summary>
    /// Checks if the bind address is valid (not empty).
    /// </summary>
    public bool HasValidBindAddress() => !string.IsNullOrWhiteSpace(BindAddress);

    /// <summary>
    /// Checks if max memory is configured (not empty).
    /// </summary>
    public bool HasMaxMemoryConfigured() => !string.IsNullOrWhiteSpace(MaxMemory);

    /// <summary>
    /// Checks if persistence is enabled.
    /// </summary>
    public bool IsPersistenceEnabled() => EnablePersistence;

    /// <summary>
    /// Checks if AOF (Append Only File) is enabled.
    /// </summary>
    public bool IsAOFEnabled() => EnableAOF;

    /// <summary>
    /// Checks if authentication is required.
    /// </summary>
    public bool IsAuthenticationRequired() => RequirePassword;

    /// <summary>
    /// Gets a summary of Redis configuration for logging (excludes password).
    /// </summary>
    public string GetRedisSummary() =>
        $"Port: {Port}, Bind: {BindAddress}, MaxMemory: {MaxMemory}, " +
        $"Persistence: {EnablePersistence}, AOF: {EnableAOF}, Auth: {RequirePassword}";
}

/// <summary>
/// Windows Service settings - immutable record.
/// Controls how the Redis service behaves as a Windows Service.
/// </summary>
public sealed record ServiceSettings
{
    /// <summary>
    /// Internal service name used by Windows Service Manager.
    /// Default: "RedisServiceWrapper"
    /// </summary>
    public string ServiceName { get; init; } = Constants.ServiceName;
    
    /// <summary>
    /// Display name shown in Windows Services console.
    /// Default: "Redis Service Wrapper"
    /// </summary>
    public string DisplayName { get; init; } = Constants.ServiceDisplayName;
    
    /// <summary>
    /// Service description shown in Windows Services console.
    /// Default: "Redis Service Wrapper for Windows"
    /// </summary>
    public string Description { get; init; } = Constants.ServiceDescription;
    
    /// <summary>
    /// Service start type: "Automatic", "Manual", "Disabled".
    /// Default: "Automatic"
    /// </summary>
    public string StartType { get; init; } = "Automatic";
    
    /// <summary>
    /// Whether to use delayed auto-start for the service.
    /// Default: true
    /// </summary>
    public bool DelayedAutoStart { get; init; } = true;
    
    /// <summary>
    /// Failure action settings for service recovery.
    /// Controls what happens when the service fails.
    /// </summary>
    public FailureActionsSettings FailureActions { get; init; } = new();

    /// <summary>
    /// Checks if the service name is valid (not empty).
    /// </summary>
    public bool HasValidServiceName() => !string.IsNullOrWhiteSpace(ServiceName);

    /// <summary>
    /// Checks if the display name is valid (not empty).
    /// </summary>
    public bool HasValidDisplayName() => !string.IsNullOrWhiteSpace(DisplayName);

    /// <summary>
    /// Checks if the start type is valid.
    /// </summary>
    public bool HasValidStartType() => 
        StartType == "Automatic" || StartType == "Manual" || StartType == "Disabled";

    /// <summary>
    /// Checks if the service is configured for automatic start.
    /// </summary>
    public bool IsAutomaticStart() => StartType == "Automatic";

    /// <summary>
    /// Checks if the service is configured for manual start.
    /// </summary>
    public bool IsManualStart() => StartType == "Manual";

    /// <summary>
    /// Checks if the service is disabled.
    /// </summary>
    public bool IsDisabled() => StartType == "Disabled";

    /// <summary>
    /// Gets a summary of service settings for logging.
    /// </summary>
    public string GetServiceSummary() =>
        $"Name: {ServiceName}, Display: {DisplayName}, StartType: {StartType}, " +
        $"DelayedStart: {DelayedAutoStart}";
}

/// <summary>
/// Service failure actions - immutable record.
/// Defines what actions to take when the service fails.
/// </summary>
public sealed record FailureActionsSettings
{
    /// <summary>
    /// Time period in seconds after which failure count resets.
    /// Default: 86400 (24 hours)
    /// </summary>
    public int ResetPeriod { get; init; } = 86400; // 24 hours in seconds
    
    /// <summary>
    /// Delay in milliseconds before restarting the service.
    /// Default: 60000 (1 minute)
    /// </summary>
    public int RestartDelay { get; init; } = 60000; // 1 minute in milliseconds
    
    /// <summary>
    /// Sequence of actions to take on consecutive failures.
    /// Default: restart, restart, restart
    /// </summary>
    public Seq<ServiceAction> Actions { get; init; } = Seq(
        new ServiceAction("restart", 60000),
        new ServiceAction("restart", 60000),
        new ServiceAction("restart", 60000)
    );
}

/// <summary>
/// Service action - immutable record.
/// Represents a single action to take when a service fails.
/// </summary>
/// <param name="Type">Action type: "restart", "run_command", "reboot"</param>
/// <param name="Delay">Delay in milliseconds before executing the action</param>
public sealed record ServiceAction(string Type, int Delay);

/// <summary>
/// Monitoring configuration - immutable record.
/// Controls health monitoring, logging, and alerting settings.
/// </summary>
public sealed record MonitoringConfiguration
{
    /// <summary>
    /// Whether to enable health check monitoring.
    /// Default: true
    /// </summary>
    public bool EnableHealthCheck { get; init; } = true;
    
    /// <summary>
    /// Interval in seconds between health checks.
    /// Default: 30 seconds
    /// </summary>
    public int HealthCheckInterval { get; init; } = Constants.HealthCheckIntervalSeconds;
    
    /// <summary>
    /// Timeout in seconds for health check operations.
    /// Default: 10 seconds
    /// </summary>
    public int HealthCheckTimeout { get; init; } = Constants.HealthCheckTimeoutSeconds;
    
    /// <summary>
    /// Whether to enable Windows Event Log integration.
    /// Default: true
    /// </summary>
    public bool EnableWindowsEventLog { get; init; } = true;
    
    /// <summary>
    /// Event log source name for Windows Event Log.
    /// Default: "RedisServiceWrapper"
    /// </summary>
    public string EventLogSource { get; init; } = Constants.EventLogSourceName;
    
    /// <summary>
    /// Whether to enable file-based logging.
    /// Default: true
    /// </summary>
    public bool EnableFileLogging { get; init; } = true;
    
    /// <summary>
    /// Path to the service log file.
    /// Default: from Constants.ServiceLogPath
    /// </summary>
    public string LogFilePath { get; init; } = Constants.ServiceLogPath;
    
    /// <summary>
    /// Log level: "Debug", "Info", "Warning", "Error".
    /// Default: "Info"
    /// </summary>
    public string LogLevel { get; init; } = "Info";
    
    /// <summary>
    /// Maximum log file size in MB before rotation.
    /// Default: 50 MB
    /// </summary>
    public int MaxLogSizeMB { get; init; } = 50;
    
    /// <summary>
    /// Maximum number of log files to keep.
    /// Default: 10
    /// </summary>
    public int MaxLogFiles { get; init; } = 10;

    /// <summary>
    /// Checks if health check is enabled.
    /// </summary>
    public bool IsHealthCheckEnabled() => EnableHealthCheck;

    /// <summary>
    /// Checks if Windows Event Log is enabled.
    /// </summary>
    public bool IsWindowsEventLogEnabled() => EnableWindowsEventLog;

    /// <summary>
    /// Checks if file logging is enabled.
    /// </summary>
    public bool IsFileLoggingEnabled() => EnableFileLogging;

    /// <summary>
    /// Checks if the health check interval is valid (> 0).
    /// </summary>
    public bool HasValidHealthCheckInterval() => HealthCheckInterval > 0;

    /// <summary>
    /// Checks if the health check timeout is valid (> 0).
    /// </summary>
    public bool HasValidHealthCheckTimeout() => HealthCheckTimeout > 0;

    /// <summary>
    /// Checks if the log level is valid.
    /// </summary>
    public bool HasValidLogLevel() =>
        LogLevel == "Debug" || LogLevel == "Info" || LogLevel == "Warning" || LogLevel == "Error";

    /// <summary>
    /// Gets a summary of monitoring configuration for logging.
    /// </summary>
    public string GetMonitoringSummary() =>
        $"HealthCheck: {EnableHealthCheck}, EventLog: {EnableWindowsEventLog}, " +
        $"FileLog: {EnableFileLogging}, LogLevel: {LogLevel}";
}

/// <summary>
/// Performance configuration - immutable record.
/// Controls performance monitoring, restart behavior, and resource thresholds.
/// </summary>
public sealed record PerformanceConfiguration
{
    /// <summary>
    /// Whether to enable automatic restart on failure.
    /// Default: true
    /// </summary>
    public bool EnableAutoRestart { get; init; } = true;
    
    /// <summary>
    /// Maximum number of restart attempts before giving up.
    /// Default: from Constants.MaxRestartAttempts
    /// </summary>
    public int MaxRestartAttempts { get; init; } = Constants.MaxRestartAttempts;
    
    /// <summary>
    /// Cooldown period in seconds between restart attempts.
    /// Default: from Constants.RestartCooldownSeconds
    /// </summary>
    public int RestartCooldown { get; init; } = Constants.RestartCooldownSeconds;
    
    /// <summary>
    /// Memory usage percentage threshold for warnings.
    /// Default: 80%
    /// </summary>
    public int MemoryWarningThreshold { get; init; } = 80;
    
    /// <summary>
    /// Memory usage percentage threshold for errors.
    /// Default: 95%
    /// </summary>
    public int MemoryErrorThreshold { get; init; } = 95;
    
    /// <summary>
    /// Whether to enable slow log monitoring.
    /// Default: true
    /// </summary>
    public bool EnableSlowLogMonitoring { get; init; } = true;
    
    /// <summary>
    /// Slow log threshold in milliseconds.
    /// Default: 10000 (10 seconds)
    /// </summary>
    public int SlowLogThreshold { get; init; } = 10000; // 10 seconds in milliseconds

    /// <summary>
    /// Checks if auto restart is enabled.
    /// </summary>
    public bool IsAutoRestartEnabled() => EnableAutoRestart;

    /// <summary>
    /// Checks if the max restart attempts is valid (> 0).
    /// </summary>
    public bool HasValidMaxRestartAttempts() => MaxRestartAttempts > 0;

    /// <summary>
    /// Checks if the restart cooldown is valid (>= 0).
    /// </summary>
    public bool HasValidRestartCooldown() => RestartCooldown >= 0;

    /// <summary>
    /// Checks if the memory warning threshold is valid (0-100).
    /// </summary>
    public bool HasValidMemoryWarningThreshold() => MemoryWarningThreshold >= 0 && MemoryWarningThreshold <= 100;

    /// <summary>
    /// Checks if the memory error threshold is valid (0-100).
    /// </summary>
    public bool HasValidMemoryErrorThreshold() => MemoryErrorThreshold >= 0 && MemoryErrorThreshold <= 100;

    /// <summary>
    /// Checks if slow log monitoring is enabled.
    /// </summary>
    public bool IsSlowLogMonitoringEnabled() => EnableSlowLogMonitoring;

    /// <summary>
    /// Checks if the slow log threshold is valid (> 0).
    /// </summary>
    public bool HasValidSlowLogThreshold() => SlowLogThreshold > 0;

    /// <summary>
    /// Gets a summary of performance configuration for logging.
    /// </summary>
    public string GetPerformanceSummary() =>
        $"AutoRestart: {EnableAutoRestart}, MaxAttempts: {MaxRestartAttempts}, " +
        $"MemoryWarning: {MemoryWarningThreshold}%, SlowLog: {EnableSlowLogMonitoring}";
}

/// <summary>
/// Advanced configuration - immutable record.
/// Contains advanced settings for custom startup arguments, scripts, and environment variables.
/// </summary>
public sealed record AdvancedConfiguration
{
    /// <summary>
    /// Custom startup arguments to pass to Redis server.
    /// Default: empty sequence
    /// </summary>
    public Seq<string> CustomStartupArgs { get; init; } = default!;
    
    /// <summary>
    /// Environment variables to set for Redis process.
    /// Default: empty map
    /// </summary>
    public Map<string, string> EnvironmentVariables { get; init; } = default!;
    
    /// <summary>
    /// Script to execute before starting Redis.
    /// Default: empty string
    /// </summary>
    public string PreStartScript { get; init; } = string.Empty;
    
    /// <summary>
    /// Script to execute after Redis starts successfully.
    /// Default: empty string
    /// </summary>
    public string PostStartScript { get; init; } = string.Empty;
    
    /// <summary>
    /// Script to execute before stopping Redis.
    /// Default: empty string
    /// </summary>
    public string PreStopScript { get; init; } = string.Empty;
    
    /// <summary>
    /// Script to execute after Redis stops.
    /// Default: empty string
    /// </summary>
    public string PostStopScript { get; init; } = string.Empty;
    
    // Constructor with defaults
    public AdvancedConfiguration()
    {
        CustomStartupArgs = toSeq(new string[] { });
        EnvironmentVariables = toMap(new Dictionary<string, string>());
    }

    /// <summary>
    /// Checks if custom startup args are present (pure function).
    /// </summary>
    public bool HasCustomStartupArgs() => !CustomStartupArgs.IsEmpty;

    /// <summary>
    /// Checks if environment variables are configured (pure function).
    /// </summary>
    public bool HasEnvironmentVariables() => !EnvironmentVariables.IsEmpty;

    /// <summary>
    /// Checks if any scripts are configured.
    /// </summary>
    public bool HasScripts() =>
        !string.IsNullOrWhiteSpace(PreStartScript) ||
        !string.IsNullOrWhiteSpace(PostStartScript) ||
        !string.IsNullOrWhiteSpace(PreStopScript) ||
        !string.IsNullOrWhiteSpace(PostStopScript);

    /// <summary>
    /// Checks if pre-start script is configured.
    /// </summary>
    public bool HasPreStartScript() => !string.IsNullOrWhiteSpace(PreStartScript);

    /// <summary>
    /// Checks if post-start script is configured.
    /// </summary>
    public bool HasPostStartScript() => !string.IsNullOrWhiteSpace(PostStartScript);

    /// <summary>
    /// Checks if pre-stop script is configured.
    /// </summary>
    public bool HasPreStopScript() => !string.IsNullOrWhiteSpace(PreStopScript);

    /// <summary>
    /// Checks if post-stop script is configured.
    /// </summary>
    public bool HasPostStopScript() => !string.IsNullOrWhiteSpace(PostStopScript);

    /// <summary>
    /// Gets the number of custom startup arguments.
    /// </summary>
    public int GetCustomStartupArgsCount() => CustomStartupArgs.Count;

    /// <summary>
    /// Gets the number of environment variables.
    /// </summary>
    public int GetEnvironmentVariablesCount() => EnvironmentVariables.Count;

    /// <summary>
    /// Gets a summary of advanced configuration for logging.
    /// </summary>
    public string GetAdvancedSummary() =>
        $"CustomArgs: {GetCustomStartupArgsCount()}, EnvVars: {GetEnvironmentVariablesCount()}, " +
        $"Scripts: {GetScriptCount()}";

    /// <summary>
    /// Gets the number of configured scripts.
    /// </summary>
    private int GetScriptCount() =>
        (HasPreStartScript() ? 1 : 0) +
        (HasPostStartScript() ? 1 : 0) +
        (HasPreStopScript() ? 1 : 0) +
        (HasPostStopScript() ? 1 : 0);
}

/// <summary>
/// Metadata configuration - immutable record.
/// Contains metadata about the configuration file itself.
/// </summary>
public sealed record MetadataConfiguration
{
    /// <summary>
    /// Version of the configuration schema.
    /// Default: "1.0.0"
    /// </summary>
    public string ConfigVersion { get; init; } = "1.0.0";
    
    /// <summary>
    /// Name of the tool or user that created this configuration.
    /// Default: "Redis Windows Installer"
    /// </summary>
    public string CreatedBy { get; init; } = "Redis Windows Installer";
    
    /// <summary>
    /// Date and time when the configuration was created.
    /// Default: null (will be set during creation)
    /// </summary>
    public DateTime? CreatedDate { get; init; }
    
    /// <summary>
    /// Date and time when the configuration was last modified.
    /// Default: null (will be updated on changes)
    /// </summary>
    public DateTime? LastModifiedDate { get; init; }
    
    /// <summary>
    /// Additional notes or comments about the configuration.
    /// Default: "This configuration is automatically generated during installation."
    /// </summary>
    public string Notes { get; init; } = "This configuration is automatically generated during installation.";

    /// <summary>
    /// Checks if the configuration version is valid (not empty).
    /// </summary>
    public bool HasValidConfigVersion() => !string.IsNullOrWhiteSpace(ConfigVersion);

    /// <summary>
    /// Checks if the created by field is valid (not empty).
    /// </summary>
    public bool HasValidCreatedBy() => !string.IsNullOrWhiteSpace(CreatedBy);

    /// <summary>
    /// Checks if the created date is set.
    /// </summary>
    public bool HasCreatedDate() => CreatedDate.HasValue;

    /// <summary>
    /// Checks if the last modified date is set.
    /// </summary>
    public bool HasLastModifiedDate() => LastModifiedDate.HasValue;

    /// <summary>
    /// Gets the age of the configuration in days.
    /// Returns null if CreatedDate is not set.
    /// </summary>
    public int? GetConfigurationAgeInDays() =>
        CreatedDate.HasValue ? (int)(DateTime.UtcNow - CreatedDate.Value).TotalDays : null;

    /// <summary>
    /// Gets a summary of metadata for logging.
    /// </summary>
    public string GetMetadataSummary() =>
        $"Version: {ConfigVersion}, CreatedBy: {CreatedBy}, " +
        $"Created: {CreatedDate?.ToString("yyyy-MM-dd") ?? "Unknown"}, " +
        $"Modified: {LastModifiedDate?.ToString("yyyy-MM-dd") ?? "Unknown"}";
}

/// <summary>
/// Configuration loader using functional approach.
/// Returns Either<Error, Config> instead of throwing exceptions.
/// </summary>
public static class ConfigurationLoader
{
    /// <summary>
    /// Loads configuration from file using functional error handling.
    /// </summary>
    public static Either<string, ServiceConfiguration> LoadFromFile(string path) =>
        TryReadFile(path)
            .Bind(ParseJson)
            .Bind(config => config.Validate()
                .MapLeft(errors => string.Join(", ", errors))
            );

    /// <summary>
    /// Tries to read file content (pure function wrapping I/O).
    /// </summary>
    private static Either<string, string> TryReadFile(string path) =>
        Try(() => File.ReadAllText(path))
            .ToEither()
            .MapLeft(ex => $"Failed to read configuration file: {ex.Message}");

    /// <summary>
    /// Parses JSON to configuration (pure function).
    /// </summary>
    private static Either<string, ServiceConfiguration> ParseJson(string json) =>
        Try(() => System.Text.Json.JsonSerializer.Deserialize<ServiceConfiguration>(json))
            .ToEither()
            .MapLeft(ex => $"Failed to parse configuration: {ex.Message}")
            .Bind(config => config == null
                ? Left<string, ServiceConfiguration>("Configuration is null")
                : Right<string, ServiceConfiguration>(config)
            );
}

