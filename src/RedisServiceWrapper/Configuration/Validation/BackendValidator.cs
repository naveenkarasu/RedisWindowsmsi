using LanguageExt;
using static LanguageExt.Prelude;
using RedisServiceWrapper.Configuration;
using RedisServiceWrapper.Configuration.Validation;
using System;
using System.Linq;

namespace RedisServiceWrapper.Configuration.Validation;

/// <summary>
/// Validates backend-specific configuration settings.
/// Handles validation for both WSL2 and Docker backends.
/// </summary>
public sealed class BackendValidator : IConfigurationValidator<ServiceConfiguration>
{
    public string ValidatorName => "BackendValidator";

    public ValidationResult Validate(ServiceConfiguration config)
    {
        var result = ValidationResult.Success();

        // Validate backend type
        result = result.Combine(ValidateBackendType(config.BackendType));

        // Validate backend-specific configuration
        if (config.BackendType == Constants.BackendTypeWSL2)
        {
            result = result.Combine(ValidateWSL2Configuration(config.Wsl));
        }
        else if (config.BackendType == Constants.BackendTypeDocker)
        {
            result = result.Combine(ValidateDockerConfiguration(config.Docker));
        }

        return result;
    }

    /// <summary>
    /// Validates the backend type.
    /// </summary>
    private ValidationResult ValidateBackendType(string backendType)
    {
        if (string.IsNullOrWhiteSpace(backendType))
        {
            return ValidationResult.Failure("BackendType", "Backend type cannot be null or empty.", ValidationSeverity.Critical);
        }

        var validBackendTypes = new[] { Constants.BackendTypeWSL2, Constants.BackendTypeDocker };
        if (!validBackendTypes.Contains(backendType))
        {
            return ValidationResult.Failure(
                "BackendType", 
                $"Invalid backend type '{backendType}'. Must be one of: {string.Join(", ", validBackendTypes)}", 
                ValidationSeverity.Critical);
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates WSL2-specific configuration.
    /// </summary>
    private ValidationResult ValidateWSL2Configuration(WslConfiguration wslConfig)
    {
        var result = ValidationResult.Success();

        // Validate distribution
        result = result.Combine(StringValidators.NotNullOrEmpty(
            wslConfig.Distribution, 
            "WSL.Distribution", 
            "WSL distribution cannot be null or empty."));

        // Validate Redis paths
        result = result.Combine(StringValidators.NotNullOrEmpty(
            wslConfig.RedisPath, 
            "WSL.RedisPath", 
            "WSL Redis server path cannot be null or empty."));

        result = result.Combine(StringValidators.NotNullOrEmpty(
            wslConfig.RedisCliPath, 
            "WSL.RedisCliPath", 
            "WSL Redis CLI path cannot be null or empty."));

        // Validate configuration paths
        result = result.Combine(PathValidators.ValidPath(
            wslConfig.ConfigPath, 
            "WSL.ConfigPath", 
            "WSL Redis configuration path is invalid."));

        result = result.Combine(PathValidators.ValidPath(
            wslConfig.DataPath, 
            "WSL.DataPath", 
            "WSL Redis data path is invalid."));

        result = result.Combine(PathValidators.ValidPath(
            wslConfig.LogPath, 
            "WSL.LogPath", 
            "WSL Redis log path is invalid."));

        result = result.Combine(PathValidators.ValidPath(
            wslConfig.PidFile, 
            "WSL.PidFile", 
            "WSL Redis PID file path is invalid."));

        // Validate Windows paths
        result = result.Combine(PathValidators.ValidPath(
            wslConfig.WindowsDataPath, 
            "WSL.WindowsDataPath", 
            "WSL Windows data path is invalid."));

        result = result.Combine(PathValidators.ValidPath(
            wslConfig.WindowsConfigPath, 
            "WSL.WindowsConfigPath", 
            "WSL Windows configuration path is invalid."));

        // Validate health check interval
        result = result.Combine(NumericValidators.Positive(
            wslConfig.HealthCheckInterval, 
            "WSL.HealthCheckInterval", 
            "WSL health check interval must be positive."));

        // Add warnings for common issues
        result = result.Combine(ValidateWSL2Warnings(wslConfig));

        return result;
    }

    /// <summary>
    /// Validates Docker-specific configuration.
    /// </summary>
    private ValidationResult ValidateDockerConfiguration(DockerConfiguration dockerConfig)
    {
        var result = ValidationResult.Success();

        // Validate image name
        result = result.Combine(StringValidators.NotNullOrEmpty(
            dockerConfig.ImageName, 
            "Docker.ImageName", 
            "Docker image name cannot be null or empty."));

        // Validate container name
        result = result.Combine(StringValidators.NotNullOrEmpty(
            dockerConfig.ContainerName, 
            "Docker.ContainerName", 
            "Docker container name cannot be null or empty."));

        // Validate port mapping
        result = result.Combine(ValidateDockerPortMapping(dockerConfig.PortMapping));

        // Validate volume mappings
        result = result.Combine(ValidateDockerVolumeMappings(dockerConfig.VolumeMappings));

        // Validate network mode
        result = result.Combine(StringValidators.NotNullOrEmpty(
            dockerConfig.NetworkMode, 
            "Docker.NetworkMode", 
            "Docker network mode cannot be null or empty."));

        // Validate restart policy
        result = result.Combine(ValidateDockerRestartPolicy(dockerConfig.RestartPolicy));

        // Validate health check interval
        result = result.Combine(NumericValidators.Positive(
            dockerConfig.HealthCheckInterval, 
            "Docker.HealthCheckInterval", 
            "Docker health check interval must be positive."));

        // Validate resource limits
        result = result.Combine(ValidateDockerResourceLimits(dockerConfig.ResourceLimits));

        // Add warnings for common issues
        result = result.Combine(ValidateDockerWarnings(dockerConfig));

        return result;
    }

    /// <summary>
    /// Validates Docker port mapping format.
    /// </summary>
    private ValidationResult ValidateDockerPortMapping(string portMapping)
    {
        if (string.IsNullOrWhiteSpace(portMapping))
        {
            return ValidationResult.Failure("Docker.PortMapping", "Docker port mapping cannot be null or empty.", ValidationSeverity.Error);
        }

        // Expected format: "hostPort:containerPort" or "hostPort:containerPort/protocol"
        var parts = portMapping.Split(':');
        if (parts.Length != 2)
        {
            return ValidationResult.Failure(
                "Docker.PortMapping", 
                $"Invalid port mapping format '{portMapping}'. Expected format: 'hostPort:containerPort'", 
                ValidationSeverity.Error);
        }

        // Validate host port
        if (!int.TryParse(parts[0], out var hostPort) || hostPort < 1 || hostPort > 65535)
        {
            return ValidationResult.Failure(
                "Docker.PortMapping", 
                $"Invalid host port '{parts[0]}' in port mapping. Must be a number between 1 and 65535.", 
                ValidationSeverity.Error);
        }

        // Validate container port
        var containerPortPart = parts[1].Split('/')[0]; // Remove protocol if present
        if (!int.TryParse(containerPortPart, out var containerPort) || containerPort < 1 || containerPort > 65535)
        {
            return ValidationResult.Failure(
                "Docker.PortMapping", 
                $"Invalid container port '{containerPortPart}' in port mapping. Must be a number between 1 and 65535.", 
                ValidationSeverity.Error);
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates Docker volume mappings.
    /// </summary>
    private ValidationResult ValidateDockerVolumeMappings(Seq<string> volumeMappings)
    {
        if (volumeMappings.IsEmpty)
        {
            return ValidationResult.SuccessWithWarnings(
                new ValidationWarning("Docker.VolumeMappings", "No volume mappings configured. Data may not persist.", ValidationSeverity.Warning));
        }

        var result = ValidationResult.Success();
        foreach (var mapping in volumeMappings)
        {
            result = result.Combine(ValidateDockerVolumeMapping(mapping));
        }

        return result;
    }

    /// <summary>
    /// Validates a single Docker volume mapping.
    /// </summary>
    private ValidationResult ValidateDockerVolumeMapping(string volumeMapping)
    {
        if (string.IsNullOrWhiteSpace(volumeMapping))
        {
            return ValidationResult.Failure("Docker.VolumeMapping", "Volume mapping cannot be null or empty.", ValidationSeverity.Error);
        }

        // Expected format: "hostPath:containerPath" or "hostPath:containerPath:mode"
        var parts = volumeMapping.Split(':');
        if (parts.Length < 2 || parts.Length > 3)
        {
            return ValidationResult.Failure(
                "Docker.VolumeMapping", 
                $"Invalid volume mapping format '{volumeMapping}'. Expected format: 'hostPath:containerPath' or 'hostPath:containerPath:mode'", 
                ValidationSeverity.Error);
        }

        // Validate host path
        var hostPath = parts[0];
        if (string.IsNullOrWhiteSpace(hostPath))
        {
            return ValidationResult.Failure("Docker.VolumeMapping", "Host path in volume mapping cannot be empty.", ValidationSeverity.Error);
        }

        // Validate container path
        var containerPath = parts[1];
        if (string.IsNullOrWhiteSpace(containerPath))
        {
            return ValidationResult.Failure("Docker.VolumeMapping", "Container path in volume mapping cannot be empty.", ValidationSeverity.Error);
        }

        // Validate mode if present
        if (parts.Length == 3)
        {
            var mode = parts[2];
            var validModes = new[] { "ro", "rw", "z", "Z" };
            if (!validModes.Contains(mode))
            {
                return ValidationResult.Failure(
                    "Docker.VolumeMapping", 
                    $"Invalid volume mapping mode '{mode}'. Valid modes: {string.Join(", ", validModes)}", 
                    ValidationSeverity.Error);
            }
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates Docker restart policy.
    /// </summary>
    private ValidationResult ValidateDockerRestartPolicy(string restartPolicy)
    {
        if (string.IsNullOrWhiteSpace(restartPolicy))
        {
            return ValidationResult.Failure("Docker.RestartPolicy", "Docker restart policy cannot be null or empty.", ValidationSeverity.Error);
        }

        var validPolicies = new[] { "no", "on-failure", "always", "unless-stopped" };
        if (!validPolicies.Contains(restartPolicy))
        {
            return ValidationResult.Failure(
                "Docker.RestartPolicy", 
                $"Invalid restart policy '{restartPolicy}'. Valid policies: {string.Join(", ", validPolicies)}", 
                ValidationSeverity.Error);
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates Docker resource limits.
    /// </summary>
    private ValidationResult ValidateDockerResourceLimits(ResourceLimits resourceLimits)
    {
        var result = ValidationResult.Success();

        // Validate memory limit
        if (!string.IsNullOrWhiteSpace(resourceLimits.Memory))
        {
            result = result.Combine(ValidateDockerMemoryLimit(resourceLimits.Memory));
        }

        // Validate CPU limit
        if (!string.IsNullOrWhiteSpace(resourceLimits.Cpus))
        {
            result = result.Combine(ValidateDockerCpuLimit(resourceLimits.Cpus));
        }

        return result;
    }

    /// <summary>
    /// Validates Docker memory limit format.
    /// </summary>
    private ValidationResult ValidateDockerMemoryLimit(string memoryLimit)
    {
        // Expected format: "512m", "1g", "1024", etc.
        if (string.IsNullOrWhiteSpace(memoryLimit))
        {
            return ValidationResult.Success(); // Optional field
        }

        // Basic validation - should contain numbers and optional unit
        if (!System.Text.RegularExpressions.Regex.IsMatch(memoryLimit, @"^\d+[kmg]?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            return ValidationResult.Failure(
                "Docker.ResourceLimits.Memory", 
                $"Invalid memory limit format '{memoryLimit}'. Expected format: '512m', '1g', '1024', etc.", 
                ValidationSeverity.Error);
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates Docker CPU limit format.
    /// </summary>
    private ValidationResult ValidateDockerCpuLimit(string cpuLimit)
    {
        // Expected format: "1.0", "0.5", "2", etc.
        if (string.IsNullOrWhiteSpace(cpuLimit))
        {
            return ValidationResult.Success(); // Optional field
        }

        if (!double.TryParse(cpuLimit, out var cpuValue) || cpuValue <= 0)
        {
            return ValidationResult.Failure(
                "Docker.ResourceLimits.Cpus", 
                $"Invalid CPU limit '{cpuLimit}'. Must be a positive number (e.g., '1.0', '0.5', '2').", 
                ValidationSeverity.Error);
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates WSL2 configuration and adds warnings for common issues.
    /// </summary>
    private ValidationResult ValidateWSL2Warnings(WslConfiguration wslConfig)
    {
        var warnings = new List<ValidationWarning>();

        // Check for default values that might need attention
        if (wslConfig.Distribution == Constants.DefaultWSLDistribution)
        {
            warnings.Add(new ValidationWarning(
                "WSL.Distribution", 
                "Using default WSL distribution. Ensure it's installed and configured.", 
                ValidationSeverity.Info));
        }

        // Check for non-standard paths
        if (!wslConfig.RedisPath.Contains("redis-server"))
        {
            warnings.Add(new ValidationWarning(
                "WSL.RedisPath", 
                "Redis path doesn't contain 'redis-server'. Verify this is the correct Redis executable.", 
                ValidationSeverity.Warning));
        }

        return warnings.Count > 0 
            ? ValidationResult.SuccessWithWarnings(warnings.ToArray())
            : ValidationResult.Success();
    }

    /// <summary>
    /// Validates Docker configuration and adds warnings for common issues.
    /// </summary>
    private ValidationResult ValidateDockerWarnings(DockerConfiguration dockerConfig)
    {
        var warnings = new List<ValidationWarning>();

        // Check for default image
        if (dockerConfig.ImageName == Constants.DefaultDockerImage)
        {
            warnings.Add(new ValidationWarning(
                "Docker.ImageName", 
                "Using default Docker image. Ensure it's available and up to date.", 
                ValidationSeverity.Info));
        }

        // Check for default container name
        if (dockerConfig.ContainerName == Constants.DefaultDockerContainerName)
        {
            warnings.Add(new ValidationWarning(
                "Docker.ContainerName", 
                "Using default container name. Consider using a more descriptive name.", 
                ValidationSeverity.Info));
        }

        // Check for standard Redis port
        if (dockerConfig.PortMapping == "6379:6379")
        {
            warnings.Add(new ValidationWarning(
                "Docker.PortMapping", 
                "Using standard Redis port 6379. Ensure no conflicts with other Redis instances.", 
                ValidationSeverity.Info));
        }

        // Check for volume mappings
        if (dockerConfig.VolumeMappings.IsEmpty)
        {
            warnings.Add(new ValidationWarning(
                "Docker.VolumeMappings", 
                "No volume mappings configured. Data will not persist between container restarts.", 
                ValidationSeverity.Warning));
        }

        return warnings.Count > 0 
            ? ValidationResult.SuccessWithWarnings(warnings.ToArray())
            : ValidationResult.Success();
    }
}
