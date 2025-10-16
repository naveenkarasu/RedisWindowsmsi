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
    /// Docker-based Redis backend implementation.
    /// Provides reliable Redis deployment using Docker containers.
    /// </summary>
    public sealed class DockerRedisBackend : BaseRedisBackend
    {
        private DockerConfiguration _dockerConfig;
        private Process? _dockerProcess;
        private readonly object _processLock = new object();

        /// <summary>
        /// Gets the backend type identifier.
        /// </summary>
        public override string BackendType => "Docker";

        /// <summary>
        /// Initializes the Docker backend.
        /// </summary>
        protected override async Task OnInitializeAsync()
        {
            _dockerConfig = Configuration.Docker ?? throw new InvalidOperationException("Docker configuration is required");
            
            Logger.LogInfo($"Initializing Docker backend for container: {_dockerConfig.ContainerName}");

            try
            {
                // Check if Docker is available
                var result = await ExecuteDockerCommandAsync("--version");
                if (string.IsNullOrEmpty(result))
                {
                    throw new InvalidOperationException("Docker is not available or not running");
                }
                
                Logger.LogSuccess($"Docker backend initialized for container: {_dockerConfig.ContainerName}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to initialize Docker backend: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Starts the Redis container.
        /// </summary>
        protected override async Task OnStartAsync()
        {
            Logger.LogInfo($"Starting Redis container: {_dockerConfig.ContainerName}");

            try
            {
                // Check if container is already running
                var isRunning = await IsContainerRunningAsync();
                if (isRunning)
                {
                    Logger.LogInfo($"Container {_dockerConfig.ContainerName} is already running");
                    return;
                }

                // Start the Redis container
                await StartRedisContainerAsync();

                // Wait for Redis to be ready
                await WaitForRedisReadyAsync();

                Logger.LogSuccess($"Redis container started successfully: {_dockerConfig.ContainerName}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to start Redis container: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Stops the Redis container.
        /// </summary>
        protected override async Task OnStopAsync(TimeSpan? timeout = null)
        {
            Logger.LogInfo($"Stopping Redis container: {_dockerConfig.ContainerName}");

            try
            {
                var actualTimeout = timeout ?? TimeSpan.FromSeconds(30);
                await StopRedisContainerAsync(actualTimeout);
                Logger.LogSuccess($"Redis container stopped: {_dockerConfig.ContainerName}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to stop Redis container: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Checks if Redis container is running.
        /// </summary>
        protected override async Task<bool> OnIsRunningAsync()
        {
            try
            {
                var isContainerRunning = await IsContainerRunningAsync();
                if (!isContainerRunning)
                {
                    return false;
                }

                // Additional check using redis-cli
                var result = await ExecuteRedisCommandAsync("PING");
                return result == "PONG";
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to check if Redis is running: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the current status of the Redis container.
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
                var message = isRunning ? "Redis container is running" : "Redis container is not running";

                return new BackendStatusInfo(status, message, DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to get Docker backend status: {ex.Message}", ex);
                return new BackendStatusInfo(BackendStatus.Error, $"Failed to get status: {ex.Message}", DateTime.UtcNow, Option<Exception>.Some(ex));
            }
        }

        /// <summary>
        /// Performs a health check on the Redis container.
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
        /// Validates the Docker configuration.
        /// </summary>
        protected override async Task<BackendOperationResult> OnValidateConfigurationAsync()
        {
            try
            {
                // Check if Docker is available
                var dockerVersion = await ExecuteDockerCommandAsync("--version");
                if (string.IsNullOrEmpty(dockerVersion))
                {
                    return BackendOperationResult.Failure("Docker is not available or not running");
                }

                // Check if Redis image is available
                var imageExists = await CheckRedisImageExistsAsync();
                if (!imageExists)
                {
                    return BackendOperationResult.Failure($"Redis image '{_dockerConfig.ImageName}' is not available");
                }

                return BackendOperationResult.Success("Docker configuration is valid");
            }
            catch (Exception ex)
            {
                return BackendOperationResult.Failure($"Configuration validation failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets Redis logs from the container.
        /// </summary>
        protected override async Task<string> OnGetLogsAsync(int? lines)
        {
            try
            {
                var lineCount = lines ?? 100;
                var result = await ExecuteDockerCommandAsync($"logs --tail {lineCount} {_dockerConfig.ContainerName}");
                return result ?? "Failed to retrieve logs";
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to get Redis logs: {ex.Message}", ex);
                return $"Error retrieving logs: {ex.Message}";
            }
        }

        /// <summary>
        /// Clears Redis logs in the container.
        /// </summary>
        protected override async Task<BackendOperationResult> OnClearLogsAsync()
        {
            try
            {
                // Docker doesn't have a direct way to clear logs, but we can restart the container
                await ExecuteDockerCommandAsync($"restart {_dockerConfig.ContainerName}");
                return BackendOperationResult.Success("Redis logs cleared successfully");
            }
            catch (Exception ex)
            {
                return BackendOperationResult.Failure($"Failed to clear logs: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets resource usage information for the Redis container.
        /// </summary>
        protected override async Task<BackendResourceUsage> OnGetResourceUsageAsync()
        {
            try
            {
                // Get container stats
                var stats = await ExecuteDockerCommandAsync($"stats --no-stream --format \"table {{.CPUPerc}}\\t{{.MemUsage}}\\t{{.MemPerc}}\" {_dockerConfig.ContainerName}");
                
                // Parse stats (simplified)
                var lines = stats?.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (lines?.Length > 1)
                {
                    var parts = lines[1].Split('\t', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        var cpuPercent = double.TryParse(parts[0].Replace("%", ""), out var cpu) ? cpu : 0.0;
                        var memUsage = parts[1]; // e.g., "1.2GiB / 2GiB"
                        var memPercent = double.TryParse(parts[2].Replace("%", ""), out var mem) ? mem : 0.0;

                        return new BackendResourceUsage(
                            Option<long>.None, // Memory usage in bytes (would need parsing)
                            Option<double>.Some(cpuPercent),
                            Option<long>.None, // Disk usage
                            Option<int>.Some(1), // Process count
                            DateTime.UtcNow
                        );
                    }
                }

                return new BackendResourceUsage(
                    Option<long>.None,
                    Option<double>.None,
                    Option<long>.None,
                    Option<int>.Some(1),
                    DateTime.UtcNow
                );
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to get Docker resource usage: {ex.Message}");
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
        /// Updates the Docker configuration.
        /// </summary>
        protected override async Task<BackendOperationResult> OnUpdateConfigurationAsync(ServiceConfiguration newConfiguration)
        {
            try
            {
                if (newConfiguration.Docker == null)
                {
                    return BackendOperationResult.Failure("Docker configuration is required");
                }

                _dockerConfig = newConfiguration.Docker;
                return BackendOperationResult.Success("Configuration updated successfully");
            }
            catch (Exception ex)
            {
                return BackendOperationResult.Failure($"Failed to update configuration: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Disposes of the Docker backend resources.
        /// </summary>
        protected override void OnDispose()
        {
            try
            {
                lock (_processLock)
                {
                    _dockerProcess?.Dispose();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error disposing Docker backend: {ex.Message}", ex);
            }
        }

        #region Private Helper Methods

        /// <summary>
        /// Starts the Redis container.
        /// </summary>
        private async Task StartRedisContainerAsync()
        {
            var imageName = _dockerConfig.ImageName ?? "redis:latest";
            var containerName = _dockerConfig.ContainerName ?? "redis-container";
            var port = Configuration.Redis.Port;

            // Build Docker run command
            var args = $"run -d --name {containerName} -p {port}:6379";

            // Add volume mappings if configured
            if (_dockerConfig.VolumeMappings != null && _dockerConfig.VolumeMappings.Count > 0)
            {
                foreach (var volume in _dockerConfig.VolumeMappings)
                {
                    args += $" -v {volume}";
                }
            }

            // Add environment variables if configured
            if (Configuration.Advanced?.EnvironmentVariables != null && Configuration.Advanced.EnvironmentVariables.Count > 0)
            {
                foreach (var envVar in Configuration.Advanced.EnvironmentVariables)
                {
                    args += $" -e {envVar.Key}={envVar.Value}";
                }
            }

            // Add Redis password if configured
            if (Configuration.Redis.RequirePassword && !string.IsNullOrEmpty(Configuration.Redis.Password))
            {
                args += $" -e REDIS_PASSWORD={Configuration.Redis.Password}";
            }

            args += $" {imageName}";

            // Start the container
            var result = await ExecuteDockerCommandAsync(args);
            if (string.IsNullOrEmpty(result))
            {
                throw new InvalidOperationException("Failed to start Redis container");
            }

            Logger.LogInfo($"Started Redis container: {containerName}");
        }

        /// <summary>
        /// Stops the Redis container.
        /// </summary>
        private async Task StopRedisContainerAsync(TimeSpan timeout)
        {
            try
            {
                // Try graceful stop first
                await ExecuteDockerCommandAsync($"stop {_dockerConfig.ContainerName}");
                
                // Wait for container to stop
                var startTime = DateTime.UtcNow;
                while (DateTime.UtcNow - startTime < timeout)
                {
                    var isRunning = await IsContainerRunningAsync();
                    if (!isRunning)
                    {
                        Logger.LogInfo("Redis container stopped gracefully");
                        return;
                    }
                    await Task.Delay(1000);
                }

                // Force kill if graceful stop failed
                await ExecuteDockerCommandAsync($"kill {_dockerConfig.ContainerName}");
                Logger.LogInfo("Redis container stopped forcefully");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to stop Redis container: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Checks if the container is running.
        /// </summary>
        private async Task<bool> IsContainerRunningAsync()
        {
            try
            {
                var result = await ExecuteDockerCommandAsync($"ps --filter name={_dockerConfig.ContainerName} --filter status=running --format \"{{.Names}}\"");
                return !string.IsNullOrEmpty(result) && result.Contains(_dockerConfig.ContainerName);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to check container status: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if the Redis image exists.
        /// </summary>
        private async Task<bool> CheckRedisImageExistsAsync()
        {
            try
            {
                var result = await ExecuteDockerCommandAsync($"images --filter reference={_dockerConfig.ImageName} --format \"{{.Repository}}\"");
                return !string.IsNullOrEmpty(result) && result.Contains(_dockerConfig.ImageName.Split(':')[0]);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to check Redis image: {ex.Message}");
                return false;
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
        /// Executes a Redis command using redis-cli in the container.
        /// </summary>
        private async Task<string> ExecuteRedisCommandAsync(string command)
        {
            try
            {
                var args = $"exec {_dockerConfig.ContainerName} redis-cli";

                // Add authentication if configured
                if (Configuration.Redis.RequirePassword && !string.IsNullOrEmpty(Configuration.Redis.Password))
                {
                    args += $" -a {Configuration.Redis.Password}";
                }

                args += $" {command}";

                return await ExecuteDockerCommandAsync(args);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to execute Redis command '{command}': {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Executes a Docker command.
        /// </summary>
        private async Task<string> ExecuteDockerCommandAsync(string arguments)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    throw new InvalidOperationException("Failed to start Docker process");
                }

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"Docker command failed with exit code {process.ExitCode}: {error}");
                }

                return output.Trim();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to execute Docker command '{arguments}': {ex.Message}", ex);
                throw;
            }
        }

        #endregion
    }
}
