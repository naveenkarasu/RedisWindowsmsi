using LanguageExt;
using static LanguageExt.Prelude;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RedisServiceWrapper.Configuration.Validation;

/// <summary>
/// Represents a validation result with either success or failure information.
/// Uses functional Either pattern for error handling.
/// </summary>
public sealed record ValidationResult
{
    /// <summary>
    /// Indicates if the validation was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// List of validation errors (empty if successful).
    /// </summary>
    public Seq<ValidationError> Errors { get; init; } = toSeq(new ValidationError[] { });

    /// <summary>
    /// List of validation warnings (non-blocking issues).
    /// </summary>
    public Seq<ValidationWarning> Warnings { get; init; } = toSeq(new ValidationWarning[] { });

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success() => new() { IsSuccess = true };

    /// <summary>
    /// Creates a successful validation result with warnings.
    /// </summary>
    public static ValidationResult SuccessWithWarnings(params ValidationWarning[] warnings) =>
        new() { IsSuccess = true, Warnings = toSeq(warnings) };

    /// <summary>
    /// Creates a failed validation result with errors.
    /// </summary>
    public static ValidationResult Failure(params ValidationError[] errors) =>
        new() { IsSuccess = false, Errors = toSeq(errors) };

    /// <summary>
    /// Creates a failed validation result with a single error.
    /// </summary>
    public static ValidationResult Failure(string property, string message, ValidationSeverity severity = ValidationSeverity.Error) =>
        Failure(new ValidationError(property, message, severity));

    /// <summary>
    /// Combines this validation result with another.
    /// </summary>
    public ValidationResult Combine(ValidationResult other) =>
        new()
        {
            IsSuccess = IsSuccess && other.IsSuccess,
            Errors = Errors.Concat(other.Errors).ToSeq(),
            Warnings = Warnings.Concat(other.Warnings).ToSeq()
        };

    /// <summary>
    /// Gets a human-readable summary of the validation result.
    /// </summary>
    public string GetSummary()
    {
        if (IsSuccess && Warnings.IsEmpty)
            return "Validation successful.";

        var parts = new List<string>();
        
        if (!IsSuccess)
            parts.Add($"Validation failed with {Errors.Count} error(s).");
        
        if (!Warnings.IsEmpty)
            parts.Add($"{Warnings.Count} warning(s) found.");

        return string.Join(" ", parts);
    }
}

/// <summary>
/// Represents a validation error with property path, message, and severity.
/// </summary>
public sealed record ValidationError(
    string Property,
    string Message,
    ValidationSeverity Severity = ValidationSeverity.Error
)
{
    /// <summary>
    /// Gets a formatted error message.
    /// </summary>
    public string GetFormattedMessage() => $"{Property}: {Message}";
}

/// <summary>
/// Represents a validation warning (non-blocking issue).
/// </summary>
public sealed record ValidationWarning(
    string Property,
    string Message,
    ValidationSeverity Severity = ValidationSeverity.Warning
)
{
    /// <summary>
    /// Gets a formatted warning message.
    /// </summary>
    public string GetFormattedMessage() => $"{Property}: {Message}";
}

/// <summary>
/// Severity levels for validation issues.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>
    /// Informational message.
    /// </summary>
    Info,

    /// <summary>
    /// Warning that doesn't block validation.
    /// </summary>
    Warning,

    /// <summary>
    /// Error that blocks validation.
    /// </summary>
    Error,

    /// <summary>
    /// Critical error that must be fixed.
    /// </summary>
    Critical
}

/// <summary>
/// Base interface for configuration validators.
/// </summary>
/// <typeparam name="T">The type being validated</typeparam>
public interface IConfigurationValidator<in T>
{
    /// <summary>
    /// Validates the configuration object.
    /// </summary>
    /// <param name="config">The configuration to validate</param>
    /// <returns>Validation result</returns>
    ValidationResult Validate(T config);

    /// <summary>
    /// Gets the name of this validator.
    /// </summary>
    string ValidatorName { get; }
}

/// <summary>
/// Composite validator that combines multiple validators.
/// </summary>
/// <typeparam name="T">The type being validated</typeparam>
public sealed class CompositeValidator<T> : IConfigurationValidator<T>
{
    private readonly Seq<IConfigurationValidator<T>> _validators;

    public CompositeValidator(params IConfigurationValidator<T>[] validators)
    {
        _validators = toSeq(validators);
    }

    public string ValidatorName => $"Composite({string.Join(", ", _validators.Map(v => v.ValidatorName))})";

    public ValidationResult Validate(T config)
    {
        var result = ValidationResult.Success();

        foreach (var validator in _validators)
        {
            var validatorResult = validator.Validate(config);
            result = result.Combine(validatorResult);
        }

        return result;
    }
}

/// <summary>
/// Validator that checks if a string property is not null or empty.
/// </summary>
public static class StringValidators
{
    /// <summary>
    /// Validates that a string is not null or empty.
    /// </summary>
    public static ValidationResult NotNullOrEmpty(string value, string propertyName, string? customMessage = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            var message = customMessage ?? $"{propertyName} cannot be null or empty.";
            return ValidationResult.Failure(propertyName, message, ValidationSeverity.Error);
        }
        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates that a string is not null or empty, with warning if empty.
    /// </summary>
    public static ValidationResult NotNullOrEmptyWithWarning(string value, string propertyName, string? customMessage = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            var message = customMessage ?? $"{propertyName} is empty, using default value.";
            return ValidationResult.SuccessWithWarnings(new ValidationWarning(propertyName, message, ValidationSeverity.Warning));
        }
        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates that a string matches a specific pattern.
    /// </summary>
    public static ValidationResult MatchesPattern(string value, string pattern, string propertyName, string? customMessage = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ValidationResult.Success(); // Let NotNullOrEmpty handle this

        if (!System.Text.RegularExpressions.Regex.IsMatch(value, pattern))
        {
            var message = customMessage ?? $"{propertyName} does not match required pattern: {pattern}";
            return ValidationResult.Failure(propertyName, message, ValidationSeverity.Error);
        }
        return ValidationResult.Success();
    }
}

/// <summary>
/// Validator that checks numeric properties.
/// </summary>
public static class NumericValidators
{
    /// <summary>
    /// Validates that a number is within a specified range.
    /// </summary>
    public static ValidationResult InRange(int value, int min, int max, string propertyName, string? customMessage = null)
    {
        if (value < min || value > max)
        {
            var message = customMessage ?? $"{propertyName} must be between {min} and {max} (current: {value}).";
            return ValidationResult.Failure(propertyName, message, ValidationSeverity.Error);
        }
        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates that a number is positive.
    /// </summary>
    public static ValidationResult Positive(int value, string propertyName, string? customMessage = null)
    {
        if (value <= 0)
        {
            var message = customMessage ?? $"{propertyName} must be positive (current: {value}).";
            return ValidationResult.Failure(propertyName, message, ValidationSeverity.Error);
        }
        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates that a number is non-negative.
    /// </summary>
    public static ValidationResult NonNegative(int value, string propertyName, string? customMessage = null)
    {
        if (value < 0)
        {
            var message = customMessage ?? $"{propertyName} must be non-negative (current: {value}).";
            return ValidationResult.Failure(propertyName, message, ValidationSeverity.Error);
        }
        return ValidationResult.Success();
    }
}

/// <summary>
/// Validator that checks boolean properties.
/// </summary>
public static class BooleanValidators
{
    /// <summary>
    /// Validates a boolean property (always succeeds, but can be used for consistency).
    /// </summary>
    public static ValidationResult Validate(bool value, string propertyName)
    {
        // Boolean values are always valid
        return ValidationResult.Success();
    }
}

/// <summary>
/// Validator that checks collection properties.
/// </summary>
public static class CollectionValidators
{
    /// <summary>
    /// Validates that a collection is not empty.
    /// </summary>
    public static ValidationResult NotEmpty<T>(IEnumerable<T> collection, string propertyName, string? customMessage = null)
    {
        if (collection == null || !collection.Any())
        {
            var message = customMessage ?? $"{propertyName} cannot be empty.";
            return ValidationResult.Failure(propertyName, message, ValidationSeverity.Error);
        }
        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates that a collection has a maximum number of items.
    /// </summary>
    public static ValidationResult MaxCount<T>(IEnumerable<T> collection, int maxCount, string propertyName, string? customMessage = null)
    {
        if (collection != null && collection.Count() > maxCount)
        {
            var message = customMessage ?? $"{propertyName} cannot have more than {maxCount} items (current: {collection.Count()}).";
            return ValidationResult.Failure(propertyName, message, ValidationSeverity.Error);
        }
        return ValidationResult.Success();
    }
}

/// <summary>
/// Validator that checks file and path properties.
/// </summary>
public static class PathValidators
{
    /// <summary>
    /// Validates that a path is valid (not empty and properly formatted).
    /// </summary>
    public static ValidationResult ValidPath(string path, string propertyName, string? customMessage = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            var message = customMessage ?? $"{propertyName} cannot be null or empty.";
            return ValidationResult.Failure(propertyName, message, ValidationSeverity.Error);
        }

        try
        {
            var fullPath = System.IO.Path.GetFullPath(path);
            return ValidationResult.Success();
        }
        catch (Exception ex)
        {
            var message = customMessage ?? $"{propertyName} is not a valid path: {ex.Message}";
            return ValidationResult.Failure(propertyName, message, ValidationSeverity.Error);
        }
    }

    /// <summary>
    /// Validates that a path exists (for files that should exist).
    /// </summary>
    public static ValidationResult PathExists(string path, string propertyName, string? customMessage = null)
    {
        var pathResult = ValidPath(path, propertyName, customMessage);
        if (!pathResult.IsSuccess)
            return pathResult;

        if (!System.IO.File.Exists(path) && !System.IO.Directory.Exists(path))
        {
            var message = customMessage ?? $"{propertyName} path does not exist: {path}";
            return ValidationResult.Failure(propertyName, message, ValidationSeverity.Error);
        }
        return ValidationResult.Success();
    }
}

/// <summary>
/// Validator that checks network-related properties.
/// </summary>
public static class NetworkValidators
{
    /// <summary>
    /// Validates that a port number is valid (1-65535).
    /// </summary>
    public static ValidationResult ValidPort(int port, string propertyName, string? customMessage = null)
    {
        return NumericValidators.InRange(port, 1, 65535, propertyName, customMessage);
    }

    /// <summary>
    /// Validates that an IP address is valid.
    /// </summary>
    public static ValidationResult ValidIpAddress(string ipAddress, string propertyName, string? customMessage = null)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            var message = customMessage ?? $"{propertyName} cannot be null or empty.";
            return ValidationResult.Failure(propertyName, message, ValidationSeverity.Error);
        }

        if (!System.Net.IPAddress.TryParse(ipAddress, out _))
        {
            var message = customMessage ?? $"{propertyName} is not a valid IP address: {ipAddress}";
            return ValidationResult.Failure(propertyName, message, ValidationSeverity.Error);
        }
        return ValidationResult.Success();
    }
}

/// <summary>
/// Extension methods for validation results.
/// </summary>
public static class ValidationResultExtensions
{
    /// <summary>
    /// Converts a ValidationResult to an Either for functional composition.
    /// </summary>
    public static Either<Seq<ValidationError>, Unit> ToEither(this ValidationResult result)
    {
        return result.IsSuccess
            ? Right<Seq<ValidationError>, Unit>(unit)
            : Left<Seq<ValidationError>, Unit>(result.Errors);
    }

    /// <summary>
    /// Converts a ValidationResult to a Try for functional composition.
    /// </summary>
    public static Try<Unit> ToTry(this ValidationResult result)
    {
        return result.IsSuccess
            ? Try(() => unit)
            : Try<Unit>(() => throw new ValidationException(result.Errors));
    }
}

/// <summary>
/// Exception thrown when validation fails.
/// </summary>
public sealed class ValidationException : Exception
{
    public Seq<ValidationError> Errors { get; }

    public ValidationException(Seq<ValidationError> errors) : base(GetErrorMessage(errors))
    {
        Errors = errors;
    }

    private static string GetErrorMessage(Seq<ValidationError> errors)
    {
        var errorMessages = errors.Map(e => e.GetFormattedMessage());
        return $"Validation failed with {errors.Count} error(s):\n{string.Join("\n", errorMessages)}";
    }
}
