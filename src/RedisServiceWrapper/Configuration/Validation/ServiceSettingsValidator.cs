using LanguageExt;
using static LanguageExt.Prelude;
using RedisServiceWrapper.Configuration;
using RedisServiceWrapper.Configuration.Validation;
using System;
using System.Linq;

namespace RedisServiceWrapper.Configuration.Validation;

/// <summary>
/// Validates Windows Service-specific configuration settings.
/// Handles validation for service name, display name, start type, failure actions, etc.
/// </summary>
public sealed class ServiceSettingsValidator : IConfigurationValidator<ServiceConfiguration>
{
    public string ValidatorName => "ServiceSettingsValidator";

    public ValidationResult Validate(ServiceConfiguration config)
    {
        var result = ValidationResult.Success();

        // Validate service settings
        result = result.Combine(ValidateServiceSettings(config.Service));

        return result;
    }

    /// <summary>
    /// Validates Windows Service settings.
    /// </summary>
    private ValidationResult ValidateServiceSettings(ServiceSettings serviceSettings)
    {
        var result = ValidationResult.Success();

        // Validate service name
        result = result.Combine(ValidateServiceName(serviceSettings.ServiceName));

        // Validate display name
        result = result.Combine(ValidateDisplayName(serviceSettings.DisplayName));

        // Validate description
        result = result.Combine(ValidateDescription(serviceSettings.Description));

        // Validate start type
        result = result.Combine(ValidateStartType(serviceSettings.StartType));

        // Validate failure actions
        result = result.Combine(ValidateFailureActions(serviceSettings.FailureActions));

        // Add warnings for common issues
        result = result.Combine(ValidateServiceSettingsWarnings(serviceSettings));

        return result;
    }

    /// <summary>
    /// Validates Windows Service name.
    /// </summary>
    private ValidationResult ValidateServiceName(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            return ValidationResult.Failure("Service.ServiceName", "Service name cannot be null or empty.", ValidationSeverity.Critical);
        }

        // Windows service names have specific requirements
        if (serviceName.Length > 256)
        {
            return ValidationResult.Failure("Service.ServiceName", "Service name cannot exceed 256 characters.", ValidationSeverity.Error);
        }

        // Check for invalid characters
        var invalidChars = new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' };
        if (serviceName.Any(c => invalidChars.Contains(c)))
        {
            return ValidationResult.Failure(
                "Service.ServiceName", 
                $"Service name contains invalid characters: {string.Join(", ", invalidChars)}", 
                ValidationSeverity.Error);
        }

        // Check for reserved names
        var reservedNames = new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
        if (reservedNames.Contains(serviceName.ToUpperInvariant()))
        {
            return ValidationResult.Failure(
                "Service.ServiceName", 
                $"Service name '{serviceName}' is a reserved Windows name.", 
                ValidationSeverity.Error);
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates Windows Service display name.
    /// </summary>
    private ValidationResult ValidateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return ValidationResult.Failure("Service.DisplayName", "Service display name cannot be null or empty.", ValidationSeverity.Error);
        }

        // Display name length limit
        if (displayName.Length > 256)
        {
            return ValidationResult.Failure("Service.DisplayName", "Service display name cannot exceed 256 characters.", ValidationSeverity.Error);
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates Windows Service description.
    /// </summary>
    private ValidationResult ValidateDescription(string description)
    {
        // Description is optional, but if provided, should not be too long
        if (!string.IsNullOrWhiteSpace(description) && description.Length > 1024)
        {
            return ValidationResult.Failure("Service.Description", "Service description cannot exceed 1024 characters.", ValidationSeverity.Error);
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates Windows Service start type.
    /// </summary>
    private ValidationResult ValidateStartType(string startType)
    {
        if (string.IsNullOrWhiteSpace(startType))
        {
            return ValidationResult.Failure("Service.StartType", "Service start type cannot be null or empty.", ValidationSeverity.Error);
        }

        var validStartTypes = new[] { "Automatic", "Manual", "Disabled" };
        if (!validStartTypes.Contains(startType))
        {
            return ValidationResult.Failure(
                "Service.StartType", 
                $"Invalid start type '{startType}'. Valid types: {string.Join(", ", validStartTypes)}", 
                ValidationSeverity.Error);
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates Windows Service failure actions.
    /// </summary>
    private ValidationResult ValidateFailureActions(FailureActionsSettings failureActions)
    {
        var result = ValidationResult.Success();

        // Validate reset period
        result = result.Combine(NumericValidators.Positive(
            failureActions.ResetPeriod, 
            "Service.FailureActions.ResetPeriod", 
            "Failure actions reset period must be positive."));

        // Validate restart delay
        result = result.Combine(NumericValidators.NonNegative(
            failureActions.RestartDelay, 
            "Service.FailureActions.RestartDelay", 
            "Failure actions restart delay must be non-negative."));

        // Validate actions
        result = result.Combine(ValidateFailureActionList(failureActions.Actions));

        return result;
    }

    /// <summary>
    /// Validates the list of failure actions.
    /// </summary>
    private ValidationResult ValidateFailureActionList(Seq<ServiceAction> actions)
    {
        if (actions.IsEmpty)
        {
            return ValidationResult.SuccessWithWarnings(
                new ValidationWarning(
                    "Service.FailureActions.Actions", 
                    "No failure actions configured. Service will not automatically restart on failure.", 
                    ValidationSeverity.Warning));
        }

        var result = ValidationResult.Success();
        var actionIndex = 0;

        foreach (var action in actions)
        {
            result = result.Combine(ValidateFailureAction(action, actionIndex));
            actionIndex++;
        }

        // Check for reasonable number of actions
        if (actions.Count > 10)
        {
            result = result.Combine(ValidationResult.SuccessWithWarnings(
                new ValidationWarning(
                    "Service.FailureActions.Actions", 
                    $"Large number of failure actions ({actions.Count}). Consider simplifying the failure recovery strategy.", 
                    ValidationSeverity.Info)));
        }

        return result;
    }

    /// <summary>
    /// Validates a single failure action.
    /// </summary>
    private ValidationResult ValidateFailureAction(ServiceAction action, int index)
    {
        var result = ValidationResult.Success();
        var propertyPrefix = $"Service.FailureActions.Actions[{index}]";

        // Validate action type
        result = result.Combine(ValidateFailureActionType(action.Type, propertyPrefix));

        // Validate action delay
        result = result.Combine(NumericValidators.NonNegative(
            action.Delay, 
            $"{propertyPrefix}.Delay", 
            "Failure action delay must be non-negative."));

        // Check for reasonable delay values
        if (action.Delay > 300000) // 5 minutes
        {
            result = result.Combine(ValidationResult.SuccessWithWarnings(
                new ValidationWarning(
                    $"{propertyPrefix}.Delay", 
                    $"Long delay ({action.Delay}ms) for failure action. Consider if this is appropriate.", 
                    ValidationSeverity.Info)));
        }

        return result;
    }

    /// <summary>
    /// Validates failure action type.
    /// </summary>
    private ValidationResult ValidateFailureActionType(string actionType, string propertyPrefix)
    {
        if (string.IsNullOrWhiteSpace(actionType))
        {
            return ValidationResult.Failure($"{propertyPrefix}.Type", "Failure action type cannot be null or empty.", ValidationSeverity.Error);
        }

        var validActionTypes = new[] { "restart", "run_command", "reboot" };
        if (!validActionTypes.Contains(actionType.ToLowerInvariant()))
        {
            return ValidationResult.Failure(
                $"{propertyPrefix}.Type", 
                $"Invalid failure action type '{actionType}'. Valid types: {string.Join(", ", validActionTypes)}", 
                ValidationSeverity.Error);
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates service settings and adds warnings for common issues.
    /// </summary>
    private ValidationResult ValidateServiceSettingsWarnings(ServiceSettings serviceSettings)
    {
        var warnings = new List<ValidationWarning>();

        // Check for default service name
        if (serviceSettings.ServiceName == Constants.ServiceName)
        {
            warnings.Add(new ValidationWarning(
                "Service.ServiceName", 
                "Using default service name. Consider using a more descriptive name.", 
                ValidationSeverity.Info));
        }

        // Check for default display name
        if (serviceSettings.DisplayName == Constants.ServiceDisplayName)
        {
            warnings.Add(new ValidationWarning(
                "Service.DisplayName", 
                "Using default display name. Consider using a more descriptive name.", 
                ValidationSeverity.Info));
        }

        // Check for disabled service
        if (serviceSettings.StartType == "Disabled")
        {
            warnings.Add(new ValidationWarning(
                "Service.StartType", 
                "Service is set to 'Disabled'. It will not start automatically and must be started manually.", 
                ValidationSeverity.Warning));
        }

        // Check for manual start type
        if (serviceSettings.StartType == "Manual")
        {
            warnings.Add(new ValidationWarning(
                "Service.StartType", 
                "Service is set to 'Manual'. It will not start automatically on boot.", 
                ValidationSeverity.Info));
        }

        // Check for delayed auto start
        if (serviceSettings.StartType == "Automatic" && serviceSettings.DelayedAutoStart)
        {
            warnings.Add(new ValidationWarning(
                "Service.DelayedAutoStart", 
                "Delayed auto start is enabled. Service will start after other automatic services.", 
                ValidationSeverity.Info));
        }

        // Check for missing description
        if (string.IsNullOrWhiteSpace(serviceSettings.Description))
        {
            warnings.Add(new ValidationWarning(
                "Service.Description", 
                "Service description is empty. Consider adding a description for better service management.", 
                ValidationSeverity.Info));
        }

        // Check for failure actions configuration
        if (serviceSettings.FailureActions.Actions.IsEmpty)
        {
            warnings.Add(new ValidationWarning(
                "Service.FailureActions.Actions", 
                "No failure actions configured. Service will not automatically recover from failures.", 
                ValidationSeverity.Warning));
        }

        // Check for very short reset period
        if (serviceSettings.FailureActions.ResetPeriod < 3600) // Less than 1 hour
        {
            warnings.Add(new ValidationWarning(
                "Service.FailureActions.ResetPeriod", 
                "Short reset period for failure actions. Consider if this is appropriate for your use case.", 
                ValidationSeverity.Info));
        }

        // Check for very long reset period
        if (serviceSettings.FailureActions.ResetPeriod > 86400 * 7) // More than 1 week
        {
            warnings.Add(new ValidationWarning(
                "Service.FailureActions.ResetPeriod", 
                "Long reset period for failure actions. Consider if this is appropriate for your use case.", 
                ValidationSeverity.Info));
        }

        return warnings.Count > 0 
            ? ValidationResult.SuccessWithWarnings(warnings.ToArray())
            : ValidationResult.Success();
    }
}
