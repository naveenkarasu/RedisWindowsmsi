# Phase 3 - Group B: Configuration Management - Detailed Plan (REVISED)

## Overview
Group B focuses on implementing a robust, production-ready, functional configuration management system that reads, validates, and provides access to the `backend.json` configuration file with security, safety, and maintainability as core principles.

**Status:** 📋 Planning Complete (Revised with Senior Engineer Review)  
**Estimated Time:** 7 hours 20 minutes  
**Approach:** Functional programming with LanguageExt, Railway-oriented programming for validation, secure secrets management, and safe hot-reload

---

## 🎯 Critical Improvements Incorporated

Based on senior engineering review, this plan now includes:

1. ✅ **Configuration Hot Reload Safety** - Prevents service instability
2. ✅ **Secrets Management** - Secure password handling
3. ✅ **Business Rule Validation** - Runtime system checks
4. ✅ **User-Friendly Error Messages** - Better UX
5. ✅ **Configuration Caching** - Performance optimization
6. ✅ **Configuration Versioning** - Future-proofing
7. ✅ **Separated Validation Concerns** - Better maintainability

**Deferred to Future:** Environment-specific configurations (dev/staging/prod) - will implement after working prototype

---

## Task 3.5: Implement Configuration Reader & Management

**Objective:** Create a functional, secure, and performant configuration management system

### Subtask 3.5.1: Create ConfigurationManager Class (Core)
- **File:** `src/RedisServiceWrapper/Configuration/Loading/ConfigurationManager.cs`
- **Responsibilities:**
  - Load configuration from JSON file
  - Parse JSON to ServiceConfiguration
  - Handle file not found scenarios
  - Handle invalid JSON scenarios
  - Orchestrate all loading operations
- **Functional Approach:**
  - Return `TryAsync<ServiceConfiguration>` for all operations
  - Pure functions for JSON parsing
  - Immutable configuration once loaded
- **Estimated Time:** 35 minutes

**Key Methods:**
```csharp
public sealed class ConfigurationManager
{
    private readonly ILogger _logger;
    private readonly ConfigurationCache _cache;
    private readonly SecretResolver _secretResolver;
    
    // Core loading
    public TryAsync<ServiceConfiguration> LoadConfiguration(string configPath);
    public TryAsync<ServiceConfiguration> LoadConfigurationOrDefault(string configPath);
    public static Try<JObject> ParseJsonFile(string path);
    public static Try<ServiceConfiguration> DeserializeConfiguration(JObject json);
    
    // Save configuration
    public TryAsync<Unit> SaveConfiguration(ServiceConfiguration config, string configPath);
}
```

### Subtask 3.5.2: Implement Configuration Caching ⚡ (NEW - CRITICAL)
- **File:** `src/RedisServiceWrapper/Configuration/Loading/ConfigurationCache.cs`
- **Responsibilities:**
  - Thread-safe in-memory configuration cache
  - Lazy loading pattern
  - Cache invalidation support
  - Immutable cache using Atom<T> from LanguageExt
- **Functional Approach:**
  - Use `Atom<Option<ServiceConfiguration>>` for thread-safe cache
  - Pure cache operations
  - No locks, using atomic operations
- **Estimated Time:** 25 minutes

**Key Implementation:**
```csharp
public sealed class ConfigurationCache
{
    private readonly Atom<Option<ServiceConfiguration>> _cache = Atom(Option<ServiceConfiguration>.None);
    
    public Option<ServiceConfiguration> Get() => _cache.Value;
    
    public Unit Set(ServiceConfiguration config)
    {
        _cache.Swap(_ => Option<ServiceConfiguration>.Some(config));
        return unit;
    }
    
    public Unit Invalidate()
    {
        _cache.Swap(_ => Option<ServiceConfiguration>.None);
        return unit;
    }
    
    public TryAsync<ServiceConfiguration> GetOrLoad(
        Func<TryAsync<ServiceConfiguration>> loader) =>
        TryAsync(async () =>
        {
            var cached = _cache.Value;
            if (cached.IsSome) return cached.IfNone(() => throw new Exception("Unreachable"));
            
            var loaded = await loader();
            Set(loaded);
            return loaded;
        });
}
```

### Subtask 3.5.3: Implement Secrets Management 🔒 (NEW - CRITICAL)
- **File:** `src/RedisServiceWrapper/Configuration/Loading/SecretResolver.cs`
- **Responsibilities:**
  - Support environment variable substitution: `${ENV:REDIS_PASSWORD}`
  - Support Windows Credential Manager: `${CRED:RedisPassword}`
  - Detect and warn about plain-text passwords
  - Resolve secrets throughout configuration tree
- **Functional Approach:**
  - Pure secret resolution logic
  - Pattern matching for secret formats
  - Return `Either<string, string>` for resolution errors
- **Estimated Time:** 35 minutes

**Key Implementation:**
```csharp
public static class SecretResolver
{
    // Pattern: ${ENV:VAR_NAME} or ${CRED:CredentialName}
    private static readonly Regex SecretPattern = new Regex(@"\$\{(ENV|CRED):([^}]+)\}");
    
    // Resolve a single secret value
    public static Either<string, string> ResolveSecret(string value);
    
    // Resolve environment variable
    private static Either<string, string> ResolveEnvironmentVariable(string varName);
    
    // Resolve Windows Credential Manager entry
    private static Either<string, string> ResolveCredentialManagerSecret(string credName);
    
    // Check if value is plain-text password (heuristic)
    public static bool IsPlainTextPassword(string value);
    
    // Resolve all secrets in configuration
    public static Either<Seq<string>, ServiceConfiguration> ResolveAllSecrets(
        ServiceConfiguration config, 
        ILogger logger);
    
    // Sanitize secrets for logging
    public static ServiceConfiguration SanitizeForLogging(ServiceConfiguration config);
}
```

**Example Usage in backend.json:**
```json
{
  "redis": {
    "password": "${ENV:REDIS_PASSWORD}",
    "port": 6379
  }
}
```

### Subtask 3.5.4: Implement Configuration File Watcher
- **File:** `src/RedisServiceWrapper/Configuration/Loading/ConfigurationWatcher.cs`
- **Responsibilities:**
  - Watch `backend.json` for changes
  - Reload configuration on file change
  - Emit configuration change events
  - Debounce rapid file changes
- **Functional Approach:**
  - Use `IObservable<ConfigurationChange>` pattern
  - Side effects isolated in watcher
  - Return structured change notifications
- **Estimated Time:** 30 minutes

**Key Methods:**
```csharp
public sealed class ConfigurationWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly Subject<FileSystemEventArgs> _fileChanges;
    
    public IObservable<ConfigurationChange> WatchConfiguration(
        string configPath,
        Func<TryAsync<ServiceConfiguration>> loader);
    
    private IObservable<ConfigurationChange> DebounceAndValidate(
        IObservable<FileSystemEventArgs> changes,
        Func<TryAsync<ServiceConfiguration>> loader);
}
```

### Subtask 3.5.5: Implement Configuration Hot Reload Safety 🛡️ (NEW - CRITICAL)
- **File:** `src/RedisServiceWrapper/Configuration/Loading/ConfigurationChangeAnalyzer.cs`
- **Responsibilities:**
  - Analyze differences between old and new configurations
  - Determine if changes require service restart
  - Validate new config before applying
  - Support rollback on failed reload
  - Provide "apply on next restart" strategy
- **Functional Approach:**
  - Pure comparison functions
  - Return comprehensive change analysis
  - Use pattern matching for change types
- **Estimated Time:** 35 minutes

**Key Implementation:**
```csharp
public sealed record ConfigurationChange(
    ServiceConfiguration OldConfig,
    ServiceConfiguration NewConfig,
    Seq<string> ChangedProperties,
    bool RequiresRestart,
    ChangeImpact Impact,
    Seq<string> Warnings
);

public enum ChangeImpact
{
    None,           // No changes
    MinorConfig,    // Can be applied without restart (e.g., log level)
    RestartRequired // Requires service restart (e.g., backend type, port)
}

public static class ConfigurationChangeAnalyzer
{
    // Analyze configuration change
    public static ConfigurationChange AnalyzeChange(
        ServiceConfiguration oldConfig, 
        ServiceConfiguration newConfig);
    
    // Determine if specific property change requires restart
    private static bool RequiresRestart(string propertyPath);
    
    // Calculate change impact
    private static ChangeImpact CalculateImpact(Seq<string> changedProperties);
    
    // Safe apply configuration change
    public static TryAsync<Unit> ApplyConfigurationChange(
        ConfigurationChange change,
        Func<ServiceConfiguration, TryAsync<Unit>> applyFunc,
        ILogger logger);
}
```

### Subtask 3.5.6: Implement Configuration Versioning 🔄 (NEW - CRITICAL)
- **File:** `src/RedisServiceWrapper/Configuration/Loading/ConfigurationVersioning.cs`
- **Responsibilities:**
  - Add schema version to configuration
  - Implement migration logic for old configs
  - Backward compatibility strategy
  - Version validation
- **Functional Approach:**
  - Pure migration functions
  - Version-specific transformations
  - Return Either for migration errors
- **Estimated Time:** 30 minutes

**Key Implementation:**
```csharp
public sealed record ConfigurationSchema(
    string Version,
    DateTime CreatedAt,
    Option<string> Description
)
{
    public static readonly string CurrentVersion = "1.0.0";
    
    public static ConfigurationSchema Current() => 
        new(CurrentVersion, DateTime.UtcNow, Option<string>.None);
}

// Updated ServiceConfiguration to include schema
public sealed record ServiceConfiguration(
    ConfigurationSchema Schema,
    BackendConfig Backend,
    RedisConfiguration Redis,
    // ... other properties
)
{
    public bool IsCurrentVersion() => 
        Schema.Version == ConfigurationSchema.CurrentVersion;
}

public static class ConfigurationMigrator
{
    // Migrate configuration to latest version
    public static Either<string, ServiceConfiguration> MigrateToLatest(
        JObject json,
        string fromVersion);
    
    // Version-specific migrations
    private static Either<string, JObject> MigrateFrom_0_9_To_1_0(JObject config);
    
    // Add schema to legacy configs
    public static JObject AddSchemaIfMissing(JObject config);
}
```

**Updated backend.json format:**
```json
{
  "schema": {
    "version": "1.0.0",
    "createdAt": "2025-10-15T10:30:00Z"
  },
  "backend": {
    "type": "WSL2",
    ...
  }
}
```

### Subtask 3.5.7: Create Default Configuration Generator
- **File:** `src/RedisServiceWrapper/Configuration/Loading/DefaultConfiguration.cs`
- **Responsibilities:**
  - Generate sensible defaults for missing config
  - Create configuration templates
  - Backend-specific defaults (WSL2 vs Docker)
- **Functional Approach:**
  - Pure functions only
  - Factory methods for each backend type
  - Return immutable configurations
- **Estimated Time:** 25 minutes

**Key Methods:**
```csharp
public static class DefaultConfiguration
{
    public static ServiceConfiguration GetDefault();
    public static ServiceConfiguration GetDefaultWSL2();
    public static ServiceConfiguration GetDefaultDocker();
    
    // Merge partial config with defaults (uses functional merge)
    public static ServiceConfiguration MergeWithDefaults(ServiceConfiguration partial);
    
    // Export default config to file
    public static TryAsync<Unit> ExportDefaultConfiguration(string path);
}
```

### Subtask 3.5.8: Add Configuration Logging
- **File:** Integrated into `ConfigurationManager.cs`
- **Responsibilities:**
  - Log configuration loading attempts
  - Log validation errors
  - Log configuration changes
  - Sanitize passwords/secrets from logs (use SecretResolver)
- **Functional Approach:**
  - Use existing ILogger interface
  - Log at appropriate levels
  - Pure function for sanitization (already in SecretResolver)
- **Estimated Time:** 15 minutes

**Task 3.5 Total Estimated Time:** 3 hours 50 minutes

---

## Task 3.6: Enhance Configuration Models

**Objective:** Review and enhance configuration models with helpers, validation, and serialization support

### Subtask 3.6.1: Add Schema to ServiceConfiguration
- **File:** `src/RedisServiceWrapper/Configuration/Models/ServiceConfiguration.cs` (EXISTING)
- **Actions:**
  - Add `ConfigurationSchema` property
  - Update all factory methods to include schema
  - Add helper: `IsCurrentVersion()`
- **Estimated Time:** 15 minutes

### Subtask 3.6.2: Review and Document Existing Models
- **File:** `src/RedisServiceWrapper/Configuration/Models/ServiceConfiguration.cs` (EXISTING)
- **Actions:**
  - Review all 9 record types
  - Add comprehensive XML documentation
  - Ensure all properties have descriptions
  - Verify immutability (init-only properties)
- **Estimated Time:** 20 minutes

**Models to Review:**
- ✅ `ServiceConfiguration` - Root configuration
- ✅ `BackendConfig` - Backend type selection
- ✅ `WslConfiguration` - WSL2 settings
- ✅ `DockerConfiguration` - Docker settings
- ✅ `RedisConfiguration` - Redis settings
- ✅ `ServiceSettings` - Windows Service settings
- ✅ `MonitoringConfiguration` - Health monitoring
- ✅ `PerformanceConfiguration` - Resource limits
- ✅ `AdvancedConfiguration` - Custom scripts/env vars

### Subtask 3.6.3: Add Helper Methods to Configuration Models
- **File:** Same file, `ServiceConfiguration.cs`
- **Responsibilities:**
  - Add useful helper methods to each record
  - Pure functions for common operations
  - Type checking helpers
  - Computed properties
- **Estimated Time:** 30 minutes

**Example Helper Methods:**
```csharp
// BackendConfig
public bool IsWSL2() => 
    BackendType.Equals(Constants.BackendTypeWSL2, StringComparison.OrdinalIgnoreCase);

public bool IsDocker() => 
    BackendType.Equals(Constants.BackendTypeDocker, StringComparison.OrdinalIgnoreCase);

public Either<string, Unit> ValidateBackendSelection() =>
    IsWSL2() || IsDocker()
        ? Right<string, Unit>(unit)
        : Left<string, Unit>("Backend type must be 'WSL2' or 'Docker'");

// RedisConfiguration
public bool IsPasswordProtected() => 
    !string.IsNullOrWhiteSpace(Password);

public int GetPortOrDefault() => 
    Port > 0 ? Port : Constants.DefaultRedisPort;

public bool IsPasswordResolved() => 
    !Password.Contains("${");

// MonitoringConfiguration
public TimeSpan GetHealthCheckInterval() => 
    TimeSpan.FromSeconds(HealthCheckIntervalSeconds);

public TimeSpan GetHealthCheckTimeout() => 
    TimeSpan.FromSeconds(HealthCheckTimeoutSeconds);

// PerformanceConfiguration
public long GetMaxMemoryBytes() => 
    MaxMemoryMb * 1024L * 1024L;
```

### Subtask 3.6.4: Add Configuration Builder Pattern
- **File:** `src/RedisServiceWrapper/Configuration/Builders/ServiceConfigurationBuilder.cs`
- **Responsibilities:**
  - Fluent builder for creating configurations
  - Useful for testing and programmatic config
  - Immutable builder pattern
- **Functional Approach:**
  - Each method returns new builder instance
  - Build() returns ServiceConfiguration
  - Validation on Build()
- **Estimated Time:** 35 minutes

**Example Implementation:**
```csharp
public sealed class ServiceConfigurationBuilder
{
    private ConfigurationSchema _schema = ConfigurationSchema.Current();
    private BackendConfig? _backend;
    private RedisConfiguration? _redis;
    // ... other fields
    
    public static ServiceConfigurationBuilder Create() => new();
    
    // Fluent methods
    public ServiceConfigurationBuilder WithWSL2Backend(
        string distribution = Constants.DefaultWslDistribution) =>
        new ServiceConfigurationBuilder(this) { _backend = /* WSL2 config */ };
    
    public ServiceConfigurationBuilder WithDockerBackend(
        string image = Constants.DefaultDockerImage) =>
        new ServiceConfigurationBuilder(this) { _backend = /* Docker config */ };
    
    public ServiceConfigurationBuilder WithRedisPort(int port) =>
        new ServiceConfigurationBuilder(this) { _redis = _redis with { Port = port } };
    
    public ServiceConfigurationBuilder WithHealthChecks(
        bool enabled, int intervalSeconds) =>
        // ... builder pattern
    
    public ServiceConfigurationBuilder WithAutoRestart(bool enabled) =>
        // ... builder pattern
    
    // Build with validation
    public Either<Seq<ValidationError>, ServiceConfiguration> Build();
}
```

### Subtask 3.6.5: Add JSON Serialization Attributes
- **File:** `ServiceConfiguration.cs`
- **Responsibilities:**
  - Add `[JsonPropertyName]` attributes
  - Add `[JsonIgnore]` for computed properties
  - Configure JSON serialization options
  - Ensure proper round-trip serialization
- **Estimated Time:** 20 minutes

**Example:**
```csharp
using System.Text.Json.Serialization;

public sealed record ServiceConfiguration(
    [property: JsonPropertyName("schema")] ConfigurationSchema Schema,
    [property: JsonPropertyName("backend")] BackendConfig Backend,
    [property: JsonPropertyName("redis")] RedisConfiguration Redis,
    // ...
)
{
    [JsonIgnore]
    public bool IsConfigured => Backend != null && Redis != null;
    
    [JsonIgnore]
    public bool IsCurrentVersion() => Schema.Version == ConfigurationSchema.CurrentVersion;
}
```

**Task 3.6 Total Estimated Time:** 2 hours

---

## Task 3.7: Implement Configuration Validation

**Objective:** Create comprehensive validation using Railway-oriented programming with separated concerns

### Subtask 3.7.1: Create Validation Infrastructure
- **File:** `src/RedisServiceWrapper/Configuration/Validation/ValidationInfrastructure.cs`
- **Responsibilities:**
  - Define validation error types
  - User-friendly error messages
  - Helper methods for validation composition
- **Functional Approach:**
  - Use `Either<Seq<ValidationError>, T>`
  - Accumulate all errors (not fail-fast)
  - Pure validation functions
- **Estimated Time:** 25 minutes

**Key Types:**
```csharp
public sealed record ValidationError(
    string PropertyPath,
    string TechnicalMessage,
    string UserFriendlyMessage,
    Option<string> SuggestedFix,
    Option<string> DocumentationUrl,
    ValidationSeverity Severity
)
{
    // Factory methods for common errors
    public static ValidationError RequiredField(string propertyPath) =>
        new(
            propertyPath,
            $"{propertyPath} is required",
            $"Please provide a value for {propertyPath}",
            Option<string>.Some($"Add '{propertyPath}' to your configuration"),
            Option<string>.None,
            ValidationSeverity.Error
        );
    
    public static ValidationError InvalidRange(
        string propertyPath, 
        int value, 
        int min, 
        int max) =>
        new(
            propertyPath,
            $"{propertyPath} must be between {min} and {max}, but was {value}",
            $"The value for {propertyPath} ({value}) is outside the valid range",
            Option<string>.Some($"Set {propertyPath} to a value between {min} and {max}"),
            Option<string>.None,
            ValidationSeverity.Error
        );
}

public enum ValidationSeverity { Error, Warning, Info }

public static class ValidationResult
{
    // Success
    public static Either<Seq<ValidationError>, T> Success<T>(T value) =>
        Right<Seq<ValidationError>, T>(value);
    
    // Single error
    public static Either<Seq<ValidationError>, T> Failure<T>(ValidationError error) =>
        Left<Seq<ValidationError>, T>(Seq1(error));
    
    // Multiple errors
    public static Either<Seq<ValidationError>, T> Failure<T>(params ValidationError[] errors) =>
        Left<Seq<ValidationError>, T>(toSeq(errors));
    
    // Combine validations (accumulate errors)
    public static Either<Seq<ValidationError>, Unit> Combine(
        params Either<Seq<ValidationError>, Unit>[] results);
    
    // Format errors for display
    public static string FormatErrors(Seq<ValidationError> errors);
}
```

### Subtask 3.7.2: Implement Backend Validator 🔧 (SEPARATED)
- **File:** `src/RedisServiceWrapper/Configuration/Validation/BackendValidator.cs`
- **Responsibilities:**
  - Validate backend type selection (WSL2 or Docker)
  - Ensure backend-specific config exists
  - Validate WSL2 distribution name
  - Validate Docker image name format
  - Check path validity
- **Functional Approach:**
  - Pure validation functions
  - Return `Either<Seq<ValidationError>, Unit>`
  - Composable validations
- **Estimated Time:** 30 minutes

**Key Methods:**
```csharp
public static class BackendValidator
{
    public static Either<Seq<ValidationError>, Unit> ValidateBackendConfig(BackendConfig backend);
    
    public static Either<Seq<ValidationError>, Unit> ValidateWslConfiguration(
        WslConfiguration wsl);
    
    public static Either<Seq<ValidationError>, Unit> ValidateDockerConfiguration(
        DockerConfiguration docker);
    
    private static Either<Seq<ValidationError>, Unit> ValidateDistributionName(string name);
    private static Either<Seq<ValidationError>, Unit> ValidateDockerImage(string image);
    private static Either<Seq<ValidationError>, Unit> ValidatePath(string path, string propertyPath);
}
```

### Subtask 3.7.3: Implement Redis Validator 🔧 (SEPARATED)
- **File:** `src/RedisServiceWrapper/Configuration/Validation/RedisValidator.cs`
- **Responsibilities:**
  - Validate port range (1-65535)
  - Validate memory limits (reasonable values)
  - Check path existence and permissions
  - Validate password format (if provided)
  - Check for common misconfigurations
- **Functional Approach:**
  - Range validation helpers
  - Path validation with Try
  - Accumulate errors
- **Estimated Time:** 25 minutes

**Key Methods:**
```csharp
public static class RedisValidator
{
    public static Either<Seq<ValidationError>, Unit> ValidateRedisConfiguration(
        RedisConfiguration redis);
    
    public static Either<Seq<ValidationError>, Unit> ValidatePort(int port);
    
    public static Either<Seq<ValidationError>, Unit> ValidateMemoryLimit(int memoryMb);
    
    public static Either<Seq<ValidationError>, Unit> ValidateDataPath(string path);
    
    public static Either<Seq<ValidationError>, Unit> ValidatePassword(string password);
}
```

### Subtask 3.7.4: Implement Service Settings Validator 🔧 (SEPARATED)
- **File:** `src/RedisServiceWrapper/Configuration/Validation/ServiceSettingsValidator.cs`
- **Responsibilities:**
  - Validate service name format (Windows service naming rules)
  - Validate health check intervals (reasonable values)
  - Validate restart delay settings
  - Check for conflicting settings
  - Detect suboptimal configurations (warnings)
- **Functional Approach:**
  - Business rule validation
  - Cross-property validation
  - Return warnings for suboptimal configs
- **Estimated Time:** 25 minutes

**Key Methods:**
```csharp
public static class ServiceSettingsValidator
{
    public static Either<Seq<ValidationError>, Unit> ValidateServiceSettings(
        ServiceSettings service);
    
    public static Either<Seq<ValidationError>, Unit> ValidateMonitoringConfiguration(
        MonitoringConfiguration monitoring);
    
    public static Either<Seq<ValidationError>, Unit> ValidatePerformanceConfiguration(
        PerformanceConfiguration performance);
    
    public static Either<Seq<ValidationError>, Unit> ValidateAdvancedConfiguration(
        AdvancedConfiguration advanced);
}
```

### Subtask 3.7.5: Implement System Validator 🔧 (SEPARATED - NEW - CRITICAL)
- **File:** `src/RedisServiceWrapper/Configuration/Validation/SystemValidator.cs`
- **Responsibilities:**
  - **Business rule validation** - Check actual system state
  - Validate backend availability (WSL2 installed, Docker running)
  - Validate port availability (not in use)
  - Check disk space for data directory
  - Validate memory limit vs available system memory
  - Check file system permissions
- **Functional Approach:**
  - Runtime validation with I/O
  - Return `TryAsync<Either<Seq<ValidationError>, Unit>>`
  - Integration with PowerShell module for WSL/Docker checks
- **Estimated Time:** 35 minutes

**Key Methods:**
```csharp
public static class SystemValidator
{
    // Validate backend availability
    public static TryAsync<Either<Seq<ValidationError>, Unit>> ValidateBackendAvailability(
        BackendConfig backend);
    
    // Check if port is available
    public static TryAsync<Either<Seq<ValidationError>, Unit>> ValidatePortAvailability(
        int port);
    
    // Check disk space
    public static TryAsync<Either<Seq<ValidationError>, Unit>> ValidateDiskSpace(
        string dataPath, 
        long requiredBytes);
    
    // Check available memory
    public static TryAsync<Either<Seq<ValidationError>, Unit>> ValidateSystemMemory(
        int requestedMemoryMb);
    
    // Check file system permissions
    public static TryAsync<Either<Seq<ValidationError>, Unit>> ValidatePathPermissions(
        string path,
        bool requireWrite);
    
    // Integration with PowerShell module
    private static TryAsync<bool> IsWSL2Installed();
    private static TryAsync<bool> IsDockerRunning();
}
```

### Subtask 3.7.6: Implement Configuration Validator (Orchestrator)
- **File:** `src/RedisServiceWrapper/Configuration/Validation/ConfigurationValidator.cs`
- **Responsibilities:**
  - Orchestrate all validation methods
  - Accumulate all errors and warnings
  - Perform syntactic validation first, then semantic
  - Return comprehensive validation result
  - Integration with ServiceConfiguration.Validate()
- **Functional Approach:**
  - Compose all validators
  - Use Applicative functor pattern to accumulate errors
  - Two-phase validation: syntactic → semantic
  - Return Either with all errors or validated config
- **Estimated Time:** 30 minutes

**Key Methods:**
```csharp
public static class ConfigurationValidator
{
    // Complete configuration validation (syntactic + semantic)
    public static TryAsync<Either<Seq<ValidationError>, ServiceConfiguration>> 
        ValidateConfiguration(ServiceConfiguration config, bool includeSystemChecks = true);
    
    // Syntactic validation only (no I/O)
    public static Either<Seq<ValidationError>, Unit> ValidateSyntax(
        ServiceConfiguration config);
    
    // Semantic validation (with I/O - system checks)
    public static TryAsync<Either<Seq<ValidationError>, Unit>> ValidateSemantics(
        ServiceConfiguration config);
    
    // Format validation errors for display
    public static string FormatValidationErrors(Seq<ValidationError> errors);
    
    // Check if validation passed with only warnings
    public static bool HasOnlyWarnings(Seq<ValidationError> errors);
}
```

### Subtask 3.7.7: Add Unit Tests for Validation (BDD)
- **File:** `tests/RedisServiceWrapper.Tests/Features/ConfigurationValidation.feature`
- **Responsibilities:**
  - Create BDD scenarios for validation
  - Test valid configurations
  - Test invalid configurations
  - Test edge cases
  - Test error message quality
- **Estimated Time:** 35 minutes

**Example Scenarios:**
```gherkin
Feature: Configuration Validation
  As a system administrator
  I want configuration validation
  So that invalid configurations are caught early

  Background:
    Given the Redis service is not running

  Scenario: Valid WSL2 Configuration
    Given I have a WSL2 backend configuration
    And the configuration has a valid Redis port
    And WSL2 is installed on the system
    When I validate the configuration
    Then the validation should succeed
    And no errors should be reported

  Scenario: Invalid Redis Port - Out of Range
    Given I have a configuration with port 99999
    When I validate the configuration
    Then the validation should fail
    And the error should mention "port"
    And the error should suggest a valid range

  Scenario: Missing Backend Configuration
    Given I have a configuration without backend settings
    When I validate the configuration
    Then the validation should fail
    And the error should mention "backend configuration required"
    And the error should suggest adding backend configuration

  Scenario: WSL2 Backend but WSL2 Not Installed
    Given I have a WSL2 backend configuration
    But WSL2 is not installed on the system
    When I validate the configuration with system checks
    Then the validation should fail
    And the error should mention "WSL2 not installed"
    And the error should suggest installing WSL2

  Scenario: Port Already in Use
    Given I have a valid configuration with port 6379
    But port 6379 is already in use
    When I validate the configuration with system checks
    Then the validation should fail
    And the error should mention "port already in use"
    And the error should suggest using a different port

  Scenario: Plain-Text Password Warning
    Given I have a configuration with a plain-text password
    When I validate the configuration
    Then the validation should succeed
    But a warning should be reported about plain-text password
    And the warning should suggest using environment variables

  Scenario: Configuration with Unresolved Secret
    Given I have a configuration with password "${ENV:REDIS_PASSWORD}"
    But the environment variable REDIS_PASSWORD is not set
    When I validate the configuration
    Then the validation should fail
    And the error should mention "environment variable not found"

  Scenario: Low Memory Warning
    Given I have a configuration requesting 16GB of memory
    But the system only has 8GB available
    When I validate the configuration with system checks
    Then the validation should fail
    And the error should mention "insufficient memory"

  Scenario: Multiple Validation Errors
    Given I have a configuration with multiple issues:
      | Issue                 |
      | Invalid port          |
      | Missing backend       |
      | Invalid memory limit  |
    When I validate the configuration
    Then the validation should fail
    And all 3 errors should be reported
    And the errors should be user-friendly
```

**Step Definitions File:**
- `tests/RedisServiceWrapper.Tests/Steps/ConfigurationValidationSteps.cs`

**Task 3.7 Total Estimated Time:** 3 hours 25 minutes

---

## Group B Summary

### Total Estimated Time: 9 hours 15 minutes

### Task Breakdown:
- **Task 3.5:** Configuration Reader & Management - 3h 50m
- **Task 3.6:** Enhance Configuration Models - 2h
- **Task 3.7:** Configuration Validation - 3h 25m

### New Files to Create (18 files):

#### Configuration Loading (7 files):
1. `src/RedisServiceWrapper/Configuration/Loading/ConfigurationManager.cs`
2. `src/RedisServiceWrapper/Configuration/Loading/ConfigurationCache.cs` ⚡ NEW
3. `src/RedisServiceWrapper/Configuration/Loading/SecretResolver.cs` 🔒 NEW
4. `src/RedisServiceWrapper/Configuration/Loading/ConfigurationWatcher.cs`
5. `src/RedisServiceWrapper/Configuration/Loading/ConfigurationChangeAnalyzer.cs` 🛡️ NEW
6. `src/RedisServiceWrapper/Configuration/Loading/ConfigurationVersioning.cs` 🔄 NEW
7. `src/RedisServiceWrapper/Configuration/Loading/DefaultConfiguration.cs`

#### Configuration Builders (1 file):
8. `src/RedisServiceWrapper/Configuration/Builders/ServiceConfigurationBuilder.cs`

#### Configuration Validation (7 files):
9. `src/RedisServiceWrapper/Configuration/Validation/ValidationInfrastructure.cs`
10. `src/RedisServiceWrapper/Configuration/Validation/BackendValidator.cs` 🔧
11. `src/RedisServiceWrapper/Configuration/Validation/RedisValidator.cs` 🔧
12. `src/RedisServiceWrapper/Configuration/Validation/ServiceSettingsValidator.cs` 🔧
13. `src/RedisServiceWrapper/Configuration/Validation/SystemValidator.cs` 🔧 NEW - CRITICAL
14. `src/RedisServiceWrapper/Configuration/Validation/ConfigurationValidator.cs` (Orchestrator)

#### Testing (3 files):
15. `tests/RedisServiceWrapper.Tests/Features/ConfigurationValidation.feature` (BDD)
16. `tests/RedisServiceWrapper.Tests/Steps/ConfigurationValidationSteps.cs` (BDD Steps)
17. `tests/RedisServiceWrapper.Tests/Fixtures/SampleConfigurations.cs` (Test Data)

#### Documentation (1 file):
18. `docs/CONFIGURATION.md` (Comprehensive configuration documentation)

### Files to Enhance (1 existing):
1. `src/RedisServiceWrapper/Configuration/Models/ServiceConfiguration.cs`
   - Add schema property
   - Add helper methods
   - Add JSON attributes
   - Enhance documentation

---

## Deliverables Checklist

### Functional Requirements:
- ✅ Load configuration from JSON file with error handling
- ✅ Thread-safe configuration caching for performance
- ✅ Secure secrets management (ENV vars, Windows Credential Manager)
- ✅ Configuration file watching and hot reload
- ✅ Safe configuration change analysis (restart detection)
- ✅ Configuration versioning and migration support
- ✅ Default configuration generation
- ✅ Comprehensive validation (syntactic + semantic)
- ✅ Business rule validation (system checks)
- ✅ User-friendly error messages with suggestions
- ✅ Validation error accumulation (not fail-fast)
- ✅ Fluent builder pattern for testing
- ✅ Sensitive data sanitization in logs

### Non-Functional Requirements:
- ✅ Performance: Configuration caching reduces file I/O
- ✅ Security: Secrets never logged in plain text
- ✅ Reliability: Hot reload safety prevents crashes
- ✅ Maintainability: Separated validator concerns
- ✅ Testability: BDD tests with comprehensive scenarios
- ✅ Usability: Clear, actionable error messages
- ✅ Extensibility: Version migrations for future changes

---

## Success Criteria

### Must Pass:
- [ ] Configuration loads from valid JSON file
- [ ] Invalid JSON returns clear error message
- [ ] Missing file uses default configuration
- [ ] All configuration properties validated
- [ ] Validation accumulates all errors (not fail-fast)
- [ ] Plain-text passwords trigger warnings
- [ ] Environment variables resolve correctly
- [ ] Windows Credential Manager integration works
- [ ] Configuration changes analyzed before applying
- [ ] Breaking changes require restart flag set
- [ ] Configuration version checked and migrated
- [ ] Cache reduces file reads on repeated access
- [ ] WSL2/Docker availability checked during validation
- [ ] Port availability checked during validation
- [ ] Disk space validated before accepting config
- [ ] All validation errors have user-friendly messages
- [ ] All validation errors have suggested fixes
- [ ] Build succeeds with no errors
- [ ] All BDD tests pass
- [ ] No sensitive data in logs

---

## Functional Programming Principles Applied

1. **Immutability:** All configurations immutable records, cache uses Atom<T>
2. **Pure Functions:** Validation, parsing, comparison all pure
3. **Error Handling:** `Try<T>`, `TryAsync<T>`, `Either<L,R>`, `Option<T>` everywhere
4. **Composition:** Validators compose using Applicative functor
5. **Side Effect Isolation:** All I/O wrapped in Try/TryAsync
6. **Railway-Oriented Programming:** Validation uses Either monad with error accumulation
7. **Type Safety:** Sealed records, no nulls (use Option<T>)

---

## Deferred Features (Future Implementation)

After working prototype is complete, implement:

1. **Environment-Specific Configurations** (Requested by user to defer)
   - `backend.Development.json`
   - `backend.Staging.json`
   - `backend.Production.json`
   - Configuration overlay merging

2. **JSON Schema Generation** (Nice-to-have)
   - Generate `backend.schema.json` from C# models
   - IDE autocomplete support

3. **Property-Based Testing** (Nice-to-have)
   - FsCheck integration
   - Random configuration generation
   - Validation invariant testing

---

## Execution Plan

### Phase 1: Loading & Security (2h 30m)
1. Task 3.5.1 - ConfigurationManager
2. Task 3.5.2 - Configuration Caching
3. Task 3.5.3 - Secrets Management
4. Task 3.5.7 - Default Configuration
5. Task 3.5.8 - Logging
6. ✅ **Build and test**

### Phase 2: Hot Reload & Versioning (1h 35m)
7. Task 3.5.4 - File Watcher
8. Task 3.5.5 - Hot Reload Safety
9. Task 3.5.6 - Versioning
10. ✅ **Build and test**

### Phase 3: Model Enhancements (2h)
11. Task 3.6.1 - Add Schema
12. Task 3.6.2 - Review Models
13. Task 3.6.3 - Helper Methods
14. Task 3.6.4 - Builder Pattern
15. Task 3.6.5 - JSON Attributes
16. ✅ **Build and test**

### Phase 4: Validation Infrastructure (1h 15m)
17. Task 3.7.1 - Validation Infrastructure
18. Task 3.7.2 - Backend Validator
19. ✅ **Build and test**

### Phase 5: Complete Validation (2h 10m)
20. Task 3.7.3 - Redis Validator
21. Task 3.7.4 - Service Settings Validator
22. Task 3.7.5 - System Validator (Critical!)
23. Task 3.7.6 - Configuration Validator (Orchestrator)
24. ✅ **Build and test**

### Phase 6: Testing (35m)
25. Task 3.7.7 - BDD Tests
26. ✅ **Run all tests**
27. ✅ **Final build verification**

---

## Testing Strategy

### Unit Testing:
- Test each validator independently
- Test secret resolution
- Test configuration merging
- Test version migration

### Integration Testing:
- Full configuration loading pipeline
- File watcher with real file changes
- Secret resolution with actual env vars
- System validation with real system state

### BDD Testing:
- User-focused scenarios
- Valid and invalid configurations
- Error message quality
- Edge cases

---

## Documentation Requirements

1. **XML Documentation:** All public methods and types
2. **Code Examples:** In XML docs for complex methods
3. **Configuration Guide:** `docs/CONFIGURATION.md`
4. **Migration Guide:** How to upgrade from v0.9 to v1.0
5. **Security Guide:** Best practices for secrets management
6. **Update PROJECT-LOG.md:** After Group B completion

---

## Risk Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| Configuration reload crashes service | High | Change analyzer prevents unsafe reloads |
| Secrets leaked in logs | High | SecretResolver sanitizes all logs |
| Invalid config crashes service | High | Validation before applying any config |
| Performance degradation | Medium | Caching reduces file I/O |
| Breaking changes in future | Medium | Versioning + migration support |
| Complex validation hard to maintain | Medium | Separated validator concerns |

---

## Ready to Begin? 🚀

**Current Status:** Planning Complete  
**Next Step:** Task 3.5.1 (ConfigurationManager Core)  
**Estimated Completion:** ~9 hours 15 minutes

**Shall we start with Phase 1 (Loading & Security)?**
