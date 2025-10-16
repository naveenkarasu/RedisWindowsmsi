using LanguageExt;
using static LanguageExt.Prelude;
using System.Collections.Concurrent;

namespace RedisServiceWrapper.Configuration.Loading;

/// <summary>
/// Thread-safe configuration cache using functional programming principles.
/// Uses Atom<T> from LanguageExt for lock-free atomic operations.
/// Provides lazy loading and cache invalidation capabilities.
/// </summary>
public sealed class ConfigurationCache : IDisposable
{
    private readonly Atom<Option<ServiceConfiguration>> _cache;
    private readonly ConcurrentDictionary<string, DateTime> _fileTimestamps;
    private readonly object _disposeLock = new object();
    private bool _disposed = false;

    /// <summary>
    /// Creates a new ConfigurationCache instance.
    /// </summary>
    public ConfigurationCache()
    {
        _cache = Atom(Option<ServiceConfiguration>.None);
        _fileTimestamps = new ConcurrentDictionary<string, DateTime>();
    }

    /// <summary>
    /// Gets the cached configuration if available.
    /// </summary>
    /// <returns>Option containing cached configuration or None</returns>
    public Option<ServiceConfiguration> Get() =>
        _disposed ? Option<ServiceConfiguration>.None : _cache.Value;

    /// <summary>
    /// Sets the cached configuration.
    /// </summary>
    /// <param name="config">Configuration to cache</param>
    /// <returns>Unit for functional composition</returns>
    public Unit Set(ServiceConfiguration config)
    {
        if (_disposed) return unit;
        
        _cache.Swap(_ => Option<ServiceConfiguration>.Some(config));
        return unit;
    }

    /// <summary>
    /// Invalidates the cache (clears cached configuration).
    /// </summary>
    /// <returns>Unit for functional composition</returns>
    public Unit Invalidate()
    {
        if (_disposed) return unit;
        
        _cache.Swap(_ => Option<ServiceConfiguration>.None);
        return unit;
    }

    /// <summary>
    /// Gets cached configuration or loads it using the provided loader function.
    /// Thread-safe lazy loading with file timestamp checking.
    /// </summary>
    /// <param name="configPath">Path to configuration file</param>
    /// <param name="loader">Function to load configuration if not cached</param>
    /// <returns>TryAsync containing the configuration</returns>
    public TryAsync<ServiceConfiguration> GetOrLoadAsync(
        string configPath, 
        Func<TryAsync<ServiceConfiguration>> loader) =>
        TryAsync(async () =>
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ConfigurationCache));

            // Check if we have a cached configuration
            var cached = _cache.Value;
            if (cached.IsSome)
            {
                // Check if file has been modified since last cache
                if (ShouldReloadFromFile(configPath))
                {
                // File changed, reload
                var freshConfigResult = await loader();
                var freshConfig = freshConfigResult.IfFail(ex => throw ex);
                Set(freshConfig);
                UpdateFileTimestamp(configPath);
                return freshConfig;
                }
                
                // Return cached configuration
                return cached.IfNone(() => throw new Exception("Unreachable - cache was Some"));
            }

            // No cached configuration, load fresh
            var configResult = await loader();
            var config = configResult.IfFail(ex => throw ex);
            Set(config);
            UpdateFileTimestamp(configPath);
            return config;
        });

    /// <summary>
    /// Sets configuration in cache asynchronously.
    /// </summary>
    /// <param name="config">Configuration to cache</param>
    /// <returns>Task representing the async operation</returns>
    public async Task SetAsync(ServiceConfiguration config)
    {
        if (_disposed) return;
        
        await Task.Run(() => Set(config));
    }

    /// <summary>
    /// Checks if cache contains a configuration.
    /// </summary>
    /// <returns>True if configuration is cached</returns>
    public bool IsCached() =>
        !_disposed && _cache.Value.IsSome;

    /// <summary>
    /// Gets cache statistics for monitoring.
    /// </summary>
    /// <returns>Cache statistics</returns>
    public CacheStatistics GetStatistics() =>
        new CacheStatistics(
            IsCached: IsCached(),
            FileTimestampCount: _fileTimestamps.Count,
            Disposed: _disposed
        );

    /// <summary>
    /// Clears all cached data including file timestamps.
    /// </summary>
    /// <returns>Unit for functional composition</returns>
    public Unit ClearAll()
    {
        if (_disposed) return unit;
        
        _cache.Swap(_ => Option<ServiceConfiguration>.None);
        _fileTimestamps.Clear();
        return unit;
    }

    /// <summary>
    /// Checks if configuration file has been modified since last cache update.
    /// </summary>
    private bool ShouldReloadFromFile(string configPath)
    {
        if (!File.Exists(configPath))
            return false;

        var currentTimestamp = File.GetLastWriteTime(configPath);
        
        if (!_fileTimestamps.TryGetValue(configPath, out var cachedTimestamp))
            return true; // No cached timestamp, should reload

        return currentTimestamp > cachedTimestamp;
    }

    /// <summary>
    /// Updates the file timestamp in cache.
    /// </summary>
    private void UpdateFileTimestamp(string configPath)
    {
        if (!File.Exists(configPath))
            return;

        var timestamp = File.GetLastWriteTime(configPath);
        _fileTimestamps.AddOrUpdate(configPath, timestamp, (_, _) => timestamp);
    }

    /// <summary>
    /// Disposes the cache and clears all data.
    /// </summary>
    public void Dispose()
    {
        lock (_disposeLock)
        {
            if (_disposed)
                return;

            _disposed = true;
            _cache.Swap(_ => Option<ServiceConfiguration>.None);
            _fileTimestamps.Clear();
        }
    }
}

/// <summary>
/// Cache statistics for monitoring and debugging.
/// </summary>
public sealed record CacheStatistics(
    bool IsCached,
    int FileTimestampCount,
    bool Disposed
)
{
    /// <summary>
    /// Returns a string representation of cache statistics.
    /// </summary>
    public override string ToString() =>
        $"Cache[IsCached={IsCached}, TimestampCount={FileTimestampCount}, Disposed={Disposed}]";
}

/// <summary>
/// Factory methods for creating ConfigurationCache instances.
/// </summary>
public static class ConfigurationCacheFactory
{
    /// <summary>
    /// Creates a new ConfigurationCache instance.
    /// </summary>
    public static ConfigurationCache Create() => new();

    /// <summary>
    /// Creates a ConfigurationCache with Try wrapper for error handling.
    /// </summary>
    public static Try<ConfigurationCache> CreateSafe() =>
        Try(() => new ConfigurationCache());

    /// <summary>
    /// Creates a shared ConfigurationCache instance (singleton pattern).
    /// Useful for sharing cache across multiple ConfigurationManager instances.
    /// </summary>
    public static Lazy<ConfigurationCache> CreateShared() =>
        new Lazy<ConfigurationCache>(() => new ConfigurationCache());
}

/// <summary>
/// Extension methods for ConfigurationCache.
/// </summary>
public static class ConfigurationCacheExtensions
{
    /// <summary>
    /// Gets cached configuration or returns None if not cached.
    /// </summary>
    public static Option<ServiceConfiguration> TryGet(this ConfigurationCache cache) =>
        cache.IsCached() ? cache.Get() : Option<ServiceConfiguration>.None;

    /// <summary>
    /// Invalidates cache and returns the previous configuration if any.
    /// </summary>
    public static Option<ServiceConfiguration> InvalidateAndGetPrevious(this ConfigurationCache cache)
    {
        var previous = cache.Get();
        cache.Invalidate();
        return previous;
    }

    /// <summary>
    /// Updates cache only if the new configuration is different from cached.
    /// </summary>
    public static Unit UpdateIfChanged(this ConfigurationCache cache, ServiceConfiguration newConfig)
    {
        var cached = cache.Get();
        
        if (cached.IsNone)
        {
            cache.Set(newConfig);
            return unit;
        }

        var cachedConfig = cached.IfNone(() => throw new Exception("Unreachable"));
        
        // Simple comparison - in a real implementation, you might want to use
        // a more sophisticated comparison or implement IEquatable<ServiceConfiguration>
        if (!ReferenceEquals(cachedConfig, newConfig))
        {
            cache.Set(newConfig);
        }

        return unit;
    }

    /// <summary>
    /// Performs an action with the cached configuration if available.
    /// </summary>
    public static Unit WithCached<T>(this ConfigurationCache cache, Func<ServiceConfiguration, T> action)
    {
        var cached = cache.Get();
        cached.IfSome(config => action(config));
        return unit;
    }
}
