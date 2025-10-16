using System;
using System.Diagnostics;
using System.Threading.Tasks;
using RedisServiceWrapper.Configuration;
using RedisServiceWrapper.Logging;
using LanguageExt;
using CustomLogger = RedisServiceWrapper.Logging.ILogger;

namespace RedisServiceWrapper.Backend
{
    /// <summary>
    /// WSL2-based Redis backend implementation.
    /// Provides Redis deployment using WSL2 (Windows Subsystem for Linux 2).
    /// </summary>
    public sealed class WslRedisBackend : BaseRedisBackend
    {
        private WslConfiguration _wslConfig;
        private Process? _wslProcess;
        private readonly object _processLock = new object();

        /// <summary>
        /// Gets the backend type identifier.
        /// </summary>
        public override string BackendType => "WSL2";

        /// <summary>
        /// Initializes the WSL2 backend.
        /// </summary>
        protected override async Task OnInitializeAsync()
        {
            _wslConfig = Configuration.Wsl ?? throw new InvalidOperationException("WSL configuration is required");
            
            Logger.LogInfo($"Initializing WSL2 backend for distribution: {_wslConfig.Distribution}");

            try
            {
                // Check if WSL is available
                var result = await ExecuteWslCommandAsync("--version");
                if (string.IsNullOrEmpty(result))
                {
                    throw new InvalidOperationException("WSL2 is not available or not running");
                }
                
                Logger.LogSuccess($"WSL2 backend initialized for distribution: {_wslConfig.Distribution}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to initialize WSL2 backend: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Starts the Redis instance in WSL2.
        /// </summary>
        protected override async Task OnStartAsync()
        {
            Logger.LogInfo($"Starting Redis in WSL2 distribution: {_wslConfig.Distribution}");

            try
            {
                // Check if Redis is already running
                var isRunning = await IsRedisRunningAsync();
                if (isRunning)
                {
                    Logger.LogInfo($"Redis is already running in WSL2 distribution: {_wslConfig.Distribution}");
                    return;
                }

                // Start the Redis process
                await StartRedisProcessAsync();

                // Wait for Redis to be ready
                await WaitForRedisReadyAsync();

                Logger.LogSuccess($"Redis started successfully in WSL2 distribution: {_wslConfig.Distribution}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to start Redis in WSL2: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Stops the Redis instance in WSL2.
        /// </summary>
        protected override async Task OnStopAsync(TimeSpan? timeout = null)
        {
            Logger.LogInfo($"Stopping Redis in WSL2 distribution: {_wslConfig.Distribution}");

            try
            {
                var actualTimeout = timeout ?? TimeSpan.FromSeconds(30);
                await StopRedisProcessAsync(actualTimeout);
                Logger.LogSuccess($"Redis stopped in WSL2 distribution: {_wslConfig.Distribution}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to stop Redis in WSL2: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Checks if Redis is running in WSL2.
        /// </summary>
        protected override async Task<bool> OnIsRunningAsync()
        {
            try
            {
                return await IsRedisRunningAsync();
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to check if Redis is running: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the current status of the Redis instance in WSL2.
        /// </summary>
        protected override async Task<BackendStatusInfo> OnGetStatusAsync()
        {
            try
            {
                var isRunningResult = await IsRunningAsync();
                var isRunning = isRunningResult.Match(
                    success => success,
                    failure => false
                );
                var status = isRunning ? BackendStatus.Running : BackendStatus.Stopped;
                var message = isRunning ? "Redis is running in WSL2" : "Redis is not running in WSL2";

                return new BackendStatusInfo(status, message, DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to get WSL2 backend status: {ex.Message}", ex);
                return new BackendStatusInfo(BackendStatus.Error, $"Failed to get status: {ex.Message}", DateTime.UtcNow, Option<Exception>.Some(ex));
            }
        }

        /// <summary>
        /// Performs a health check on the Redis instance in WSL2.
        /// </summary>
        protected override async Task<BackendOperationResult> OnHealthCheckAsync()
        {
            try
            {
                var result = await ExecuteRedisCommandAsync("PING");
                if (result == "PONG")
                {
                    return BackendOperationResult.Success("Redis health check passed");
                }
                else
                {
                    return BackendOperationResult.Failure($"Redis health check failed: Expected PONG, got {result}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Redis health check failed: {ex.Message}", ex);
                return BackendOperationResult.Failure($"Redis health check failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets the Redis connection string.
        /// </summary>
        protected override async Task<string> OnGetConnectionStringAsync()
        {
            var host = Configuration.Redis.BindAddress ?? "127.0.0.1";
            var port = Configuration.Redis.Port;
            var password = Configuration.Redis.Password;

            if (!string.IsNullOrEmpty(password))
            {
                return $"redis://:{password}@{host}:{port}";
            }

            return $"redis://{host}:{port}";
        }

        /// <summary>
        /// Gets the Redis port.
        /// </summary>
        protected override async Task<int> OnGetPortAsync()
        {
            return Configuration.Redis.Port;
        }

        /// <summary>
        /// Gets the Redis host.
        /// </summary>
        protected override async Task<string> OnGetHostAsync()
        {
            return Configuration.Redis.BindAddress ?? "127.0.0.1";
        }

        /// <summary>
        /// Validates the WSL2 configuration.
        /// </summary>
        protected override async Task<BackendOperationResult> OnValidateConfigurationAsync()
        {
            try
            {
                // Check if WSL is available
                var wslVersion = await ExecuteWslCommandAsync("--version");
                if (string.IsNullOrEmpty(wslVersion))
                {
                    return BackendOperationResult.Failure("WSL2 is not available or not running");
                }

                // Check if the distribution exists
                var distExists = await CheckDistributionExistsAsync();
                if (!distExists)
                {
                    return BackendOperationResult.Failure($"WSL2 distribution '{_wslConfig.Distribution}' is not available");
                }

                return BackendOperationResult.Success("WSL2 configuration is valid");
            }
            catch (Exception ex)
            {
                return BackendOperationResult.Failure($"Configuration validation failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets Redis logs from WSL2.
        /// </summary>
        protected override async Task<string> OnGetLogsAsync(int? lines)
        {
            try
            {
                var logPath = _wslConfig.LogPath ?? "/var/log/redis/redis-server.log";
                var lineCount = lines ?? 100;
                
                var result = await ExecuteWslCommandAsync($"-d {_wslConfig.Distribution} --exec tail -n {lineCount} {logPath}");
                return result ?? "Failed to retrieve logs";
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to get Redis logs: {ex.Message}", ex);
                return $"Error retrieving logs: {ex.Message}";
            }
        }

        /// <summary>
        /// Clears Redis logs in WSL2.
        /// </summary>
        protected override async Task<BackendOperationResult> OnClearLogsAsync()
        {
            try
            {
                var logPath = _wslConfig.LogPath ?? "/var/log/redis/redis-server.log";
                var result = await ExecuteWslCommandAsync($"-d {_wslConfig.Distribution} --exec truncate -s 0 {logPath}");
                
                if (!string.IsNullOrEmpty(result))
                {
                    return BackendOperationResult.Success("Redis logs cleared successfully");
                }
                else
                {
                    return BackendOperationResult.Failure("Failed to clear logs");
                }
            }
            catch (Exception ex)
            {
                return BackendOperationResult.Failure($"Failed to clear logs: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets resource usage information for the Redis process in WSL2.
        /// </summary>
        protected override async Task<BackendResourceUsage> OnGetResourceUsageAsync()
        {
            try
            {
                // Get Redis process info
                var processInfo = await GetRedisProcessInfoAsync();
                
                return new BackendResourceUsage(
                    processInfo.MemoryUsageBytes,
                    processInfo.CpuUsagePercent,
                    Option<long>.None, // Disk usage
                    Option<int>.Some(1), // Process count
                    DateTime.UtcNow
                );
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to get WSL2 resource usage: {ex.Message}");
                return new BackendResourceUsage(
                    Option<long>.None,
                    Option<double>.None,
                    Option<long>.None,
                    Option<int>.None,
                    DateTime.UtcNow
                );
            }
        }

        /// <summary>
        /// Updates the WSL2 configuration.
        /// </summary>
        protected override async Task<BackendOperationResult> OnUpdateConfigurationAsync(ServiceConfiguration newConfiguration)
        {
            try
            {
                if (newConfiguration.Wsl == null)
                {
                    return BackendOperationResult.Failure("WSL configuration is required");
                }

                _wslConfig = newConfiguration.Wsl;
                return BackendOperationResult.Success("Configuration updated successfully");
            }
            catch (Exception ex)
            {
                return BackendOperationResult.Failure($"Failed to update configuration: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Disposes of the WSL2 backend resources.
        /// </summary>
        protected override void OnDispose()
        {
            try
            {
                lock (_processLock)
                {
                    _wslProcess?.Dispose();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error disposing WSL2 backend: {ex.Message}", ex);
            }
        }

        #region Private Helper Methods

        /// <summary>
        /// Checks if Redis is running in WSL2.
        /// </summary>
        private async Task<bool> IsRedisRunningAsync()
        {
            try
            {
                var result = await ExecuteRedisCommandAsync("PING");
                return result == "PONG";
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Starts the Redis process in WSL2.
        /// </summary>
        private async Task StartRedisProcessAsync()
        {
            var redisPath = _wslConfig.RedisPath ?? "/usr/bin/redis-server";
            var configPath = _wslConfig.ConfigPath ?? "/etc/redis/redis.conf";
            var dataDir = _wslConfig.DataPath ?? "/var/lib/redis";

            // Build Redis command arguments
            var args = $"-d {_wslConfig.Distribution} --exec {redisPath} {configPath} --dir {dataDir} --port {Configuration.Redis.Port} --bind {Configuration.Redis.BindAddress ?? "127.0.0.1"}";

            // Add authentication if configured
            if (Configuration.Redis.RequirePassword && !string.IsNullOrEmpty(Configuration.Redis.Password))
            {
                args += $" --requirepass {Configuration.Redis.Password}";
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "wsl",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            lock (_processLock)
            {
                _wslProcess = Process.Start(startInfo);
                if (_wslProcess == null)
                {
                    throw new InvalidOperationException("Failed to start Redis process in WSL2");
                }
            }

            Logger.LogInfo($"Started Redis process in WSL2 with PID: {_wslProcess.Id}");
        }

        /// <summary>
        /// Stops the Redis process gracefully.
        /// </summary>
        private async Task StopRedisProcessAsync(TimeSpan timeout)
        {
            try
            {
                // Try graceful shutdown using redis-cli
                var result = await ExecuteRedisCommandAsync("SHUTDOWN");
                if (result == "OK")
                {
                    Logger.LogInfo("Redis process stopped gracefully");
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Graceful shutdown failed: {ex.Message}");
            }

            // Force kill if graceful shutdown failed
            try
            {
                await ExecuteWslCommandAsync($"-d {_wslConfig.Distribution} --exec pkill -f redis-server");
                Logger.LogInfo("Redis process stopped with SIGTERM");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to stop Redis process: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Waits for Redis to become ready.
        /// </summary>
        private async Task WaitForRedisReadyAsync()
        {
            var maxAttempts = 30; // 30 seconds
            var delay = TimeSpan.FromSeconds(1);

            for (int i = 0; i < maxAttempts; i++)
            {
                var result = await ExecuteRedisCommandAsync("PING");
                if (result == "PONG")
                {
                    return;
                }

                await Task.Delay(delay);
            }

            throw new TimeoutException("Redis failed to become ready within timeout");
        }

        /// <summary>
        /// Executes a Redis command using redis-cli.
        /// </summary>
        private async Task<string> ExecuteRedisCommandAsync(string command)
        {
            try
            {
                var redisCliPath = _wslConfig.RedisCliPath ?? "/usr/bin/redis-cli";
                var args = $"-d {_wslConfig.Distribution} --exec {redisCliPath} -p {Configuration.Redis.Port} -h {Configuration.Redis.BindAddress ?? "127.0.0.1"}";

                // Add authentication if configured
                if (Configuration.Redis.RequirePassword && !string.IsNullOrEmpty(Configuration.Redis.Password))
                {
                    args += $" -a {Configuration.Redis.Password}";
                }

                args += $" {command}";

                return await ExecuteWslCommandAsync(args);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to execute Redis command '{command}': {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Executes a WSL command.
        /// </summary>
        private async Task<string> ExecuteWslCommandAsync(string arguments)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "wsl",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    throw new InvalidOperationException("Failed to start WSL process");
                }

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"WSL command failed with exit code {process.ExitCode}: {error}");
                }

                return output.Trim();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to execute WSL command '{arguments}': {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Checks if the WSL distribution exists.
        /// </summary>
        private async Task<bool> CheckDistributionExistsAsync()
        {
            try
            {
                var result = await ExecuteWslCommandAsync($"-d {_wslConfig.Distribution} --exec echo 'test'");
                return !string.IsNullOrEmpty(result);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets Redis process information.
        /// </summary>
        private async Task<(Option<long> MemoryUsageBytes, Option<double> CpuUsagePercent)> GetRedisProcessInfoAsync()
        {
            try
            {
                var result = await ExecuteWslCommandAsync($"-d {_wslConfig.Distribution} --exec ps aux | grep redis-server | grep -v grep");
                
                if (string.IsNullOrEmpty(result))
                {
                    return (Option<long>.None, Option<double>.None);
                }

                // Parse ps output (simplified)
                var parts = result.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4)
                {
                    var memoryStr = parts[3]; // RSS column
                    var cpuStr = parts[2];    // CPU column
                    
                    if (long.TryParse(memoryStr, out var memory))
                    {
                        var memoryBytes = memory * 1024; // Convert from KB to bytes
                        var cpu = double.TryParse(cpuStr, out var cpuPercent) ? cpuPercent : 0.0;
                        
                        return (Option<long>.Some(memoryBytes), Option<double>.Some(cpu));
                    }
                }

                return (Option<long>.None, Option<double>.None);
            }
            catch
            {
                return (Option<long>.None, Option<double>.None);
            }
        }

        #endregion
    }
}
