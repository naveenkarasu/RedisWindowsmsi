using LanguageExt;

namespace RedisServiceWrapper.Logging;

/// <summary>
/// Logging interface using functional programming principles.
/// All operations return Unit (similar to void but composable).
/// </summary>
public interface ILogger
{
    /// <summary>
    /// Logs an informational message.
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <returns>Unit for functional composition</returns>
    Unit LogInfo(string message);

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    /// <param name="message">The warning message</param>
    /// <returns>Unit for functional composition</returns>
    Unit LogWarning(string message);

    /// <summary>
    /// Logs an error message with optional exception.
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="exception">Optional exception details</param>
    /// <returns>Unit for functional composition</returns>
    Unit LogError(string message, Exception? exception = null);

    /// <summary>
    /// Logs a debug message.
    /// </summary>
    /// <param name="message">The debug message</param>
    /// <returns>Unit for functional composition</returns>
    Unit LogDebug(string message);

    /// <summary>
    /// Logs a success message.
    /// </summary>
    /// <param name="message">The success message</param>
    /// <returns>Unit for functional composition</returns>
    Unit LogSuccess(string message);
}

/// <summary>
/// Log level enumeration.
/// </summary>
public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Success = 2,
    Warning = 3,
    Error = 4
}

/// <summary>
/// Log entry record - immutable representation of a log event.
/// </summary>
public sealed record LogEntry(
    DateTime Timestamp,
    LogLevel Level,
    string Message,
    Option<Exception> Exception,
    string? Source = null)
{
    /// <summary>
    /// Creates an info log entry (pure function).
    /// </summary>
    public static LogEntry Info(string message, string? source = null) =>
        new(DateTime.UtcNow, LogLevel.Info, message, Option<Exception>.None, source);

    /// <summary>
    /// Creates a warning log entry (pure function).
    /// </summary>
    public static LogEntry Warning(string message, string? source = null) =>
        new(DateTime.UtcNow, LogLevel.Warning, message, Option<Exception>.None, source);

    /// <summary>
    /// Creates an error log entry (pure function).
    /// </summary>
    public static LogEntry Error(string message, Exception? exception = null, string? source = null) =>
        new(DateTime.UtcNow, LogLevel.Error, message, 
            exception != null ? Option<Exception>.Some(exception) : Option<Exception>.None, 
            source);

    /// <summary>
    /// Creates a debug log entry (pure function).
    /// </summary>
    public static LogEntry Debug(string message, string? source = null) =>
        new(DateTime.UtcNow, LogLevel.Debug, message, Option<Exception>.None, source);

    /// <summary>
    /// Creates a success log entry (pure function).
    /// </summary>
    public static LogEntry Success(string message, string? source = null) =>
        new(DateTime.UtcNow, LogLevel.Success, message, Option<Exception>.None, source);

    /// <summary>
    /// Formats the log entry as a string (pure function).
    /// </summary>
    public string Format() =>
        Exception.Match(
            Some: ex => $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level}] {Message}\n{ex.Message}\n{ex.StackTrace}",
            None: () => $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level}] {Message}"
        );

    /// <summary>
    /// Formats for Windows Event Log (pure function).
    /// </summary>
    public string FormatForEventLog() =>
        Exception.Match(
            Some: ex => $"{Message}\n\nException: {ex.GetType().Name}\nMessage: {ex.Message}\nStack Trace:\n{ex.StackTrace}",
            None: () => Message
        );
}

/// <summary>
/// Extension methods for functional composition.
/// </summary>
public static class LoggerExtensions
{
    /// <summary>
    /// Logs and returns the value (useful in pipelines).
    /// </summary>
    public static T LogAndReturn<T>(this ILogger logger, string message, T value)
    {
        logger.LogInfo(message);
        return value;
    }

    /// <summary>
    /// Logs a Try result and returns it (for error handling pipelines).
    /// </summary>
    public static Try<T> LogTry<T>(this ILogger logger, Try<T> tryValue, string successMessage, string errorMessage) =>
        tryValue.Match(
            Succ: value =>
            {
                logger.LogSuccess(successMessage);
                return tryValue;
            },
            Fail: ex =>
            {
                logger.LogError(errorMessage, ex);
                return tryValue;
            }
        );

    /// <summary>
    /// Logs an Either result (for Railway-oriented programming).
    /// </summary>
    public static Either<L, R> LogEither<L, R>(
        this ILogger logger, 
        Either<L, R> either, 
        Func<R, string> successMessage,
        Func<L, string> errorMessage) =>
        either.Match(
            Right: value =>
            {
                logger.LogSuccess(successMessage(value));
                return either;
            },
            Left: error =>
            {
                logger.LogError(errorMessage(error), null);
                return either;
            }
        );
}

