using LanguageExt;
using static LanguageExt.Prelude;

namespace RedisServiceWrapper.Configuration;

/// <summary>
/// Main service configuration - immutable record.
/// Maps to backend.json structure using functional principles.
/// </summary>
public sealed record ServiceConfiguration
{
    public string BackendType { get; init; } = Constants.BackendTypeWSL2;
    public WslConfiguration Wsl { get; init; } = new();
    public DockerConfiguration Docker { get; init; } = new();
    public RedisConfiguration Redis { get; init; } = new();
    public ServiceSettings Service { get; init; } = new();
    public MonitoringConfiguration Monitoring { get; init; } = new();
    public PerformanceConfiguration Performance { get; init; } = new();
    public AdvancedConfiguration Advanced { get; init; } = new();
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
/// </summary>
public sealed record WslConfiguration
{
    public string Distribution { get; init; } = Constants.DefaultWSLDistribution;
    public string RedisPath { get; init; } = Constants.DefaultWSLRedisServerPath;
    public string RedisCliPath { get; init; } = Constants.DefaultWSLRedisCliPath;
    public string ConfigPath { get; init; } = "/etc/redis/redis.conf";
    public string DataPath { get; init; } = "/var/lib/redis";
    public string LogPath { get; init; } = "/var/log/redis/redis-server.log";
    public string PidFile { get; init; } = "/var/run/redis/redis-server.pid";
    public string WindowsDataPath { get; init; } = Constants.DataDirectory;
    public string WindowsConfigPath { get; init; } = Constants.RedisConfigPath;
    public bool AutoStartOnBoot { get; init; } = true;
    public int HealthCheckInterval { get; init; } = Constants.HealthCheckIntervalSeconds;
}

/// <summary>
/// Docker configuration - immutable record.
/// </summary>
public sealed record DockerConfiguration
{
    public string ImageName { get; init; } = Constants.DefaultDockerImage;
    public string ContainerName { get; init; } = Constants.DefaultDockerContainerName;
    public string PortMapping { get; init; } = "6379:6379";
    public Seq<string> VolumeMappings { get; init; } = Seq(
        $"{Constants.DataDirectory}:/data",
        $"{Constants.RedisConfigPath}:/usr/local/etc/redis/redis.conf"
    );
    public string NetworkMode { get; init; } = "default";
    public string RestartPolicy { get; init; } = "unless-stopped";
    public bool AutoStartOnBoot { get; init; } = true;
    public int HealthCheckInterval { get; init; } = Constants.HealthCheckIntervalSeconds;
    public ResourceLimits ResourceLimits { get; init; } = new();
}

/// <summary>
/// Docker resource limits - immutable record.
/// </summary>
public sealed record ResourceLimits
{
    public string Memory { get; init; } = "512m";
    public string Cpus { get; init; } = "1.0";
}

/// <summary>
/// Redis configuration - immutable record.
/// </summary>
public sealed record RedisConfiguration
{
    public int Port { get; init; } = Constants.DefaultRedisPort;
    public string BindAddress { get; init; } = Constants.DefaultBindAddress;
    public string MaxMemory { get; init; } = Constants.DefaultMaxMemory;
    public string MaxMemoryPolicy { get; init; } = "allkeys-lru";
    public bool EnablePersistence { get; init; } = true;
    public string PersistenceMode { get; init; } = "rdb";
    public bool EnableAOF { get; init; } = false;
    public bool RequirePassword { get; init; } = false;
    public string Password { get; init; } = string.Empty;
    public string LogLevel { get; init; } = "notice";

    /// <summary>
    /// Gets password as Option<string> (functional approach to nullable).
    /// </summary>
    public Option<string> GetPassword() =>
        RequirePassword && !string.IsNullOrWhiteSpace(Password)
            ? Some(Password)
            : None;
}

/// <summary>
/// Windows Service settings - immutable record.
/// </summary>
public sealed record ServiceSettings
{
    public string ServiceName { get; init; } = Constants.ServiceName;
    public string DisplayName { get; init; } = Constants.ServiceDisplayName;
    public string Description { get; init; } = Constants.ServiceDescription;
    public string StartType { get; init; } = "Automatic";
    public bool DelayedAutoStart { get; init; } = true;
    public FailureActionsSettings FailureActions { get; init; } = new();
}

/// <summary>
/// Service failure actions - immutable record.
/// </summary>
public sealed record FailureActionsSettings
{
    public int ResetPeriod { get; init; } = 86400; // 24 hours in seconds
    public int RestartDelay { get; init; } = 60000; // 1 minute in milliseconds
    public Seq<ServiceAction> Actions { get; init; } = Seq(
        new ServiceAction("restart", 60000),
        new ServiceAction("restart", 60000),
        new ServiceAction("restart", 60000)
    );
}

/// <summary>
/// Service action - immutable record.
/// </summary>
public sealed record ServiceAction(string Type, int Delay);

/// <summary>
/// Monitoring configuration - immutable record.
/// </summary>
public sealed record MonitoringConfiguration
{
    public bool EnableHealthCheck { get; init; } = true;
    public int HealthCheckInterval { get; init; } = Constants.HealthCheckIntervalSeconds;
    public int HealthCheckTimeout { get; init; } = Constants.HealthCheckTimeoutSeconds;
    public bool EnableWindowsEventLog { get; init; } = true;
    public string EventLogSource { get; init; } = Constants.EventLogSourceName;
    public bool EnableFileLogging { get; init; } = true;
    public string LogFilePath { get; init; } = Constants.ServiceLogPath;
    public string LogLevel { get; init; } = "Info";
    public int MaxLogSizeMB { get; init; } = 50;
    public int MaxLogFiles { get; init; } = 10;
}

/// <summary>
/// Performance configuration - immutable record.
/// </summary>
public sealed record PerformanceConfiguration
{
    public bool EnableAutoRestart { get; init; } = true;
    public int MaxRestartAttempts { get; init; } = Constants.MaxRestartAttempts;
    public int RestartCooldown { get; init; } = Constants.RestartCooldownSeconds;
    public int MemoryWarningThreshold { get; init; } = 80;
    public int MemoryErrorThreshold { get; init; } = 95;
    public bool EnableSlowLogMonitoring { get; init; } = true;
    public int SlowLogThreshold { get; init; } = 10000; // 10 seconds in milliseconds
}

/// <summary>
/// Advanced configuration - immutable record.
/// </summary>
public sealed record AdvancedConfiguration
{
    public Seq<string> CustomStartupArgs { get; init; } = default!;
    public Map<string, string> EnvironmentVariables { get; init; } = default!;
    public string PreStartScript { get; init; } = string.Empty;
    public string PostStartScript { get; init; } = string.Empty;
    public string PreStopScript { get; init; } = string.Empty;
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
}

/// <summary>
/// Metadata configuration - immutable record.
/// </summary>
public sealed record MetadataConfiguration
{
    public string ConfigVersion { get; init; } = "1.0.0";
    public string CreatedBy { get; init; } = "Redis Windows Installer";
    public DateTime? CreatedDate { get; init; }
    public DateTime? LastModifiedDate { get; init; }
    public string Notes { get; init; } = "This configuration is automatically generated during installation.";
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

