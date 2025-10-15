namespace RedisServiceWrapper;

/// <summary>
/// Application-wide constants.
/// Immutable by design - all values are const or static readonly.
/// </summary>
public static class Constants
{
    #region Service Identity
    
    /// <summary>
    /// Windows Service name (internal identifier).
    /// </summary>
    public const string ServiceName = "Redis";
    
    /// <summary>
    /// Display name shown in Windows Services.
    /// </summary>
    public const string ServiceDisplayName = "Redis Server";
    
    /// <summary>
    /// Service description.
    /// </summary>
    public const string ServiceDescription = "Redis in-memory data structure store running on WSL2 or Docker";
    
    /// <summary>
    /// Application version.
    /// </summary>
    public const string Version = "1.0.0";
    
    #endregion

    #region Paths (Windows)
    
    /// <summary>
    /// Installation directory.
    /// </summary>
    public static readonly string InstallDirectory = 
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Redis");
    
    /// <summary>
    /// Configuration directory.
    /// </summary>
    public static readonly string ConfigDirectory = 
        Path.Combine(InstallDirectory, "conf");
    
    /// <summary>
    /// Data directory (ProgramData - shared across users).
    /// </summary>
    public static readonly string DataDirectory = 
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Redis", "data");
    
    /// <summary>
    /// Log directory.
    /// </summary>
    public static readonly string LogDirectory = 
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Redis", "logs");
    
    /// <summary>
    /// Backend configuration file path.
    /// </summary>
    public static readonly string BackendConfigPath = 
        Path.Combine(ConfigDirectory, "backend.json");
    
    /// <summary>
    /// Redis configuration file path.
    /// </summary>
    public static readonly string RedisConfigPath = 
        Path.Combine(ConfigDirectory, "redis.conf");
    
    /// <summary>
    /// Service log file path.
    /// </summary>
    public static readonly string ServiceLogPath = 
        Path.Combine(LogDirectory, "service.log");
    
    #endregion

    #region Redis Defaults
    
    /// <summary>
    /// Default Redis port.
    /// </summary>
    public const int DefaultRedisPort = 6379;
    
    /// <summary>
    /// Default bind address.
    /// </summary>
    public const string DefaultBindAddress = "127.0.0.1";
    
    /// <summary>
    /// Default maximum memory (512MB).
    /// </summary>
    public const string DefaultMaxMemory = "512mb";
    
    #endregion

    #region Health Monitoring
    
    /// <summary>
    /// Health check interval in seconds.
    /// </summary>
    public const int HealthCheckIntervalSeconds = 30;
    
    /// <summary>
    /// Health check timeout in seconds.
    /// </summary>
    public const int HealthCheckTimeoutSeconds = 5;
    
    /// <summary>
    /// Maximum restart attempts on failure.
    /// </summary>
    public const int MaxRestartAttempts = 3;
    
    /// <summary>
    /// Cooldown period between restart attempts (seconds).
    /// </summary>
    public const int RestartCooldownSeconds = 60;
    
    #endregion

    #region Backend Configuration
    
    /// <summary>
    /// WSL2 backend identifier.
    /// </summary>
    public const string BackendTypeWSL2 = "WSL2";
    
    /// <summary>
    /// Docker backend identifier.
    /// </summary>
    public const string BackendTypeDocker = "Docker";
    
    /// <summary>
    /// Default WSL distribution name.
    /// </summary>
    public const string DefaultWSLDistribution = "Ubuntu";
    
    /// <summary>
    /// Default Docker image.
    /// </summary>
    public const string DefaultDockerImage = "redis:7.2-alpine";
    
    /// <summary>
    /// Default Docker container name.
    /// </summary>
    public const string DefaultDockerContainerName = "redis-windows";
    
    #endregion

    #region Windows Event Log
    
    /// <summary>
    /// Event log source name.
    /// </summary>
    public const string EventLogSourceName = "Redis Service";
    
    /// <summary>
    /// Event log name.
    /// </summary>
    public const string EventLogName = "Application";
    
    #endregion

    #region Service Lifecycle
    
    /// <summary>
    /// Service start timeout in seconds.
    /// </summary>
    public const int ServiceStartTimeoutSeconds = 30;
    
    /// <summary>
    /// Service stop timeout in seconds.
    /// </summary>
    public const int ServiceStopTimeoutSeconds = 10;
    
    /// <summary>
    /// Graceful shutdown grace period in seconds.
    /// </summary>
    public const int GracefulShutdownSeconds = 5;
    
    #endregion

    #region WSL Paths (Linux)
    
    /// <summary>
    /// Default Redis server path in WSL.
    /// </summary>
    public const string DefaultWSLRedisServerPath = "/usr/bin/redis-server";
    
    /// <summary>
    /// Default Redis CLI path in WSL.
    /// </summary>
    public const string DefaultWSLRedisCliPath = "/usr/bin/redis-cli";
    
    /// <summary>
    /// WSL mount point for Windows drives.
    /// </summary>
    public const string WSLMountPoint = "/mnt";
    
    #endregion

    #region Process Management
    
    /// <summary>
    /// Process execution timeout in seconds.
    /// </summary>
    public const int ProcessExecutionTimeoutSeconds = 30;
    
    /// <summary>
    /// Maximum output buffer size for process execution.
    /// </summary>
    public const int MaxProcessOutputBufferSize = 4096;
    
    #endregion

    #region Error Messages
    
    /// <summary>
    /// Configuration file not found error message.
    /// </summary>
    public const string ErrorConfigNotFound = "Configuration file not found";
    
    /// <summary>
    /// Invalid configuration error message.
    /// </summary>
    public const string ErrorInvalidConfig = "Invalid configuration";
    
    /// <summary>
    /// Backend not available error message.
    /// </summary>
    public const string ErrorBackendNotAvailable = "Backend (WSL2 or Docker) not available";
    
    /// <summary>
    /// Redis connection failed error message.
    /// </summary>
    public const string ErrorRedisConnectionFailed = "Failed to connect to Redis";
    
    #endregion

    #region Helper Methods (Pure Functions)
    
    /// <summary>
    /// Converts a Windows path to WSL path (pure function).
    /// Example: C:\Program Files\Redis -> /mnt/c/Program Files/Redis
    /// </summary>
    public static string ToWSLPath(string windowsPath)
    {
        if (string.IsNullOrWhiteSpace(windowsPath))
            return windowsPath;

        // Convert backslashes to forward slashes
        var unixPath = windowsPath.Replace('\\', '/');
        
        // Handle drive letter (C: -> /mnt/c)
        if (unixPath.Length >= 2 && unixPath[1] == ':')
        {
            var driveLetter = char.ToLower(unixPath[0]);
            var pathWithoutDrive = unixPath.Substring(2);
            return $"{WSLMountPoint}/{driveLetter}{pathWithoutDrive}";
        }
        
        return unixPath;
    }
    
    /// <summary>
    /// Ensures a directory exists (side effect wrapped in pure interface).
    /// </summary>
    public static LanguageExt.TryAsync<string> EnsureDirectory(string path) =>
        LanguageExt.Prelude.TryAsync(async () =>
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return path;
        });
    
    #endregion
}

