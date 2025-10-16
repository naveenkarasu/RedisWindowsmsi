using LanguageExt;
using static LanguageExt.Prelude;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CustomLogger = RedisServiceWrapper.Logging.ILogger;

namespace RedisServiceWrapper.Configuration.Loading;

/// <summary>
/// Monitors configuration file changes and provides reactive notifications.
/// Uses FileSystemWatcher with debouncing to prevent excessive reloads.
/// Thread-safe and disposable.
/// </summary>
public sealed class ConfigurationWatcher : IDisposable
{
    private readonly FileSystemWatcher _fileWatcher;
    private readonly Subject<ConfigurationChangeEvent> _changeSubject;
    private readonly CustomLogger _logger;
    private readonly string _configPath;
    private readonly TimeSpan _debounceInterval;
    private bool _disposed = false;
    private readonly object _disposeLock = new object();

    /// <summary>
    /// Observable stream of configuration change events.
    /// Debounced to prevent excessive notifications.
    /// </summary>
    public IObservable<ConfigurationChangeEvent> ConfigurationChanges { get; }

    /// <summary>
    /// Creates a ConfigurationWatcher instance.
    /// </summary>
    /// <param name="configPath">Path to the configuration file to watch</param>
    /// <param name="logger">Logger for watcher operations</param>
    /// <param name="debounceInterval">Time to wait before processing changes (default: 1 second)</param>
    public ConfigurationWatcher(
        string configPath, 
        CustomLogger logger, 
        TimeSpan? debounceInterval = null)
    {
        _configPath = configPath ?? throw new ArgumentNullException(nameof(configPath));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _debounceInterval = debounceInterval ?? TimeSpan.FromSeconds(1);

        // Create the file watcher
        var directory = Path.GetDirectoryName(_configPath);
        var fileName = Path.GetFileName(_configPath);
        
        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
            throw new ArgumentException($"Invalid configuration path: {_configPath}");

        _fileWatcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
            EnableRaisingEvents = false // Will be enabled when Start() is called
        };

        // Create the change subject
        _changeSubject = new Subject<ConfigurationChangeEvent>();

        // Create debounced observable
        ConfigurationChanges = _changeSubject
            .Throttle(_debounceInterval)
            .DistinctUntilChanged(evt => evt.ChangeType)
            .Publish()
            .RefCount();

        // Subscribe to file system events
        _fileWatcher.Changed += OnFileChanged;
        _fileWatcher.Created += OnFileCreated;
        _fileWatcher.Deleted += OnFileDeleted;
        _fileWatcher.Renamed += OnFileRenamed;
        _fileWatcher.Error += OnFileWatcherError;

        _logger.LogInfo($"ConfigurationWatcher created for: {_configPath}");
    }

    /// <summary>
    /// Starts watching for configuration file changes.
    /// </summary>
    /// <returns>Try containing Unit on success or error</returns>
    public Try<Unit> Start() =>
        Try(() =>
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ConfigurationWatcher));

            if (!_fileWatcher.EnableRaisingEvents)
            {
                _fileWatcher.EnableRaisingEvents = true;
                _logger.LogSuccess($"ConfigurationWatcher started for: {_configPath}");
            }
            return unit;
        });

    /// <summary>
    /// Stops watching for configuration file changes.
    /// </summary>
    /// <returns>Try containing Unit on success or error</returns>
    public Try<Unit> Stop() =>
        Try(() =>
        {
            if (_disposed)
                return unit;

            if (_fileWatcher.EnableRaisingEvents)
            {
                _fileWatcher.EnableRaisingEvents = false;
                _logger.LogInfo($"ConfigurationWatcher stopped for: {_configPath}");
            }
            return unit;
        });

    /// <summary>
    /// Manually triggers a configuration change event.
    /// Useful for testing or manual reloads.
    /// </summary>
    /// <param name="changeType">Type of change to report</param>
    /// <param name="reason">Reason for the change</param>
    /// <returns>Unit for functional composition</returns>
    public Unit TriggerChange(ConfigurationChangeType changeType, string reason = "Manual trigger") =>
        Try(() =>
        {
            if (_disposed)
                return unit;

            var changeEvent = new ConfigurationChangeEvent(
                ChangeType: changeType,
                FilePath: _configPath,
                Timestamp: DateTime.UtcNow,
                Reason: reason
            );

            _changeSubject.OnNext(changeEvent);
            _logger.LogInfo($"Configuration change triggered: {changeType} - {reason}");
            return unit;
        }).IfFail(ex => _logger.LogError($"Failed to trigger configuration change: {ex.Message}", ex));

    /// <summary>
    /// Gets the current status of the watcher.
    /// </summary>
    /// <returns>Watcher status information</returns>
    public WatcherStatus GetStatus() =>
        new WatcherStatus(
            IsWatching: !_disposed && _fileWatcher.EnableRaisingEvents,
            ConfigPath: _configPath,
            DebounceInterval: _debounceInterval,
            IsDisposed: _disposed
        );

    #region Event Handlers

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (_disposed || e.FullPath != _configPath)
            return;

        _logger.LogDebug($"Configuration file changed: {e.FullPath}");
        EmitChangeEvent(ConfigurationChangeType.Modified, "File modified");
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        if (_disposed || e.FullPath != _configPath)
            return;

        _logger.LogInfo($"Configuration file created: {e.FullPath}");
        EmitChangeEvent(ConfigurationChangeType.Created, "File created");
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        if (_disposed || e.FullPath != _configPath)
            return;

        _logger.LogWarning($"Configuration file deleted: {e.FullPath}");
        EmitChangeEvent(ConfigurationChangeType.Deleted, "File deleted");
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        if (_disposed || e.FullPath != _configPath)
            return;

        _logger.LogInfo($"Configuration file renamed: {e.OldFullPath} -> {e.FullPath}");
        EmitChangeEvent(ConfigurationChangeType.Renamed, $"File renamed from {e.OldName}");
    }

    private void OnFileWatcherError(object sender, ErrorEventArgs e)
    {
        if (_disposed)
            return;

        _logger.LogError($"FileSystemWatcher error: {e.GetException().Message}", e.GetException());
        
        // Try to restart the watcher
        Try(() =>
        {
            _fileWatcher.EnableRaisingEvents = false;
            Thread.Sleep(1000); // Wait a bit before restarting
            _fileWatcher.EnableRaisingEvents = true;
            _logger.LogInfo("FileSystemWatcher restarted after error");
        }).IfFail(ex => _logger.LogError($"Failed to restart FileSystemWatcher: {ex.Message}", ex));
    }

    private void EmitChangeEvent(ConfigurationChangeType changeType, string reason)
    {
        try
        {
            var changeEvent = new ConfigurationChangeEvent(
                ChangeType: changeType,
                FilePath: _configPath,
                Timestamp: DateTime.UtcNow,
                Reason: reason
            );

            _changeSubject.OnNext(changeEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to emit configuration change event: {ex.Message}", ex);
        }
    }

    #endregion

    /// <summary>
    /// Disposes the ConfigurationWatcher and stops all monitoring.
    /// </summary>
    public void Dispose()
    {
        lock (_disposeLock)
        {
            if (_disposed)
                return;

            _disposed = true;

            // Stop the file watcher
            Try(() => _fileWatcher.EnableRaisingEvents = false)
                .IfFail(ex => _logger.LogError($"Error stopping FileSystemWatcher: {ex.Message}", ex));

            // Dispose the file watcher
            Try(() => _fileWatcher.Dispose())
                .IfFail(ex => _logger.LogError($"Error disposing FileSystemWatcher: {ex.Message}", ex));

            // Complete the subject
            Try(() => _changeSubject.OnCompleted())
                .IfFail(ex => _logger.LogError($"Error completing change subject: {ex.Message}", ex));

            // Dispose the subject
            Try(() => _changeSubject.Dispose())
                .IfFail(ex => _logger.LogError($"Error disposing change subject: {ex.Message}", ex));

            _logger.LogInfo($"ConfigurationWatcher disposed for: {_configPath}");
        }
    }
}

/// <summary>
/// Represents a configuration file change event.
/// </summary>
public sealed record ConfigurationChangeEvent(
    ConfigurationChangeType ChangeType,
    string FilePath,
    DateTime Timestamp,
    string Reason
)
{
    /// <summary>
    /// Returns a string representation of the change event.
    /// </summary>
    public override string ToString() =>
        $"[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] {ChangeType}: {Path.GetFileName(FilePath)} - {Reason}";
}

/// <summary>
/// Types of configuration file changes.
/// </summary>
public enum ConfigurationChangeType
{
    /// <summary>
    /// File was modified.
    /// </summary>
    Modified,
    
    /// <summary>
    /// File was created.
    /// </summary>
    Created,
    
    /// <summary>
    /// File was deleted.
    /// </summary>
    Deleted,
    
    /// <summary>
    /// File was renamed.
    /// </summary>
    Renamed
}

/// <summary>
/// Status information for the ConfigurationWatcher.
/// </summary>
public sealed record WatcherStatus(
    bool IsWatching,
    string ConfigPath,
    TimeSpan DebounceInterval,
    bool IsDisposed
)
{
    /// <summary>
    /// Returns a string representation of the watcher status.
    /// </summary>
    public override string ToString() =>
        $"Watcher[Watching={IsWatching}, Path={Path.GetFileName(ConfigPath)}, Debounce={DebounceInterval.TotalSeconds}s, Disposed={IsDisposed}]";
}

/// <summary>
/// Factory methods for creating ConfigurationWatcher instances.
/// </summary>
public static class ConfigurationWatcherFactory
{
    /// <summary>
    /// Creates a ConfigurationWatcher with default settings.
    /// </summary>
    public static Try<ConfigurationWatcher> Create(string configPath, CustomLogger logger) =>
        Try(() => new ConfigurationWatcher(configPath, logger));

    /// <summary>
    /// Creates a ConfigurationWatcher with custom debounce interval.
    /// </summary>
    public static Try<ConfigurationWatcher> CreateWithDebounce(
        string configPath, 
        CustomLogger logger, 
        TimeSpan debounceInterval) =>
        Try(() => new ConfigurationWatcher(configPath, logger, debounceInterval));

    /// <summary>
    /// Creates a ConfigurationWatcher for development (shorter debounce interval).
    /// </summary>
    public static Try<ConfigurationWatcher> CreateForDevelopment(string configPath, CustomLogger logger) =>
        Try(() => new ConfigurationWatcher(configPath, logger, TimeSpan.FromMilliseconds(500)));

    /// <summary>
    /// Creates a ConfigurationWatcher for production (longer debounce interval).
    /// </summary>
    public static Try<ConfigurationWatcher> CreateForProduction(string configPath, CustomLogger logger) =>
        Try(() => new ConfigurationWatcher(configPath, logger, TimeSpan.FromSeconds(2)));
}

/// <summary>
/// Extension methods for ConfigurationWatcher.
/// </summary>
public static class ConfigurationWatcherExtensions
{
    /// <summary>
    /// Starts the watcher and returns a Try for error handling.
    /// </summary>
    public static Try<Unit> StartSafely(this ConfigurationWatcher watcher) =>
        watcher.Start();

    /// <summary>
    /// Stops the watcher and returns a Try for error handling.
    /// </summary>
    public static Try<Unit> StopSafely(this ConfigurationWatcher watcher) =>
        watcher.Stop();

    /// <summary>
    /// Subscribes to configuration changes with error handling.
    /// </summary>
    public static IDisposable SubscribeSafely(
        this ConfigurationWatcher watcher,
        Action<ConfigurationChangeEvent> onNext,
        Action<Exception>? onError = null,
        Action? onCompleted = null)
    {
        return watcher.ConfigurationChanges.Subscribe(
            onNext,
            onError ?? (ex => { }), // Default: ignore errors
            onCompleted ?? (() => { }) // Default: do nothing on completion
        );
    }

    /// <summary>
    /// Filters configuration changes by type.
    /// </summary>
    public static IObservable<ConfigurationChangeEvent> FilterByType(
        this ConfigurationWatcher watcher,
        ConfigurationChangeType changeType) =>
        watcher.ConfigurationChanges.Where(evt => evt.ChangeType == changeType);
}
