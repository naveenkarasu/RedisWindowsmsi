using LanguageExt;
using static LanguageExt.Prelude;
using System.Collections.Concurrent;

namespace RedisServiceWrapper.Logging;

/// <summary>
/// File-based logger using functional programming principles.
/// Thread-safe implementation with async I/O.
/// Side effects are isolated and explicit.
/// </summary>
public sealed class FileLogger : ILogger, IDisposable
{
    private readonly string _logFilePath;
    private readonly ConcurrentQueue<LogEntry> _logQueue;
    private readonly SemaphoreSlim _writeSemaphore;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Task _writerTask;
    private bool _disposed = false;

    /// <summary>
    /// Creates a FileLogger that writes to the specified path.
    /// Starts a background writer task for async logging.
    /// </summary>
    public FileLogger(string? logFilePath = null)
    {
        _logFilePath = logFilePath ?? Constants.ServiceLogPath;
        _logQueue = new ConcurrentQueue<LogEntry>();
        _writeSemaphore = new SemaphoreSlim(0);
        _cancellationTokenSource = new CancellationTokenSource();

        // Ensure log directory exists
        EnsureLogDirectoryExists();

        // Start background writer task
        _writerTask = Task.Run(() => WriteQueuedLogsAsync(_cancellationTokenSource.Token));
    }

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    public Unit LogInfo(string message) =>
        EnqueueLog(LogEntry.Info(message));

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    public Unit LogWarning(string message) =>
        EnqueueLog(LogEntry.Warning(message));

    /// <summary>
    /// Logs an error message with optional exception.
    /// </summary>
    public Unit LogError(string message, Exception? exception = null) =>
        EnqueueLog(LogEntry.Error(message, exception));

    /// <summary>
    /// Logs a debug message.
    /// </summary>
    public Unit LogDebug(string message) =>
        EnqueueLog(LogEntry.Debug(message));

    /// <summary>
    /// Logs a success message.
    /// </summary>
    public Unit LogSuccess(string message) =>
        EnqueueLog(LogEntry.Success(message));

    /// <summary>
    /// Enqueues a log entry for async writing (pure function with side effect).
    /// </summary>
    private Unit EnqueueLog(LogEntry entry)
    {
        if (_disposed)
            return unit;

        _logQueue.Enqueue(entry);
        _writeSemaphore.Release();
        return unit;
    }

    /// <summary>
    /// Background task that writes queued logs to file asynchronously.
    /// </summary>
    private async Task WriteQueuedLogsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested || !_logQueue.IsEmpty)
        {
            try
            {
                // Wait for logs to be available
                await _writeSemaphore.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);

                // Process all available logs
                await ProcessLogQueue();
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                // Log to console as fallback
                Console.Error.WriteLine($"Error writing logs: {ex.Message}");
            }
        }

        // Flush remaining logs on shutdown
        await ProcessLogQueue();
    }

    /// <summary>
    /// Processes all queued log entries (I/O operation wrapped in Try).
    /// </summary>
    private async Task ProcessLogQueue()
    {
        var logsToWrite = new List<string>();

        // Dequeue all available logs
        while (_logQueue.TryDequeue(out var entry))
        {
            logsToWrite.Add(entry.Format());
        }

        if (logsToWrite.Count == 0)
            return;

        // Write all logs in a single operation (more efficient)
        await WriteLogsToFile(logsToWrite);
    }

    /// <summary>
    /// Writes log entries to file with error handling.
    /// </summary>
    private async Task WriteLogsToFile(List<string> logs) =>
        await TryAsync(async () =>
        {
            var logText = string.Join(Environment.NewLine, logs) + Environment.NewLine;
            await File.AppendAllTextAsync(_logFilePath, logText);
            return unit;
        })
        .Match(
            Succ: _ => Task.CompletedTask,
            Fail: ex =>
            {
                Console.Error.WriteLine($"Failed to write to log file: {ex.Message}");
                return Task.CompletedTask;
            }
        );

    /// <summary>
    /// Ensures the log directory exists (side effect with error handling).
    /// </summary>
    private Unit EnsureLogDirectoryExists() =>
        Try(() =>
        {
            var directory = Path.GetDirectoryName(_logFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            return unit;
        })
        .Match(
            Succ: _ => unit,
            Fail: ex =>
            {
                Console.Error.WriteLine($"Failed to create log directory: {ex.Message}");
                return unit;
            }
        );

    /// <summary>
    /// Disposes resources and flushes remaining logs.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Signal writer task to stop
        _cancellationTokenSource.Cancel();

        // Wait for writer task to complete (with timeout)
        try
        {
            _writerTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
            // Ignore cancellation exceptions
        }

        _cancellationTokenSource.Dispose();
        _writeSemaphore.Dispose();
    }
}

/// <summary>
/// Factory for creating FileLogger instances with functional approach.
/// </summary>
public static class FileLoggerFactory
{
    /// <summary>
    /// Creates a FileLogger with Try wrapper (pure function returning I/O action).
    /// </summary>
    public static Try<FileLogger> Create(string? logFilePath = null) =>
        Try(() => new FileLogger(logFilePath));

    /// <summary>
    /// Creates a FileLogger or returns a fallback on failure.
    /// </summary>
    public static ILogger CreateOrFallback(
        string? logFilePath = null,
        Func<ILogger>? fallbackFactory = null) =>
        Create(logFilePath)
            .Match(
                Succ: logger => (ILogger)logger,
                Fail: ex =>
                {
                    Console.WriteLine($"Failed to create FileLogger: {ex.Message}");
                    return fallbackFactory?.Invoke() ?? new ConsoleLogger();
                }
            );

    /// <summary>
    /// Creates a FileLogger with rotation support (future enhancement).
    /// </summary>
    public static Try<FileLogger> CreateWithRotation(
        string? logFilePath = null,
        long maxFileSizeBytes = 50 * 1024 * 1024, // 50MB default
        int maxFileCount = 10) =>
        Try(() =>
        {
            // TODO: Implement log rotation
            // For now, just create regular FileLogger
            return new FileLogger(logFilePath);
        });
}

/// <summary>
/// Log rotation helper (future enhancement) - pure functions.
/// </summary>
public static class LogRotationHelper
{
    /// <summary>
    /// Checks if log rotation is needed (pure function).
    /// </summary>
    public static bool ShouldRotate(string filePath, long maxSizeBytes) =>
        File.Exists(filePath) && new FileInfo(filePath).Length > maxSizeBytes;

    /// <summary>
    /// Generates rotated file name (pure function).
    /// </summary>
    public static string GetRotatedFileName(string originalPath, int index) =>
        $"{Path.GetFileNameWithoutExtension(originalPath)}.{index}{Path.GetExtension(originalPath)}";

    /// <summary>
    /// Performs log rotation (I/O operation wrapped in Try).
    /// </summary>
    public static TryAsync<Unit> RotateLog(string filePath, int maxFiles) =>
        TryAsync(async () =>
        {
            if (!File.Exists(filePath))
                return unit;

            var directory = Path.GetDirectoryName(filePath) ?? ".";
            var baseName = Path.GetFileNameWithoutExtension(filePath);
            var extension = Path.GetExtension(filePath);

            // Rotate existing files
            for (int i = maxFiles - 1; i > 0; i--)
            {
                var currentFile = Path.Combine(directory, $"{baseName}.{i}{extension}");
                var nextFile = Path.Combine(directory, $"{baseName}.{i + 1}{extension}");

                if (File.Exists(currentFile))
                {
                    if (i == maxFiles - 1 && File.Exists(nextFile))
                    {
                        File.Delete(nextFile);
                    }
                    File.Move(currentFile, nextFile, true);
                }
            }

            // Move current file to .1
            var rotatedFile = Path.Combine(directory, $"{baseName}.1{extension}");
            File.Move(filePath, rotatedFile, true);

            return await Task.FromResult(unit);
        });
}

