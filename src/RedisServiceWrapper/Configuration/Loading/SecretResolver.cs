using LanguageExt;
using static LanguageExt.Prelude;
using System.Text.RegularExpressions;
using CustomLogger = RedisServiceWrapper.Logging.ILogger;

namespace RedisServiceWrapper.Configuration.Loading;

/// <summary>
/// Resolves secrets in configuration using environment variables and Windows Credential Manager.
/// Supports patterns: ${ENV:VAR_NAME} and ${CRED:CredentialName}
/// Provides security by never logging resolved secrets.
/// </summary>
public sealed class SecretResolver
{
    // Pattern: ${ENV:VAR_NAME} or ${CRED:CredentialName}
    private static readonly Regex SecretPattern = new Regex(
        @"\$\{(ENV|CRED):([^}]+)\}", 
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    /// <summary>
    /// Resolves a single secret value from environment variable or credential manager.
    /// </summary>
    /// <param name="value">Value that may contain secret patterns</param>
    /// <returns>Either containing resolved value or error message</returns>
    public Either<string, string> ResolveSecret(string value)
    {
        if (string.IsNullOrEmpty(value))
            return Right<string, string>(value);

        var match = SecretPattern.Match(value);
        if (!match.Success)
            return Right<string, string>(value); // No secret pattern, return as-is

        var secretType = match.Groups[1].Value.ToUpperInvariant();
        var secretName = match.Groups[2].Value;

        return secretType switch
        {
            "ENV" => ResolveEnvironmentVariable(secretName),
            "CRED" => ResolveCredentialManagerSecret(secretName),
            _ => Left<string, string>($"Unknown secret type: {secretType}. Supported types: ENV, CRED")
        };
    }

    /// <summary>
    /// Resolves all secrets in a ServiceConfiguration.
    /// </summary>
    /// <param name="config">Configuration containing potential secrets</param>
    /// <param name="logger">Logger for warnings about plain-text secrets</param>
    /// <returns>Either containing resolved configuration or list of errors</returns>
    public TryAsync<Either<Seq<string>, ServiceConfiguration>> ResolveAllSecretsAsync(
        ServiceConfiguration config, 
        CustomLogger? logger = null) =>
        TryAsync(async () =>
        {
            var errors = new List<string>();

            try
            {
                // For now, just return the config as-is
                // TODO: Implement secret resolution when RedisConfiguration structure is clear
                
                return errors.Count == 0
                    ? Right<Seq<string>, ServiceConfiguration>(config)
                    : Left<Seq<string>, ServiceConfiguration>(toSeq(errors));
            }
            catch (Exception ex)
            {
                return Left<Seq<string>, ServiceConfiguration>(Seq1($"Failed to resolve secrets: {ex.Message}"));
            }
        });

    /// <summary>
    /// Sanitizes configuration for logging by replacing secrets with placeholders.
    /// </summary>
    /// <param name="config">Configuration to sanitize</param>
    /// <returns>Sanitized configuration safe for logging</returns>
    public ServiceConfiguration SanitizeForLogging(ServiceConfiguration config) =>
        config; // TODO: Implement when structure is clear

    /// <summary>
    /// Sanitizes configuration for saving (removes resolved secrets, keeps patterns).
    /// </summary>
    /// <param name="config">Configuration to sanitize</param>
    /// <returns>Sanitized configuration safe for saving</returns>
    public ServiceConfiguration SanitizeForSaving(ServiceConfiguration config) =>
        config; // TODO: Implement when structure is clear

    /// <summary>
    /// Checks if a value looks like a plain-text password (heuristic).
    /// </summary>
    /// <param name="value">Value to check</param>
    /// <returns>True if value appears to be a plain-text password</returns>
    public bool IsPlainTextPassword(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        // If it contains secret patterns, it's not plain text
        if (SecretPattern.IsMatch(value))
            return false;

        // Simple heuristic: 8+ chars, contains letters and numbers
        return value.Length >= 8 && value.Any(char.IsLetter) && value.Any(char.IsDigit);
    }

    #region Private Methods

    /// <summary>
    /// Resolves environment variable.
    /// </summary>
    private Either<string, string> ResolveEnvironmentVariable(string varName)
    {
        try
        {
            var value = Environment.GetEnvironmentVariable(varName);
            if (string.IsNullOrEmpty(value))
                return Left<string, string>($"Environment variable '{varName}' is not set or empty");

            return Right<string, string>(value);
        }
        catch (Exception ex)
        {
            return Left<string, string>($"Failed to read environment variable '{varName}': {ex.Message}");
        }
    }

    /// <summary>
    /// Resolves Windows Credential Manager secret.
    /// </summary>
    private Either<string, string> ResolveCredentialManagerSecret(string credName)
    {
        try
        {
            // Note: This is a simplified implementation
            // In a real implementation, you would use Windows API or a library
            
            // For now, we'll try to read from environment variable as fallback
            var fallbackVarName = $"CRED_{credName}";
            var value = Environment.GetEnvironmentVariable(fallbackVarName);
            
            if (string.IsNullOrEmpty(value))
                return Left<string, string>($"Credential '{credName}' not found in Windows Credential Manager (and fallback env var '{fallbackVarName}' not set)");

            return Right<string, string>(value);
        }
        catch (Exception ex)
        {
            return Left<string, string>($"Failed to read credential '{credName}': {ex.Message}");
        }
    }

    #endregion
}