using LanguageExt;
using static LanguageExt.Prelude;
using RedisServiceWrapper.Configuration;
using RedisServiceWrapper.Configuration.Validation;
using System;
using System.Linq;

namespace RedisServiceWrapper.Configuration.Validation;

/// <summary>
/// Main configuration validator that combines all individual validators.
/// Provides a comprehensive validation system for ServiceConfiguration.
/// </summary>
public sealed class ConfigurationValidator : IConfigurationValidator<ServiceConfiguration>
{
    private readonly CompositeValidator<ServiceConfiguration> _compositeValidator;

    public string ValidatorName => "ConfigurationValidator";

    /// <summary>
    /// Initializes a new instance of the ConfigurationValidator with default validators.
    /// </summary>
    public ConfigurationValidator()
    {
        _compositeValidator = new CompositeValidator<ServiceConfiguration>(
            new BackendValidator(),
            new RedisValidator(),
            new ServiceSettingsValidator(),
            new SystemValidator()
        );
    }

    /// <summary>
    /// Initializes a new instance of the ConfigurationValidator with custom validators.
    /// </summary>
    /// <param name="validators">Custom validators to use</param>
    public ConfigurationValidator(params IConfigurationValidator<ServiceConfiguration>[] validators)
    {
        _compositeValidator = new CompositeValidator<ServiceConfiguration>(validators);
    }

    /// <summary>
    /// Validates the entire ServiceConfiguration.
    /// </summary>
    /// <param name="config">The configuration to validate</param>
    /// <returns>Comprehensive validation result</returns>
    public ValidationResult Validate(ServiceConfiguration config)
    {
        if (config == null)
        {
            return ValidationResult.Failure("Configuration", "Configuration cannot be null.", ValidationSeverity.Critical);
        }

        // Run all validators
        var result = _compositeValidator.Validate(config);

        // Add cross-cutting validations
        result = result.Combine(ValidateCrossCuttingConcerns(config));

        // Add configuration summary
        result = result.Combine(ValidateConfigurationSummary(config));

        return result;
    }

    /// <summary>
    /// Validates cross-cutting concerns that span multiple configuration sections.
    /// </summary>
    private ValidationResult ValidateCrossCuttingConcerns(ServiceConfiguration config)
    {
        var result = ValidationResult.Success();

        // Validate backend-specific cross-cutting concerns
        if (config.BackendType == Constants.BackendTypeWSL2)
        {
            result = result.Combine(ValidateWSL2CrossCutting(config));
        }
        else if (config.BackendType == Constants.BackendTypeDocker)
        {
            result = result.Combine(ValidateDockerCrossCutting(config));
        }

        // Validate Redis port conflicts
        result = result.Combine(ValidateRedisPortConflicts(config));

        // Validate monitoring and performance alignment
        result = result.Combine(ValidateMonitoringPerformanceAlignment(config));

        // Validate security concerns
        result = result.Combine(ValidateSecurityConcerns(config));

        return result;
    }

    /// <summary>
    /// Validates WSL2-specific cross-cutting concerns.
    /// </summary>
    private ValidationResult ValidateWSL2CrossCutting(ServiceConfiguration config)
    {
        var result = ValidationResult.Success();

        // Check if WSL2 paths are accessible from Windows
        if (!string.IsNullOrWhiteSpace(config.Wsl.WindowsDataPath))
        {
            try
            {
                var fullPath = System.IO.Path.GetFullPath(config.Wsl.WindowsDataPath);
                // Additional validation could be added here
            }
            catch (Exception ex)
            {
                result = result.Combine(ValidationResult.Failure(
                    "WSL.WindowsDataPath", 
                    $"Windows data path is not accessible: {ex.Message}", 
                    ValidationSeverity.Error));
            }
        }

        // Check for WSL2 distribution availability (warning only)
        if (!string.IsNullOrWhiteSpace(config.Wsl.Distribution))
        {
            result = result.Combine(ValidationResult.SuccessWithWarnings(
                new ValidationWarning(
                    "WSL.Distribution", 
                    $"Ensure WSL2 distribution '{config.Wsl.Distribution}' is installed and available.", 
                    ValidationSeverity.Info)));
        }

        return result;
    }

    /// <summary>
    /// Validates Docker-specific cross-cutting concerns.
    /// </summary>
    private ValidationResult ValidateDockerCrossCutting(ServiceConfiguration config)
    {
        var result = ValidationResult.Success();

        // Check for Docker availability (warning only)
        result = result.Combine(ValidationResult.SuccessWithWarnings(
            new ValidationWarning(
                "Docker.Availability", 
                "Ensure Docker Desktop is installed and running.", 
                ValidationSeverity.Info)));

        // Check for port conflicts with host
        if (!string.IsNullOrWhiteSpace(config.Docker.PortMapping))
        {
            var portParts = config.Docker.PortMapping.Split(':');
            if (portParts.Length >= 2 && int.TryParse(portParts[0], out var hostPort))
            {
                if (hostPort == config.Redis.Port)
                {
                    result = result.Combine(ValidationResult.SuccessWithWarnings(
                        new ValidationWarning(
                            "Docker.PortMapping", 
                            $"Docker host port {hostPort} matches Redis port. Ensure no conflicts.", 
                            ValidationSeverity.Info)));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Validates Redis port conflicts.
    /// </summary>
    private ValidationResult ValidateRedisPortConflicts(ServiceConfiguration config)
    {
        var result = ValidationResult.Success();

        // Check for standard Redis port usage
        if (config.Redis.Port == Constants.DefaultRedisPort)
        {
            result = result.Combine(ValidationResult.SuccessWithWarnings(
                new ValidationWarning(
                    "Redis.Port", 
                    "Using standard Redis port 6379. Ensure no other Redis instances are running on this port.", 
                    ValidationSeverity.Info)));
        }

        // Check for privileged port usage
        if (config.Redis.Port < 1024)
        {
            result = result.Combine(ValidationResult.SuccessWithWarnings(
                new ValidationWarning(
                    "Redis.Port", 
                    "Using privileged port. Ensure the service has appropriate permissions.", 
                    ValidationSeverity.Warning)));
        }

        return result;
    }

    /// <summary>
    /// Validates alignment between monitoring and performance settings.
    /// </summary>
    private ValidationResult ValidateMonitoringPerformanceAlignment(ServiceConfiguration config)
    {
        var result = ValidationResult.Success();

        // Check if health check is enabled but auto restart is disabled
        if (config.Monitoring.EnableHealthCheck && !config.Performance.EnableAutoRestart)
        {
            result = result.Combine(ValidationResult.SuccessWithWarnings(
                new ValidationWarning(
                    "Monitoring.Performance.Alignment", 
                    "Health check is enabled but auto restart is disabled. Health issues will be detected but not automatically resolved.", 
                    ValidationSeverity.Info)));
        }

        // Check if health check interval is much longer than restart cooldown
        if (config.Monitoring.EnableHealthCheck && config.Performance.EnableAutoRestart)
        {
            var healthCheckInterval = config.Monitoring.HealthCheckInterval;
            var restartCooldown = config.Performance.RestartCooldown;

            if (healthCheckInterval > restartCooldown * 2)
            {
                result = result.Combine(ValidationResult.SuccessWithWarnings(
                    new ValidationWarning(
                        "Monitoring.Performance.Alignment", 
                        "Health check interval is much longer than restart cooldown. Consider adjusting for better responsiveness.", 
                        ValidationSeverity.Info)));
            }
        }

        return result;
    }

    /// <summary>
    /// Validates security concerns across the configuration.
    /// </summary>
    private ValidationResult ValidateSecurityConcerns(ServiceConfiguration config)
    {
        var result = ValidationResult.Success();

        // Check for authentication requirements
        if (!config.Redis.RequirePassword)
        {
            result = result.Combine(ValidationResult.SuccessWithWarnings(
                new ValidationWarning(
                    "Security.Authentication", 
                    "Redis authentication is disabled. Consider enabling authentication for production use.", 
                    ValidationSeverity.Warning)));
        }

        // Check for localhost binding
        if (config.Redis.BindAddress == "127.0.0.1" || config.Redis.BindAddress == "localhost")
        {
            result = result.Combine(ValidationResult.SuccessWithWarnings(
                new ValidationWarning(
                    "Security.Network", 
                    "Redis is bound to localhost only. Remote access is not possible.", 
                    ValidationSeverity.Info)));
        }

        // Check for debug logging in production
        if (config.Monitoring.LogLevel == "Debug" || config.Redis.LogLevel == "debug")
        {
            result = result.Combine(ValidationResult.SuccessWithWarnings(
                new ValidationWarning(
                    "Security.Logging", 
                    "Debug logging is enabled. This may expose sensitive information in logs.", 
                    ValidationSeverity.Warning)));
        }

        return result;
    }

    /// <summary>
    /// Validates configuration summary and provides overall assessment.
    /// </summary>
    private ValidationResult ValidateConfigurationSummary(ServiceConfiguration config)
    {
        var result = ValidationResult.Success();

        // Check for default configuration usage
        var defaultUsageWarnings = new List<ValidationWarning>();

        if (config.BackendType == Constants.BackendTypeWSL2)
        {
            if (config.Wsl.Distribution == Constants.DefaultWSLDistribution)
                defaultUsageWarnings.Add(new ValidationWarning("Configuration.Defaults", "Using default WSL distribution.", ValidationSeverity.Info));
        }
        else if (config.BackendType == Constants.BackendTypeDocker)
        {
            if (config.Docker.ImageName == Constants.DefaultDockerImage)
                defaultUsageWarnings.Add(new ValidationWarning("Configuration.Defaults", "Using default Docker image.", ValidationSeverity.Info));
        }

        if (config.Redis.Port == Constants.DefaultRedisPort)
            defaultUsageWarnings.Add(new ValidationWarning("Configuration.Defaults", "Using default Redis port.", ValidationSeverity.Info));

        if (defaultUsageWarnings.Count > 0)
        {
            result = result.Combine(ValidationResult.SuccessWithWarnings(defaultUsageWarnings.ToArray()));
        }

        // Check for production readiness
        var productionReadinessWarnings = new List<ValidationWarning>();

        if (!config.Redis.RequirePassword)
            productionReadinessWarnings.Add(new ValidationWarning("Production.Readiness", "Authentication disabled - not recommended for production.", ValidationSeverity.Warning));

        if (!config.Redis.EnablePersistence)
            productionReadinessWarnings.Add(new ValidationWarning("Production.Readiness", "Persistence disabled - data will be lost on restart.", ValidationSeverity.Warning));

        if (config.Monitoring.LogLevel == "Debug")
            productionReadinessWarnings.Add(new ValidationWarning("Production.Readiness", "Debug logging enabled - may impact performance.", ValidationSeverity.Warning));

        if (productionReadinessWarnings.Count > 0)
        {
            result = result.Combine(ValidationResult.SuccessWithWarnings(productionReadinessWarnings.ToArray()));
        }

        return result;
    }

    /// <summary>
    /// Validates configuration and returns a detailed report.
    /// </summary>
    /// <param name="config">The configuration to validate</param>
    /// <returns>Detailed validation report</returns>
    public ValidationReport ValidateWithReport(ServiceConfiguration config)
    {
        var validationResult = Validate(config);
        
        return new ValidationReport(
            validationResult,
            config,
            DateTime.UtcNow,
            GetValidationSummary(validationResult)
        );
    }

    /// <summary>
    /// Gets a summary of the validation result.
    /// </summary>
    private string GetValidationSummary(ValidationResult result)
    {
        var summary = new List<string>();
        
        if (result.IsSuccess)
        {
            summary.Add("✅ Configuration validation passed successfully.");
        }
        else
        {
            summary.Add($"❌ Configuration validation failed with {result.Errors.Count} error(s).");
        }

        if (!result.Warnings.IsEmpty)
        {
            summary.Add($"⚠️ {result.Warnings.Count} warning(s) found.");
        }

        if (result.Errors.Count > 0)
        {
            summary.Add("\nErrors:");
            foreach (var error in result.Errors)
            {
                summary.Add($"  • {error.GetFormattedMessage()}");
            }
        }

        if (result.Warnings.Count > 0)
        {
            summary.Add("\nWarnings:");
            foreach (var warning in result.Warnings)
            {
                summary.Add($"  • {warning.GetFormattedMessage()}");
            }
        }

        return string.Join("\n", summary);
    }
}

/// <summary>
/// Represents a detailed validation report.
/// </summary>
public sealed record ValidationReport(
    ValidationResult Result,
    ServiceConfiguration Configuration,
    DateTime ValidatedAt,
    string Summary
)
{
    /// <summary>
    /// Indicates if the configuration is valid for production use.
    /// </summary>
    public bool IsProductionReady => Result.IsSuccess && !Result.Warnings.Any(w => w.Severity == ValidationSeverity.Warning || w.Severity == ValidationSeverity.Critical);

    /// <summary>
    /// Gets the number of critical issues.
    /// </summary>
    public int CriticalIssues => Result.Errors.Count(e => e.Severity == ValidationSeverity.Critical);

    /// <summary>
    /// Gets the number of errors.
    /// </summary>
    public int ErrorCount => Result.Errors.Count;

    /// <summary>
    /// Gets the number of warnings.
    /// </summary>
    public int WarningCount => Result.Warnings.Count;

    /// <summary>
    /// Gets the number of informational messages.
    /// </summary>
    public int InfoCount => Result.Warnings.Count(w => w.Severity == ValidationSeverity.Info);
}
