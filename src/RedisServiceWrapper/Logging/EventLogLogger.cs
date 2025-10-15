using System.Diagnostics;
using LanguageExt;
using static LanguageExt.Prelude;

namespace RedisServiceWrapper.Logging;

/// <summary>
/// Windows Event Log implementation using functional programming principles.
/// Logs to Windows Application Event Log.
/// Side effects are isolated and explicit.
/// </summary>
public sealed class EventLogLogger : ILogger, IDisposable
{
    private readonly EventLog _eventLog;
    private readonly string _sourceName;
    private bool _disposed = false;

    /// <summary>
    /// Creates an EventLogLogger with the specified source name.
    /// Side effect: Creates event source if it doesn't exist (requires admin).
    /// </summary>
    public EventLogLogger(string sourceName = Constants.EventLogSourceName)
    {
        _sourceName = sourceName;
        _eventLog = new EventLog(Constants.EventLogName)
        {
            Source = _sourceName
        };

        // Ensure event source exists (wrapped in Try for safety)
        TryEnsureEventSource();
    }

    /// <summary>
    /// Logs an informational message to Event Log.
    /// </summary>
    public Unit LogInfo(string message) =>
        WriteToEventLog(LogEntry.Info(message, _sourceName), EventLogEntryType.Information);

    /// <summary>
    /// Logs a warning message to Event Log.
    /// </summary>
    public Unit LogWarning(string message) =>
        WriteToEventLog(LogEntry.Warning(message, _sourceName), EventLogEntryType.Warning);

    /// <summary>
    /// Logs an error message to Event Log.
    /// </summary>
    public Unit LogError(string message, Exception? exception = null) =>
        WriteToEventLog(LogEntry.Error(message, exception, _sourceName), EventLogEntryType.Error);

    /// <summary>
    /// Logs a debug message to Event Log (as Information).
    /// </summary>
    public Unit LogDebug(string message) =>
        WriteToEventLog(LogEntry.Debug(message, _sourceName), EventLogEntryType.Information);

    /// <summary>
    /// Logs a success message to Event Log (as Information).
    /// </summary>
    public Unit LogSuccess(string message) =>
        WriteToEventLog(LogEntry.Success(message, _sourceName), EventLogEntryType.Information);

    /// <summary>
    /// Writes to Event Log with error handling (side effect wrapped in Unit).
    /// </summary>
    private Unit WriteToEventLog(LogEntry entry, EventLogEntryType entryType) =>
        Try(() =>
        {
            if (_disposed)
                return unit;

            var formattedMessage = entry.FormatForEventLog();
            
            // Event Log has message size limit (32,766 characters)
            if (formattedMessage.Length > 32000)
            {
                formattedMessage = formattedMessage.Substring(0, 32000) + "\n\n[Message truncated]";
            }

            _eventLog.WriteEntry(formattedMessage, entryType);
            return unit;
        })
        .Match(
            Succ: _ => unit,
            Fail: ex =>
            {
                // Fallback to Console if Event Log fails
                Console.Error.WriteLine($"Failed to write to Event Log: {ex.Message}");
                Console.Error.WriteLine($"Original message: {entry.Message}");
                return unit;
            }
        );

    /// <summary>
    /// Ensures the event source exists (side effect with error handling).
    /// </summary>
    private Unit TryEnsureEventSource() =>
        Try(() =>
        {
            if (!EventLog.SourceExists(_sourceName))
            {
                // This requires administrator privileges
                EventLog.CreateEventSource(_sourceName, Constants.EventLogName);
            }
            return unit;
        })
        .Match(
            Succ: _ => unit,
            Fail: ex =>
            {
                // Silently fail if we don't have permission
                // Event source should be created during installation
                Console.WriteLine($"Note: Event source not created (may require admin): {ex.Message}");
                return unit;
            }
        );

    /// <summary>
    /// Disposes the EventLog resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _eventLog?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Factory for creating EventLogLogger instances.
/// </summary>
public static class EventLogLoggerFactory
{
    /// <summary>
    /// Creates an EventLogLogger with Try wrapper (pure function returning I/O action).
    /// </summary>
    public static Try<EventLogLogger> Create(string sourceName = Constants.EventLogSourceName) =>
        Try(() => new EventLogLogger(sourceName));

    /// <summary>
    /// Creates an EventLogLogger or returns a fallback logger on failure.
    /// </summary>
    public static ILogger CreateOrFallback(
        string sourceName = Constants.EventLogSourceName,
        Func<ILogger>? fallbackFactory = null) =>
        Create(sourceName)
            .Match(
                Succ: logger => (ILogger)logger,
                Fail: ex =>
                {
                    Console.WriteLine($"Failed to create EventLogLogger: {ex.Message}");
                    return fallbackFactory?.Invoke() ?? new ConsoleLogger();
                }
            );
}

/// <summary>
/// Simple console logger as fallback (functional implementation).
/// </summary>
public sealed class ConsoleLogger : ILogger
{
    public Unit LogInfo(string message)
    {
        Console.WriteLine($"[INFO] {message}");
        return unit;
    }

    public Unit LogWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[WARNING] {message}");
        Console.ResetColor();
        return unit;
    }

    public Unit LogError(string message, Exception? exception = null)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[ERROR] {message}");
        if (exception != null)
        {
            Console.WriteLine($"Exception: {exception.Message}");
            Console.WriteLine($"Stack Trace: {exception.StackTrace}");
        }
        Console.ResetColor();
        return unit;
    }

    public Unit LogDebug(string message)
    {
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine($"[DEBUG] {message}");
        Console.ResetColor();
        return unit;
    }

    public Unit LogSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[SUCCESS] {message}");
        Console.ResetColor();
        return unit;
    }
}

