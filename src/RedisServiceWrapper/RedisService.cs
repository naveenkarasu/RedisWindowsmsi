using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedisServiceWrapper.Configuration;
using LanguageExt;
using static LanguageExt.Prelude;

namespace RedisServiceWrapper;

/// <summary>
/// Main Windows Service implementation using functional programming principles.
/// Implements BackgroundService for long-running operations.
/// Immutable state where possible, side effects isolated and explicit.
/// </summary>
public sealed class RedisService : BackgroundService
{
    private readonly ILogger<RedisService> _logger;
    private readonly ServiceConfiguration _configuration;
    private readonly Logging.ILogger _customLogger;

    // Mutable state (minimal, isolated)
    private int _restartAttempts = 0;
    private DateTime _lastRestartAttempt = DateTime.MinValue;

    /// <summary>
    /// Constructor with dependency injection.
    /// </summary>
    public RedisService(
        ILogger<RedisService> logger,
        IOptions<ServiceConfiguration> configuration,
        Logging.ILogger customLogger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration?.Value ?? throw new ArgumentNullException(nameof(configuration));
        _customLogger = customLogger ?? throw new ArgumentNullException(nameof(customLogger));
    }

    /// <summary>
    /// Executes the service asynchronously (main service loop).
    /// Uses functional approach with async/await and cancellation tokens.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await LogServiceStarting();

            // Initialize service using functional composition
            await InitializeService()
                .Match(
                    Succ: async _ => await RunServiceLoop(stoppingToken),
                    Fail: async ex => await HandleInitializationFailure(ex)
                );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _customLogger.LogError("Fatal error in service execution", ex);
            throw;
        }
    }

    /// <summary>
    /// Called when the service is stopping.
    /// Ensures graceful shutdown.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await LogServiceStopping();

        try
        {
            await PerformGracefulShutdown()
                .Match(
                    Succ: _ => _customLogger.LogInfo("Service stopped successfully"),
                    Fail: ex => _customLogger.LogError("Error during shutdown", ex)
                );
        }
        finally
        {
            await base.StopAsync(cancellationToken);
        }
    }

    #region Service Lifecycle (Functional)

    /// <summary>
    /// Initializes the service (I/O operation wrapped in Try).
    /// </summary>
    private TryAsync<Unit> InitializeService() =>
        TryAsync(async () =>
        {
            _customLogger.LogInfo($"Initializing {Constants.ServiceDisplayName}...");

            // Validate configuration
            var validationResult = await ValidateConfiguration();
            validationResult.IfFail(errors => throw new InvalidOperationException($"Configuration validation failed: {errors}"));

            // Ensure directories exist
            await EnsureDirectoriesExist();

            // Load backend configuration
            _customLogger.LogInfo($"Backend type: {_configuration.BackendType}");

            // TODO: Initialize backend manager (Task 3.8+)
            // TODO: Start Redis (Task 3.8+)
            // TODO: Start health monitoring (Task 3.16+)

            _customLogger.LogSuccess("Service initialized successfully");
            
            return unit;
        });

    /// <summary>
    /// Main service loop - keeps the service running.
    /// </summary>
    private async Task RunServiceLoop(CancellationToken cancellationToken)
    {
        _customLogger.LogInfo("Service is running. Press Ctrl+C to stop.");

        // Service loop - wait until cancellation is requested
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _customLogger.LogInfo("Service shutdown requested");
        }
    }

    /// <summary>
    /// Performs graceful shutdown of all components.
    /// </summary>
    private TryAsync<Unit> PerformGracefulShutdown() =>
        TryAsync(async () =>
        {
            _customLogger.LogInfo("Stopping service components...");

            // TODO: Stop health monitoring (Task 3.16+)
            // TODO: Stop Redis gracefully (Task 3.8+)
            // TODO: Cleanup resources (Task 3.21)

            // Give components time to shut down
            await Task.Delay(TimeSpan.FromSeconds(Constants.GracefulShutdownSeconds));

            return unit;
        });

    /// <summary>
    /// Handles initialization failure with logging.
    /// </summary>
    private async Task HandleInitializationFailure(Exception ex)
    {
        _customLogger.LogError("Service initialization failed", ex);
        _logger.LogCritical(ex, "Service failed to initialize");
        
        // Allow host to handle the failure
        await Task.CompletedTask;
    }

    #endregion

    #region Configuration and Validation (Pure Functions)

    /// <summary>
    /// Validates configuration asynchronously.
    /// </summary>
    private TryAsync<Unit> ValidateConfiguration() =>
        TryAsync(async () =>
        {
            _customLogger.LogInfo("Validating configuration...");

            var validationResult = _configuration.Validate();
            
            return await validationResult
                .Match(
                    Right: _ =>
                    {
                        _customLogger.LogSuccess("Configuration is valid");
                        return Task.FromResult(unit);
                    },
                    Left: errors =>
                    {
                        var errorMessage = string.Join(", ", errors);
                        _customLogger.LogError($"Configuration validation failed: {errorMessage}", null);
                        throw new InvalidOperationException(errorMessage);
                    }
                );
        });

    /// <summary>
    /// Ensures all required directories exist.
    /// </summary>
    private async Task EnsureDirectoriesExist()
    {
        var directories = Seq(
            Constants.LogDirectory,
            Constants.DataDirectory,
            Constants.ConfigDirectory
        );

        await directories
            .Map(Constants.EnsureDirectory)
            .TraverseSerial(identity)
            .Match(
                Succ: _ => _customLogger.LogInfo("All directories verified"),
                Fail: ex => _customLogger.LogWarning($"Directory creation warning: {ex.Message}")
            );
    }

    #endregion

    #region Logging Helpers (Side Effects)

    /// <summary>
    /// Logs service starting event.
    /// </summary>
    private async Task LogServiceStarting()
    {
        _customLogger.LogInfo($"{Constants.ServiceDisplayName} v{Constants.Version} starting...");
        _logger.LogInformation("Service starting");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Logs service stopping event.
    /// </summary>
    private async Task LogServiceStopping()
    {
        _customLogger.LogInfo("Service stopping...");
        _logger.LogInformation("Service stopping");
        await Task.CompletedTask;
    }

    #endregion

    #region Health and Recovery (To be implemented)

    // TODO: Task 3.16 - Implement health checking
    // TODO: Task 3.18 - Implement auto-recovery
    // private async Task MonitorHealth(CancellationToken cancellationToken) { }
    // private async Task HandleHealthCheckFailure() { }
    // private async Task AttemptAutoRestart() { }

    #endregion

    #region Backend Management (To be implemented)

    // TODO: Task 3.8-3.11 - Implement backend management
    // private IRedisBackend _backend;
    // private async Task StartRedis() { }
    // private async Task StopRedis() { }
    // private async Task RestartRedis() { }

    #endregion
}

