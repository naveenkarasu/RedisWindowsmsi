using LanguageExt;
using static LanguageExt.Prelude;
using RedisServiceWrapper.Configuration;
using RedisServiceWrapper.Configuration.Validation;
using System;
using System.Linq;

namespace RedisServiceWrapper.Configuration.Validation;

/// <summary>
/// Validates Redis-specific configuration settings.
/// Handles validation for Redis server configuration, authentication, persistence, etc.
/// </summary>
public sealed class RedisValidator : IConfigurationValidator<ServiceConfiguration>
{
    public string ValidatorName => "RedisValidator";

    public ValidationResult Validate(ServiceConfiguration config)
    {
        var result = ValidationResult.Success();

        // Validate Redis configuration
        result = result.Combine(ValidateRedisConfiguration(config.Redis));

        return result;
    }

    /// <summary>
    /// Validates Redis configuration settings.
    /// </summary>
    private ValidationResult ValidateRedisConfiguration(RedisConfiguration redisConfig)
    {
        var result = ValidationResult.Success();

        // Validate port
        result = result.Combine(NetworkValidators.ValidPort(
            redisConfig.Port, 
            "Redis.Port", 
            "Redis port must be between 1 and 65535."));

        // Validate bind address
        result = result.Combine(ValidateRedisBindAddress(redisConfig.BindAddress));

        // Validate max memory
        result = result.Combine(ValidateRedisMaxMemory(redisConfig.MaxMemory));

        // Validate max memory policy
        result = result.Combine(ValidateRedisMaxMemoryPolicy(redisConfig.MaxMemoryPolicy));

        // Validate persistence settings
        result = result.Combine(ValidateRedisPersistence(redisConfig));

        // Validate authentication settings
        result = result.Combine(ValidateRedisAuthentication(redisConfig));

        // Validate log level
        result = result.Combine(ValidateRedisLogLevel(redisConfig.LogLevel));

        // Add warnings for common issues
        result = result.Combine(ValidateRedisWarnings(redisConfig));

        return result;
    }

    /// <summary>
    /// Validates Redis bind address.
    /// </summary>
    private ValidationResult ValidateRedisBindAddress(string bindAddress)
    {
        if (string.IsNullOrWhiteSpace(bindAddress))
        {
            return ValidationResult.Failure("Redis.BindAddress", "Redis bind address cannot be null or empty.", ValidationSeverity.Error);
        }

        // Check for special values
        var validBindAddresses = new[] { "127.0.0.1", "0.0.0.0", "localhost" };
        if (validBindAddresses.Contains(bindAddress))
        {
            return ValidationResult.Success();
        }

        // Validate as IP address
        var ipResult = NetworkValidators.ValidIpAddress(bindAddress, "Redis.BindAddress");
        if (!ipResult.IsSuccess)
        {
            return ValidationResult.Failure(
                "Redis.BindAddress", 
                $"Invalid bind address '{bindAddress}'. Must be a valid IP address or one of: {string.Join(", ", validBindAddresses)}", 
                ValidationSeverity.Error);
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates Redis max memory setting.
    /// </summary>
    private ValidationResult ValidateRedisMaxMemory(string maxMemory)
    {
        if (string.IsNullOrWhiteSpace(maxMemory))
        {
            return ValidationResult.Failure("Redis.MaxMemory", "Redis max memory cannot be null or empty.", ValidationSeverity.Error);
        }

        // Expected format: "512mb", "1gb", "1024", "1024k", etc.
        if (!System.Text.RegularExpressions.Regex.IsMatch(maxMemory, @"^\d+[kmg]?b?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            return ValidationResult.Failure(
                "Redis.MaxMemory", 
                $"Invalid max memory format '{maxMemory}'. Expected format: '512mb', '1gb', '1024', '1024k', etc.", 
                ValidationSeverity.Error);
        }

        // Parse and validate the numeric value
        var numericPart = System.Text.RegularExpressions.Regex.Replace(maxMemory, @"[kmg]?b?$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!int.TryParse(numericPart, out var value) || value <= 0)
        {
            return ValidationResult.Failure(
                "Redis.MaxMemory", 
                $"Invalid max memory value '{numericPart}'. Must be a positive number.", 
                ValidationSeverity.Error);
        }

        // Check for reasonable limits (warnings for very small or very large values)
        var unit = maxMemory.Substring(numericPart.Length).ToLowerInvariant();
        var bytesValue = ConvertToBytes(value, unit);

        if (bytesValue < 1024 * 1024) // Less than 1MB
        {
            return ValidationResult.SuccessWithWarnings(
                new ValidationWarning(
                    "Redis.MaxMemory", 
                    $"Max memory is very small ({maxMemory}). Redis may not function properly with such low memory.", 
                    ValidationSeverity.Warning));
        }

        if (bytesValue > 1024L * 1024 * 1024 * 1024) // More than 1TB
        {
            return ValidationResult.SuccessWithWarnings(
                new ValidationWarning(
                    "Redis.MaxMemory", 
                    $"Max memory is very large ({maxMemory}). Ensure your system has sufficient memory.", 
                    ValidationSeverity.Warning));
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Converts memory value to bytes for validation.
    /// </summary>
    private long ConvertToBytes(int value, string unit)
    {
        return unit.ToLowerInvariant() switch
        {
            "k" or "kb" => value * 1024L,
            "m" or "mb" => value * 1024L * 1024,
            "g" or "gb" => value * 1024L * 1024 * 1024,
            "" => value, // Assume bytes
            _ => value
        };
    }

    /// <summary>
    /// Validates Redis max memory policy.
    /// </summary>
    private ValidationResult ValidateRedisMaxMemoryPolicy(string maxMemoryPolicy)
    {
        if (string.IsNullOrWhiteSpace(maxMemoryPolicy))
        {
            return ValidationResult.Failure("Redis.MaxMemoryPolicy", "Redis max memory policy cannot be null or empty.", ValidationSeverity.Error);
        }

        var validPolicies = new[]
        {
            "noeviction", "allkeys-lru", "volatile-lru", "allkeys-random", "volatile-random",
            "volatile-ttl", "allkeys-lfu", "volatile-lfu"
        };

        if (!validPolicies.Contains(maxMemoryPolicy))
        {
            return ValidationResult.Failure(
                "Redis.MaxMemoryPolicy", 
                $"Invalid max memory policy '{maxMemoryPolicy}'. Valid policies: {string.Join(", ", validPolicies)}", 
                ValidationSeverity.Error);
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates Redis persistence settings.
    /// </summary>
    private ValidationResult ValidateRedisPersistence(RedisConfiguration redisConfig)
    {
        var result = ValidationResult.Success();

        // Validate persistence mode
        if (redisConfig.EnablePersistence)
        {
            result = result.Combine(ValidateRedisPersistenceMode(redisConfig.PersistenceMode));
        }

        // Validate AOF settings
        if (redisConfig.EnableAOF)
        {
            result = result.Combine(ValidateRedisAOFSettings(redisConfig));
        }

        // Check for conflicting persistence settings
        if (redisConfig.EnablePersistence && redisConfig.EnableAOF && redisConfig.PersistenceMode == "rdb")
        {
            result = result.Combine(ValidationResult.SuccessWithWarnings(
                new ValidationWarning(
                    "Redis.Persistence", 
                    "Both RDB and AOF persistence are enabled. This is valid but may impact performance.", 
                    ValidationSeverity.Info)));
        }

        return result;
    }

    /// <summary>
    /// Validates Redis persistence mode.
    /// </summary>
    private ValidationResult ValidateRedisPersistenceMode(string persistenceMode)
    {
        if (string.IsNullOrWhiteSpace(persistenceMode))
        {
            return ValidationResult.Failure("Redis.PersistenceMode", "Redis persistence mode cannot be null or empty when persistence is enabled.", ValidationSeverity.Error);
        }

        var validModes = new[] { "rdb", "aof", "both" };
        if (!validModes.Contains(persistenceMode))
        {
            return ValidationResult.Failure(
                "Redis.PersistenceMode", 
                $"Invalid persistence mode '{persistenceMode}'. Valid modes: {string.Join(", ", validModes)}", 
                ValidationSeverity.Error);
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates Redis AOF settings.
    /// </summary>
    private ValidationResult ValidateRedisAOFSettings(RedisConfiguration redisConfig)
    {
        var result = ValidationResult.Success();

        // AOF is enabled, check if persistence mode is compatible
        if (redisConfig.PersistenceMode == "rdb" && !redisConfig.EnableAOF)
        {
            result = result.Combine(ValidationResult.SuccessWithWarnings(
                new ValidationWarning(
                    "Redis.AOF", 
                    "AOF is enabled but persistence mode is 'rdb'. Consider setting persistence mode to 'aof' or 'both'.", 
                    ValidationSeverity.Warning)));
        }

        return result;
    }

    /// <summary>
    /// Validates Redis authentication settings.
    /// </summary>
    private ValidationResult ValidateRedisAuthentication(RedisConfiguration redisConfig)
    {
        var result = ValidationResult.Success();

        // If password is required, it must not be empty
        if (redisConfig.RequirePassword)
        {
            if (string.IsNullOrWhiteSpace(redisConfig.Password))
            {
                result = result.Combine(ValidationResult.Failure(
                    "Redis.Password", 
                    "Redis password is required but not provided.", 
                    ValidationSeverity.Critical));
            }
            else
            {
                // Validate password strength
                result = result.Combine(ValidateRedisPasswordStrength(redisConfig.Password));
            }
        }
        else
        {
            // Password not required, but if provided, warn about security
            if (!string.IsNullOrWhiteSpace(redisConfig.Password))
            {
                result = result.Combine(ValidationResult.SuccessWithWarnings(
                    new ValidationWarning(
                        "Redis.Password", 
                        "Password is provided but authentication is not required. Consider enabling authentication for security.", 
                        ValidationSeverity.Warning)));
            }
        }

        return result;
    }

    /// <summary>
    /// Validates Redis password strength.
    /// </summary>
    private ValidationResult ValidateRedisPasswordStrength(string password)
    {
        var warnings = new List<ValidationWarning>();

        // Check password length
        if (password.Length < 8)
        {
            warnings.Add(new ValidationWarning(
                "Redis.Password", 
                "Password is shorter than 8 characters. Consider using a longer password for better security.", 
                ValidationSeverity.Warning));
        }

        // Check for common weak passwords
        var weakPasswords = new[] { "password", "123456", "redis", "admin", "test" };
        if (weakPasswords.Contains(password.ToLowerInvariant()))
        {
            warnings.Add(new ValidationWarning(
                "Redis.Password", 
                "Password appears to be a common weak password. Consider using a stronger password.", 
                ValidationSeverity.Warning));
        }

        // Check for complexity
        var hasUpper = password.Any(char.IsUpper);
        var hasLower = password.Any(char.IsLower);
        var hasDigit = password.Any(char.IsDigit);
        var hasSpecial = password.Any(c => !char.IsLetterOrDigit(c));

        if (!hasUpper || !hasLower || !hasDigit || !hasSpecial)
        {
            warnings.Add(new ValidationWarning(
                "Redis.Password", 
                "Password should contain uppercase letters, lowercase letters, digits, and special characters for better security.", 
                ValidationSeverity.Info));
        }

        return warnings.Count > 0 
            ? ValidationResult.SuccessWithWarnings(warnings.ToArray())
            : ValidationResult.Success();
    }

    /// <summary>
    /// Validates Redis log level.
    /// </summary>
    private ValidationResult ValidateRedisLogLevel(string logLevel)
    {
        if (string.IsNullOrWhiteSpace(logLevel))
        {
            return ValidationResult.Failure("Redis.LogLevel", "Redis log level cannot be null or empty.", ValidationSeverity.Error);
        }

        var validLogLevels = new[] { "debug", "verbose", "notice", "warning" };
        if (!validLogLevels.Contains(logLevel.ToLowerInvariant()))
        {
            return ValidationResult.Failure(
                "Redis.LogLevel", 
                $"Invalid log level '{logLevel}'. Valid levels: {string.Join(", ", validLogLevels)}", 
                ValidationSeverity.Error);
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates Redis configuration and adds warnings for common issues.
    /// </summary>
    private ValidationResult ValidateRedisWarnings(RedisConfiguration redisConfig)
    {
        var warnings = new List<ValidationWarning>();

        // Check for default port
        if (redisConfig.Port == Constants.DefaultRedisPort)
        {
            warnings.Add(new ValidationWarning(
                "Redis.Port", 
                "Using default Redis port 6379. Ensure no conflicts with other Redis instances.", 
                ValidationSeverity.Info));
        }

        // Check for localhost binding
        if (redisConfig.BindAddress == "127.0.0.1" || redisConfig.BindAddress == "localhost")
        {
            warnings.Add(new ValidationWarning(
                "Redis.BindAddress", 
                "Redis is bound to localhost only. Remote connections will not be possible.", 
                ValidationSeverity.Info));
        }

        // Check for no authentication
        if (!redisConfig.RequirePassword)
        {
            warnings.Add(new ValidationWarning(
                "Redis.RequirePassword", 
                "Redis authentication is disabled. Consider enabling authentication for security.", 
                ValidationSeverity.Warning));
        }

        // Check for no persistence
        if (!redisConfig.EnablePersistence)
        {
            warnings.Add(new ValidationWarning(
                "Redis.EnablePersistence", 
                "Redis persistence is disabled. Data will be lost on restart.", 
                ValidationSeverity.Warning));
        }

        // Check for debug log level in production
        if (redisConfig.LogLevel.ToLowerInvariant() == "debug")
        {
            warnings.Add(new ValidationWarning(
                "Redis.LogLevel", 
                "Debug log level is enabled. This may impact performance in production.", 
                ValidationSeverity.Warning));
        }

        return warnings.Count > 0 
            ? ValidationResult.SuccessWithWarnings(warnings.ToArray())
            : ValidationResult.Success();
    }
}
