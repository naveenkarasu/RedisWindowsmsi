using LanguageExt;
using static LanguageExt.Prelude;

namespace RedisServiceWrapper.Logging;

/// <summary>
/// Composite logger that delegates to multiple loggers using functional composition.
/// Implements the Composite pattern with functional error handling.
/// </summary>
public sealed class CompositeLogger : ILogger, IDisposable
{
    private readonly Seq<ILogger> _loggers;
    private bool _disposed = false;

    /// <summary>
    /// Creates a CompositeLogger with the specified loggers.
    /// Uses immutable sequence for functional composition.
    /// </summary>
    public CompositeLogger(params ILogger[] loggers)
    {
        _loggers = loggers.ToSeq();
    }

    /// <summary>
    /// Creates a CompositeLogger from a sequence of loggers.
    /// </summary>
    public CompositeLogger(Seq<ILogger> loggers)
    {
        _loggers = loggers;
    }

    /// <summary>
    /// Logs an informational message to all loggers.
    /// </summary>
    public Unit LogInfo(string message) =>
        DelegateToAllLoggers(logger => logger.LogInfo(message));

    /// <summary>
    /// Logs a warning message to all loggers.
    /// </summary>
    public Unit LogWarning(string message) =>
        DelegateToAllLoggers(logger => logger.LogWarning(message));

    /// <summary>
    /// Logs an error message to all loggers.
    /// </summary>
    public Unit LogError(string message, Exception? exception = null) =>
        DelegateToAllLoggers(logger => logger.LogError(message, exception));

    /// <summary>
    /// Logs a debug message to all loggers.
    /// </summary>
    public Unit LogDebug(string message) =>
        DelegateToAllLoggers(logger => logger.LogDebug(message));

    /// <summary>
    /// Logs a success message to all loggers.
    /// </summary>
    public Unit LogSuccess(string message) =>
        DelegateToAllLoggers(logger => logger.LogSuccess(message));

    /// <summary>
    /// Delegates a logging action to all loggers with error handling.
    /// If one logger fails, others still execute (fault tolerance).
    /// </summary>
    private Unit DelegateToAllLoggers(Func<ILogger, Unit> logAction)
    {
        if (_disposed)
            return unit;

        _loggers.Iter(logger =>
            Try(() => logAction(logger))
                .Match(
                    Succ: _ => unit,
                    Fail: ex =>
                    {
                        // Silently handle logger failures to prevent cascading failures
                        // Could log to console as last resort
                        Console.Error.WriteLine($"Logger {logger.GetType().Name} failed: {ex.Message}");
                        return unit;
                    }
                )
        );

        return unit;
    }

    /// <summary>
    /// Disposes all disposable loggers.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _loggers
            .Where(logger => logger is IDisposable)
            .Cast<IDisposable>()
            .Iter(disposable =>
                Try(() => disposable.Dispose())
                    .Match(
                        Succ: _ => unit,
                        Fail: ex =>
                        {
                            Console.Error.WriteLine($"Failed to dispose logger: {ex.Message}");
                            return unit;
                        }
                    )
            );
    }
}

/// <summary>
/// Factory for creating CompositeLogger instances with common configurations.
/// </summary>
public static class CompositeLoggerFactory
{
    /// <summary>
    /// Creates a CompositeLogger with Event Log and File logging.
    /// This is the default configuration for the Windows Service.
    /// </summary>
    public static Try<CompositeLogger> CreateDefault(
        string? logFilePath = null,
        string? eventSourceName = null) =>
        Try(() =>
        {
            var loggers = new List<ILogger>();

            // Add Event Log logger
            EventLogLoggerFactory.Create(eventSourceName ?? Constants.EventLogSourceName)
                .Match(
                    Succ: logger => loggers.Add(logger),
                    Fail: ex => Console.WriteLine($"Event Log logger not available: {ex.Message}")
                );

            // Add File logger
            FileLoggerFactory.Create(logFilePath)
                .Match(
                    Succ: logger => loggers.Add(logger),
                    Fail: ex => Console.WriteLine($"File logger not available: {ex.Message}")
                );

            // Ensure at least console logger if both fail
            if (loggers.Count == 0)
            {
                loggers.Add(new ConsoleLogger());
            }

            return new CompositeLogger(loggers.ToArray());
        });

    /// <summary>
    /// Creates a CompositeLogger for development (Console + File).
    /// </summary>
    public static Try<CompositeLogger> CreateForDevelopment(string? logFilePath = null) =>
        Try(() =>
        {
            var loggers = new List<ILogger>
            {
                new ConsoleLogger()
            };

            FileLoggerFactory.Create(logFilePath)
                .Match(
                    Succ: logger => loggers.Add(logger),
                    Fail: _ => { } // Console only if file fails
                );

            return new CompositeLogger(loggers.ToArray());
        });

    /// <summary>
    /// Creates a CompositeLogger for production (Event Log + File).
    /// </summary>
    public static Try<CompositeLogger> CreateForProduction(
        string? logFilePath = null,
        string? eventSourceName = null) =>
        CreateDefault(logFilePath, eventSourceName);

    /// <summary>
    /// Creates a CompositeLogger with all loggers (Console + File + Event Log).
    /// Useful for debugging.
    /// </summary>
    public static Try<CompositeLogger> CreateVerbose(
        string? logFilePath = null,
        string? eventSourceName = null) =>
        Try(() =>
        {
            var loggers = new List<ILogger>
            {
                new ConsoleLogger()
            };

            EventLogLoggerFactory.Create(eventSourceName ?? Constants.EventLogSourceName)
                .Match(
                    Succ: logger => loggers.Add(logger),
                    Fail: _ => { }
                );

            FileLoggerFactory.Create(logFilePath)
                .Match(
                    Succ: logger => loggers.Add(logger),
                    Fail: _ => { }
                );

            return new CompositeLogger(loggers.ToArray());
        });

    /// <summary>
    /// Creates a CompositeLogger that always succeeds (uses fallbacks).
    /// </summary>
    public static CompositeLogger CreateSafe(
        string? logFilePath = null,
        string? eventSourceName = null) =>
        CreateDefault(logFilePath, eventSourceName)
            .Match(
                Succ: logger => logger,
                Fail: _ => new CompositeLogger(new ConsoleLogger())
            );
}

/// <summary>
/// Builder pattern for fluent CompositeLogger creation.
/// </summary>
public sealed class CompositeLoggerBuilder
{
    private readonly List<ILogger> _loggers = new();

    /// <summary>
    /// Adds a logger to the composite (fluent interface).
    /// </summary>
    public CompositeLoggerBuilder WithLogger(ILogger logger)
    {
        _loggers.Add(logger);
        return this;
    }

    /// <summary>
    /// Adds Event Log logger (fluent interface).
    /// </summary>
    public CompositeLoggerBuilder WithEventLog(string? sourceName = null)
    {
        EventLogLoggerFactory.Create(sourceName ?? Constants.EventLogSourceName)
            .Match(
                Succ: logger => _loggers.Add(logger),
                Fail: ex => Console.WriteLine($"Could not add Event Log: {ex.Message}")
            );
        return this;
    }

    /// <summary>
    /// Adds File logger (fluent interface).
    /// </summary>
    public CompositeLoggerBuilder WithFileLog(string? logFilePath = null)
    {
        FileLoggerFactory.Create(logFilePath)
            .Match(
                Succ: logger => _loggers.Add(logger),
                Fail: ex => Console.WriteLine($"Could not add File Log: {ex.Message}")
            );
        return this;
    }

    /// <summary>
    /// Adds Console logger (fluent interface).
    /// </summary>
    public CompositeLoggerBuilder WithConsole()
    {
        _loggers.Add(new ConsoleLogger());
        return this;
    }

    /// <summary>
    /// Builds the CompositeLogger.
    /// </summary>
    public CompositeLogger Build() =>
        _loggers.Count > 0
            ? new CompositeLogger(_loggers.ToArray())
            : new CompositeLogger(new ConsoleLogger()); // Fallback to console

    /// <summary>
    /// Builds with Try wrapper for functional composition.
    /// </summary>
    public Try<CompositeLogger> TryBuild() =>
        Try(() => Build());
}

/// <summary>
/// Extension methods for creating CompositeLogger fluently.
/// </summary>
public static class CompositeLoggerExtensions
{
    /// <summary>
    /// Starts building a CompositeLogger.
    /// Usage: CompositeLogger.Create().WithEventLog().WithFileLog().Build()
    /// </summary>
    public static CompositeLoggerBuilder CreateBuilder() =>
        new CompositeLoggerBuilder();
}

