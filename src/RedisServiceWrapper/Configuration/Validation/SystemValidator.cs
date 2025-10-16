using LanguageExt;
using static LanguageExt.Prelude;
using RedisServiceWrapper.Configuration;
using RedisServiceWrapper.Configuration.Validation;
using System;
using System.Linq;

namespace RedisServiceWrapper.Configuration.Validation;

/// <summary>
/// Validates system-level configuration settings.
/// Handles validation for monitoring, performance, and advanced configuration.
/// </summary>
public sealed class SystemValidator : IConfigurationValidator<ServiceConfiguration>
{
    public string ValidatorName => "SystemValidator";

    public ValidationResult Validate(ServiceConfiguration config)
    {
        var result = ValidationResult.Success();

        // Validate monitoring configuration
        result = result.Combine(ValidateMonitoringConfiguration(config.Monitoring));

        // Validate performance configuration
        result = result.Combine(ValidatePerformanceConfiguration(config.Performance));

        // Validate advanced configuration
        result = result.Combine(ValidateAdvancedConfiguration(config.Advanced));

        // Validate metadata configuration
        result = result.Combine(ValidateMetadataConfiguration(config.Metadata));

        return result;
    }

    /// <summary>
    /// Validates monitoring configuration.
    /// </summary>
    private ValidationResult ValidateMonitoringConfiguration(MonitoringConfiguration monitoringConfig)
    {
        var result = ValidationResult.Success();

        // Validate health check settings
        if (monitoringConfig.EnableHealthCheck)
        {
            result = result.Combine(NumericValidators.Positive(
                monitoringConfig.HealthCheckInterval, 
                "Monitoring.HealthCheckInterval", 
                "Health check interval must be positive when health check is enabled."));

            result = result.Combine(NumericValidators.Positive(
                monitoringConfig.HealthCheckTimeout, 
                "Monitoring.HealthCheckTimeout", 
                "Health check timeout must be positive when health check is enabled."));
        }

        // Validate log settings
        if (monitoringConfig.EnableFileLogging)
        {
            result = result.Combine(PathValidators.ValidPath(
                monitoringConfig.LogFilePath, 
                "Monitoring.LogFilePath", 
                "Log file path is invalid."));
        }

        // Validate log level
        result = result.Combine(ValidateLogLevel(monitoringConfig.LogLevel, "Monitoring.LogLevel"));

        // Validate log size and file limits
        result = result.Combine(NumericValidators.Positive(
            monitoringConfig.MaxLogSizeMB, 
            "Monitoring.MaxLogSizeMB", 
            "Maximum log size must be positive."));

        result = result.Combine(NumericValidators.Positive(
            monitoringConfig.MaxLogFiles, 
            "Monitoring.MaxLogFiles", 
            "Maximum log files must be positive."));

        // Add warnings for monitoring configuration
        result = result.Combine(ValidateMonitoringWarnings(monitoringConfig));

        return result;
    }

    /// <summary>
    /// Validates performance configuration.
    /// </summary>
    private ValidationResult ValidatePerformanceConfiguration(PerformanceConfiguration performanceConfig)
    {
        var result = ValidationResult.Success();

        // Validate restart settings
        if (performanceConfig.EnableAutoRestart)
        {
            result = result.Combine(NumericValidators.Positive(
                performanceConfig.MaxRestartAttempts, 
                "Performance.MaxRestartAttempts", 
                "Maximum restart attempts must be positive when auto restart is enabled."));

            result = result.Combine(NumericValidators.NonNegative(
                performanceConfig.RestartCooldown, 
                "Performance.RestartCooldown", 
                "Restart cooldown must be non-negative."));
        }

        // Validate memory thresholds
        result = result.Combine(NumericValidators.InRange(
            performanceConfig.MemoryWarningThreshold, 
            0, 100, 
            "Performance.MemoryWarningThreshold", 
            "Memory warning threshold must be between 0 and 100."));

        result = result.Combine(NumericValidators.InRange(
            performanceConfig.MemoryErrorThreshold, 
            0, 100, 
            "Performance.MemoryErrorThreshold", 
            "Memory error threshold must be between 0 and 100."));

        // Validate threshold relationship
        if (performanceConfig.MemoryWarningThreshold >= performanceConfig.MemoryErrorThreshold)
        {
            result = result.Combine(ValidationResult.Failure(
                "Performance.MemoryThresholds", 
                "Memory warning threshold must be less than memory error threshold.", 
                ValidationSeverity.Error));
        }

        // Validate slow log settings
        if (performanceConfig.EnableSlowLogMonitoring)
        {
            result = result.Combine(NumericValidators.Positive(
                performanceConfig.SlowLogThreshold, 
                "Performance.SlowLogThreshold", 
                "Slow log threshold must be positive when slow log monitoring is enabled."));
        }

        // Add warnings for performance configuration
        result = result.Combine(ValidatePerformanceWarnings(performanceConfig));

        return result;
    }

    /// <summary>
    /// Validates advanced configuration.
    /// </summary>
    private ValidationResult ValidateAdvancedConfiguration(AdvancedConfiguration advancedConfig)
    {
        var result = ValidationResult.Success();

        // Validate custom startup arguments
        result = result.Combine(ValidateCustomStartupArgs(advancedConfig.CustomStartupArgs));

        // Validate environment variables
        result = result.Combine(ValidateEnvironmentVariables(advancedConfig.EnvironmentVariables));

        // Validate scripts
        result = result.Combine(ValidateScripts(advancedConfig));

        // Add warnings for advanced configuration
        result = result.Combine(ValidateAdvancedWarnings(advancedConfig));

        return result;
    }

    /// <summary>
    /// Validates metadata configuration.
    /// </summary>
    private ValidationResult ValidateMetadataConfiguration(MetadataConfiguration metadataConfig)
    {
        var result = ValidationResult.Success();

        // Validate config version
        result = result.Combine(StringValidators.NotNullOrEmpty(
            metadataConfig.ConfigVersion, 
            "Metadata.ConfigVersion", 
            "Configuration version cannot be null or empty."));

        // Validate created by
        result = result.Combine(StringValidators.NotNullOrEmpty(
            metadataConfig.CreatedBy, 
            "Metadata.CreatedBy", 
            "Created by field cannot be null or empty."));

        // Validate dates
        if (metadataConfig.CreatedDate.HasValue && metadataConfig.CreatedDate.Value > DateTime.UtcNow)
        {
            result = result.Combine(ValidationResult.Failure(
                "Metadata.CreatedDate", 
                "Created date cannot be in the future.", 
                ValidationSeverity.Error));
        }

        if (metadataConfig.LastModifiedDate.HasValue && metadataConfig.LastModifiedDate.Value > DateTime.UtcNow)
        {
            result = result.Combine(ValidationResult.Failure(
                "Metadata.LastModifiedDate", 
                "Last modified date cannot be in the future.", 
                ValidationSeverity.Error));
        }

        if (metadataConfig.CreatedDate.HasValue && metadataConfig.LastModifiedDate.HasValue && 
            metadataConfig.LastModifiedDate.Value < metadataConfig.CreatedDate.Value)
        {
            result = result.Combine(ValidationResult.Failure(
                "Metadata.Dates", 
                "Last modified date cannot be earlier than created date.", 
                ValidationSeverity.Error));
        }

        return result;
    }

    /// <summary>
    /// Validates log level.
    /// </summary>
    private ValidationResult ValidateLogLevel(string logLevel, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(logLevel))
        {
            return ValidationResult.Failure(propertyName, "Log level cannot be null or empty.", ValidationSeverity.Error);
        }

        var validLogLevels = new[] { "Debug", "Info", "Warning", "Error" };
        if (!validLogLevels.Contains(logLevel))
        {
            return ValidationResult.Failure(
                propertyName, 
                $"Invalid log level '{logLevel}'. Valid levels: {string.Join(", ", validLogLevels)}", 
                ValidationSeverity.Error);
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates custom startup arguments.
    /// </summary>
    private ValidationResult ValidateCustomStartupArgs(Seq<string> customStartupArgs)
    {
        if (customStartupArgs.IsEmpty)
        {
            return ValidationResult.Success();
        }

        var result = ValidationResult.Success();
        var argIndex = 0;

        foreach (var arg in customStartupArgs)
        {
            if (string.IsNullOrWhiteSpace(arg))
            {
                result = result.Combine(ValidationResult.Failure(
                    $"Advanced.CustomStartupArgs[{argIndex}]", 
                    "Custom startup argument cannot be null or empty.", 
                    ValidationSeverity.Error));
            }
            argIndex++;
        }

        // Check for reasonable number of arguments
        if (customStartupArgs.Count > 50)
        {
            result = result.Combine(ValidationResult.SuccessWithWarnings(
                new ValidationWarning(
                    "Advanced.CustomStartupArgs", 
                    $"Large number of custom startup arguments ({customStartupArgs.Count}). Consider if all are necessary.", 
                    ValidationSeverity.Info)));
        }

        return result;
    }

    /// <summary>
    /// Validates environment variables.
    /// </summary>
    private ValidationResult ValidateEnvironmentVariables(Map<string, string> environmentVariables)
    {
        if (environmentVariables.IsEmpty)
        {
            return ValidationResult.Success();
        }

        var result = ValidationResult.Success();

        foreach (var (key, value) in environmentVariables)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                result = result.Combine(ValidationResult.Failure(
                    "Advanced.EnvironmentVariables", 
                    "Environment variable key cannot be null or empty.", 
                    ValidationSeverity.Error));
            }

            // Check for reserved environment variable names
            var reservedVars = new[] { "PATH", "TEMP", "TMP", "WINDIR", "SYSTEMROOT", "PROGRAMFILES", "PROGRAMFILES(X86)" };
            if (reservedVars.Contains(key.ToUpperInvariant()))
            {
                result = result.Combine(ValidationResult.SuccessWithWarnings(
                    new ValidationWarning(
                        $"Advanced.EnvironmentVariables.{key}", 
                        $"Environment variable '{key}' is a reserved Windows environment variable.", 
                        ValidationSeverity.Warning)));
            }
        }

        // Check for reasonable number of environment variables
        if (environmentVariables.Count > 100)
        {
            result = result.Combine(ValidationResult.SuccessWithWarnings(
                new ValidationWarning(
                    "Advanced.EnvironmentVariables", 
                    $"Large number of environment variables ({environmentVariables.Count}). Consider if all are necessary.", 
                    ValidationSeverity.Info)));
        }

        return result;
    }

    /// <summary>
    /// Validates script configurations.
    /// </summary>
    private ValidationResult ValidateScripts(AdvancedConfiguration advancedConfig)
    {
        var result = ValidationResult.Success();

        // Validate pre-start script
        result = result.Combine(ValidateScript(advancedConfig.PreStartScript, "Advanced.PreStartScript"));

        // Validate post-start script
        result = result.Combine(ValidateScript(advancedConfig.PostStartScript, "Advanced.PostStartScript"));

        // Validate pre-stop script
        result = result.Combine(ValidateScript(advancedConfig.PreStopScript, "Advanced.PreStopScript"));

        // Validate post-stop script
        result = result.Combine(ValidateScript(advancedConfig.PostStopScript, "Advanced.PostStopScript"));

        return result;
    }

    /// <summary>
    /// Validates a single script configuration.
    /// </summary>
    private ValidationResult ValidateScript(string script, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            return ValidationResult.Success(); // Scripts are optional
        }

        // Check if script path is valid
        var pathResult = PathValidators.ValidPath(script, propertyName, "Script path is invalid.");
        if (!pathResult.IsSuccess)
        {
            return pathResult;
        }

        // Check for common script extensions
        var validExtensions = new[] { ".bat", ".cmd", ".ps1", ".exe" };
        var extension = System.IO.Path.GetExtension(script).ToLowerInvariant();
        if (!string.IsNullOrEmpty(extension) && !validExtensions.Contains(extension))
        {
            return ValidationResult.SuccessWithWarnings(
                new ValidationWarning(
                    propertyName, 
                    $"Script has unusual extension '{extension}'. Common extensions: {string.Join(", ", validExtensions)}", 
                    ValidationSeverity.Info));
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates monitoring configuration and adds warnings.
    /// </summary>
    private ValidationResult ValidateMonitoringWarnings(MonitoringConfiguration monitoringConfig)
    {
        var warnings = new List<ValidationWarning>();

        // Check for disabled health check
        if (!monitoringConfig.EnableHealthCheck)
        {
            warnings.Add(new ValidationWarning(
                "Monitoring.EnableHealthCheck", 
                "Health check monitoring is disabled. Service health will not be monitored.", 
                ValidationSeverity.Warning));
        }

        // Check for very frequent health checks
        if (monitoringConfig.EnableHealthCheck && monitoringConfig.HealthCheckInterval < 10)
        {
            warnings.Add(new ValidationWarning(
                "Monitoring.HealthCheckInterval", 
                "Very frequent health checks may impact performance.", 
                ValidationSeverity.Info));
        }

        // Check for very infrequent health checks
        if (monitoringConfig.EnableHealthCheck && monitoringConfig.HealthCheckInterval > 300)
        {
            warnings.Add(new ValidationWarning(
                "Monitoring.HealthCheckInterval", 
                "Infrequent health checks may delay failure detection.", 
                ValidationSeverity.Info));
        }

        // Check for disabled logging
        if (!monitoringConfig.EnableFileLogging && !monitoringConfig.EnableWindowsEventLog)
        {
            warnings.Add(new ValidationWarning(
                "Monitoring.Logging", 
                "Both file logging and Windows Event Log are disabled. No logs will be generated.", 
                ValidationSeverity.Warning));
        }

        // Check for debug log level
        if (monitoringConfig.LogLevel == "Debug")
        {
            warnings.Add(new ValidationWarning(
                "Monitoring.LogLevel", 
                "Debug log level may impact performance and generate large log files.", 
                ValidationSeverity.Warning));
        }

        // Check for very large log files
        if (monitoringConfig.MaxLogSizeMB > 100)
        {
            warnings.Add(new ValidationWarning(
                "Monitoring.MaxLogSizeMB", 
                "Large log file size may impact disk space and performance.", 
                ValidationSeverity.Info));
        }

        return warnings.Count > 0 
            ? ValidationResult.SuccessWithWarnings(warnings.ToArray())
            : ValidationResult.Success();
    }

    /// <summary>
    /// Validates performance configuration and adds warnings.
    /// </summary>
    private ValidationResult ValidatePerformanceWarnings(PerformanceConfiguration performanceConfig)
    {
        var warnings = new List<ValidationWarning>();

        // Check for disabled auto restart
        if (!performanceConfig.EnableAutoRestart)
        {
            warnings.Add(new ValidationWarning(
                "Performance.EnableAutoRestart", 
                "Auto restart is disabled. Service will not automatically recover from failures.", 
                ValidationSeverity.Warning));
        }

        // Check for very high restart attempts
        if (performanceConfig.EnableAutoRestart && performanceConfig.MaxRestartAttempts > 10)
        {
            warnings.Add(new ValidationWarning(
                "Performance.MaxRestartAttempts", 
                "High number of restart attempts may indicate a persistent issue.", 
                ValidationSeverity.Info));
        }

        // Check for very long cooldown
        if (performanceConfig.RestartCooldown > 300) // 5 minutes
        {
            warnings.Add(new ValidationWarning(
                "Performance.RestartCooldown", 
                "Long restart cooldown may delay service recovery.", 
                ValidationSeverity.Info));
        }

        // Check for high memory thresholds
        if (performanceConfig.MemoryWarningThreshold > 90)
        {
            warnings.Add(new ValidationWarning(
                "Performance.MemoryWarningThreshold", 
                "High memory warning threshold may not provide sufficient warning time.", 
                ValidationSeverity.Info));
        }

        // Check for very low slow log threshold
        if (performanceConfig.EnableSlowLogMonitoring && performanceConfig.SlowLogThreshold < 1000)
        {
            warnings.Add(new ValidationWarning(
                "Performance.SlowLogThreshold", 
                "Very low slow log threshold may generate excessive log entries.", 
                ValidationSeverity.Info));
        }

        return warnings.Count > 0 
            ? ValidationResult.SuccessWithWarnings(warnings.ToArray())
            : ValidationResult.Success();
    }

    /// <summary>
    /// Validates advanced configuration and adds warnings.
    /// </summary>
    private ValidationResult ValidateAdvancedWarnings(AdvancedConfiguration advancedConfig)
    {
        var warnings = new List<ValidationWarning>();

        // Check for custom startup arguments
        if (!advancedConfig.CustomStartupArgs.IsEmpty)
        {
            warnings.Add(new ValidationWarning(
                "Advanced.CustomStartupArgs", 
                "Custom startup arguments are configured. Ensure they are compatible with Redis.", 
                ValidationSeverity.Info));
        }

        // Check for environment variables
        if (!advancedConfig.EnvironmentVariables.IsEmpty)
        {
            warnings.Add(new ValidationWarning(
                "Advanced.EnvironmentVariables", 
                "Custom environment variables are configured. Ensure they don't conflict with system variables.", 
                ValidationSeverity.Info));
        }

        // Check for scripts
        if (advancedConfig.HasScripts())
        {
            warnings.Add(new ValidationWarning(
                "Advanced.Scripts", 
                "Custom scripts are configured. Ensure they are tested and secure.", 
                ValidationSeverity.Info));
        }

        return warnings.Count > 0 
            ? ValidationResult.SuccessWithWarnings(warnings.ToArray())
            : ValidationResult.Success();
    }
}
