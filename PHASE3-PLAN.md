# Phase 3: C# Windows Service Wrapper - Implementation Plan

## Progress Tracker

**Last Updated:** Session 4, October 15, 2025

| Group | Tasks | Status | Completion |
|-------|-------|--------|------------|
| A - Project Setup | 4 | ✅ 3/4 Complete | 75% |
| B - Configuration | 3 | ⏳ Pending | 0% |
| C - Backend Management | 4 | ⏳ Pending | 0% |
| D - Process Control | 3 | ⏳ Pending | 0% |
| E - Health Monitoring | 3 | ⏳ Pending | 0% |
| F - Service Lifecycle | 3 | ⏳ Pending | 0% |
| G - Service Control | 3 | ⏳ Pending | 0% |
| H - Integration | 3 | ⏳ Pending | 0% |
| I - Testing & Validation | 4 | ⏳ Pending | 0% |
| **TOTAL** | **30 tasks** | **3 Complete** | **10%** |

### Completed Tasks:
- ✅ **Task 3.1:** Create C# Project Structure (Session 4)
- ✅ **Task 3.2:** Create Service Foundation Classes (Session 4)
- ✅ **Task 3.3:** Setup Logging Infrastructure (Session 4)

### Current Task:
- 🔄 **Task 3.4:** Add Project to Solution (Next)

## Overview
This phase implements a C# Windows Service that wraps and manages Redis running in WSL2 or Docker, providing seamless Windows integration with service management, health monitoring, and event logging.

## Architecture
```
Windows Service (C#)
    ├── Service Lifecycle (Start/Stop/Restart)
    ├── Backend Manager (WSL2 / Docker)
    ├── Health Monitor (Ping checks)
    ├── Configuration Manager (backend.json)
    ├── Event Logger (Windows Event Log)
    └── Process Controller (Start/Stop Redis)
```

## Task Breakdown

### Group A: Project Setup & Structure (4 tasks)

**Task 3.1: Create C# Project Structure**
- Create `RedisServiceWrapper.csproj` (.NET 8, Windows-only)
- Configure project properties (self-contained, single-file publish)
- Add NuGet package references:
  - `Microsoft.Extensions.Hosting.WindowsServices` (8.0.0)
  - `StackExchange.Redis` (2.7.0+)
  - `Newtonsoft.Json` (13.0.3+)
  - `Microsoft.Extensions.Configuration.Json` (8.0.0)
- Set output directory structure

**Task 3.2: Create Service Foundation Classes**
- `Program.cs` - Service entry point
- `RedisService.cs` - Main service class inheriting from `ServiceBase`
- `ServiceConfiguration.cs` - Configuration model classes
- `Constants.cs` - Application constants and paths

**Task 3.3: Setup Logging Infrastructure**
- Create `ILogger` interface
- Implement `EventLogLogger` - Windows Event Log integration
- Implement `FileLogger` - File-based logging
- Implement `CompositeLogger` - Combined logging
- Configure log levels and formatting

**Task 3.4: Add Project to Solution**
- Update `RedisWindowsInstaller.sln`
- Configure build dependencies
- Add project references
- Setup Debug/Release configurations

---

### Group B: Configuration Management (3 tasks) - **REVISED WITH CRITICAL IMPROVEMENTS**

**Estimated Time:** 9 hours 15 minutes (includes production-ready features)

**Task 3.5: Configuration Reader & Management (18 subtasks)**
- Core configuration loading with error handling
- **Configuration caching** (thread-safe, performance) ⚡ NEW
- **Secrets management** (ENV vars, Windows Credential Manager) 🔒 NEW - CRITICAL
- Configuration file watching and hot reload
- **Hot reload safety** (change analysis, restart detection) 🛡️ NEW - CRITICAL
- **Configuration versioning** (schema versioning, migrations) 🔄 NEW
- Default configuration generation
- Comprehensive logging with secret sanitization

**Task 3.6: Enhance Configuration Models (5 subtasks)**
- Add schema version to configuration
- Review and document all models (XML docs)
- Add helper methods to records
- **Fluent builder pattern** for testing
- JSON serialization attributes

**Task 3.7: Configuration Validation (7 subtasks) - SEPARATED CONCERNS**
- Validation infrastructure (user-friendly errors, suggestions)
- **Backend Validator** (WSL2/Docker validation) 🔧
- **Redis Validator** (port, memory, paths) 🔧
- **Service Settings Validator** (service config) 🔧
- **System Validator** (runtime checks: WSL installed, port available) 🔧 NEW - CRITICAL
- **Configuration Validator** (orchestrator with error accumulation)
- BDD Tests (SpecFlow scenarios)

**Key Improvements Incorporated:**
✅ Configuration hot reload safety (prevents crashes)
✅ Secrets management with sanitization (security)
✅ Business rule validation with system checks (reliability)
✅ User-friendly error messages with suggestions (UX)
✅ Configuration caching (performance)
✅ Configuration versioning (future-proofing)
✅ Separated validator concerns (maintainability)

**Deferred to Future:** Environment-specific configs (dev/staging/prod)

---

### Group C: Backend Management (4 tasks)

**Task 3.8: Create Backend Interface**
- Define `IRedisBackend` interface
- Common methods: `Start()`, `Stop()`, `Restart()`, `IsRunning()`, `GetStatus()`
- Event definitions for status changes

**Task 3.9: Implement WSL2 Backend Manager**
- `WslRedisBackend` class implementing `IRedisBackend`
- Start Redis in WSL2 distribution
- Stop Redis gracefully (SIGTERM/redis-cli shutdown)
- Monitor WSL2 process
- Handle WSL2-specific errors
- Mount Windows paths to WSL2

**Task 3.10: Implement Docker Backend Manager**
- `DockerRedisBackend` class implementing `IRedisBackend`
- Start/Stop Docker container
- Handle container lifecycle (create, start, stop, remove)
- Volume mapping for data persistence
- Network configuration
- Resource limits enforcement

**Task 3.11: Create Backend Factory**
- `BackendFactory` class
- Auto-detection logic (read from config)
- Backend instantiation based on type
- Fallback handling
- Error reporting

---

### Group D: Service Lifecycle Management (4 tasks)

**Task 3.12: Implement Service Start Logic**
- Override `OnStart(string[] args)`
- Load configuration
- Initialize backend manager
- Start Redis via backend
- Validate Redis is running
- Log startup events
- Handle startup failures gracefully

**Task 3.13: Implement Service Stop Logic**
- Override `OnStop()`
- Graceful Redis shutdown
- Stop health monitoring
- Release resources
- Log shutdown events
- Timeout handling (force stop after delay)

**Task 3.14: Implement Service Pause/Resume (Optional)**
- Override `OnPause()` and `OnContinue()`
- Pause health checks
- Resume operations
- State management

**Task 3.15: Add Service Recovery Options**
- Configure service recovery actions
- Restart on failure
- Recovery delay configuration
- Maximum restart attempts
- Failure logging

---

### Group E: Health Monitoring (3 tasks)

**Task 3.16: Create Health Check Infrastructure**
- `HealthMonitor` class
- Timer-based health checks
- Configurable check interval
- Redis PING command
- Connection validation

**Task 3.17: Implement Health Check Logic**
- Use `StackExchange.Redis` for PING
- Handle timeout scenarios
- Retry logic with exponential backoff
- Health status enumeration (Healthy, Unhealthy, Degraded)
- Status change notifications

**Task 3.18: Implement Auto-Recovery**
- Detect unhealthy state
- Automatic restart on failure
- Cooldown period between restarts
- Maximum restart attempts
- Alert logging for repeated failures

---

### Group F: Process & Resource Management (3 tasks)

**Task 3.19: Implement Process Controller**
- `ProcessController` class
- Execute WSL commands
- Execute Docker commands
- Capture stdout/stderr
- Process exit code handling
- Timeout management

**Task 3.20: Add Resource Monitoring**
- Memory usage tracking
- CPU usage monitoring
- Connection count monitoring
- Warning thresholds
- Resource limit enforcement

**Task 3.21: Implement Cleanup & Disposal**
- Proper `IDisposable` implementation
- Resource cleanup on service stop
- Handle abrupt termination
- Orphaned process cleanup
- Temporary file cleanup

---

### Group G: Event Logging & Diagnostics (3 tasks)

**Task 3.22: Setup Windows Event Log Source**
- Register "Redis Service" event source
- Create installation script for event source registration
- Define event IDs for different scenarios
- Event categorization (Info, Warning, Error)

**Task 3.23: Implement Structured Logging**
- Log service lifecycle events
- Log backend operations
- Log health check results
- Log configuration changes
- Log errors with stack traces
- Performance metrics logging

**Task 3.24: Create Diagnostic Commands**
- Service status command
- Health check command
- Configuration dump command
- Version information
- Backend status reporting

---

### Group H: Installation & Service Registration (3 tasks)

**Task 3.25: Create Service Installer**
- `ServiceInstaller` configuration
- Service name, display name, description
- Start type (Automatic, Manual, Disabled)
- Service dependencies (LxssManager for WSL2, Docker for Docker)
- Account configuration (LocalSystem)

**Task 3.26: Implement Command-Line Interface**
- `--install` - Install service
- `--uninstall` - Uninstall service
- `--start` - Start service
- `--stop` - Stop service
- `--status` - Get service status
- `--help` - Show help

**Task 3.27: Create Installation Scripts**
- `install-service.ps1` - PowerShell installation script
- `uninstall-service.ps1` - Uninstall script
- Pre-installation validation
- Post-installation verification
- Error handling and rollback

---

### Group I: Testing & Validation (4 tasks)

**Task 3.28: Create Unit Tests with BDD (SpecFlow)**
- Test project: `RedisServiceWrapper.Tests`
- Setup SpecFlow with xUnit/NUnit
- Create `.feature` files using Gherkin syntax
- Configuration parsing tests (BDD scenarios)
- Backend manager tests (mocked scenarios)
- Health monitor tests (behavior scenarios)
- Process controller tests
- Step definitions for all scenarios
- Benefits: Readable tests, living documentation

**Task 3.29: Create Integration Tests**
- End-to-end service tests
- WSL2 backend integration tests
- Docker backend integration tests
- Service start/stop tests
- Configuration reload tests

**Task 3.30: Manual Testing Procedures**
- Create test script for manual validation
- Service installation test
- Redis connectivity test
- Health monitoring test
- Restart behavior test
- Error scenario tests

**Task 3.31: Performance Testing**
- Service startup time
- Memory footprint
- Health check overhead
- Restart recovery time
- Resource usage under load

---

## Deliverables

### Code Files
- `src/RedisServiceWrapper/Program.cs`
- `src/RedisServiceWrapper/RedisService.cs`
- `src/RedisServiceWrapper/Configuration/ConfigurationManager.cs`
- `src/RedisServiceWrapper/Configuration/ServiceConfiguration.cs`
- `src/RedisServiceWrapper/Backend/IRedisBackend.cs`
- `src/RedisServiceWrapper/Backend/WslRedisBackend.cs`
- `src/RedisServiceWrapper/Backend/DockerRedisBackend.cs`
- `src/RedisServiceWrapper/Backend/BackendFactory.cs`
- `src/RedisServiceWrapper/Monitoring/HealthMonitor.cs`
- `src/RedisServiceWrapper/Logging/ILogger.cs`
- `src/RedisServiceWrapper/Logging/EventLogLogger.cs`
- `src/RedisServiceWrapper/Logging/FileLogger.cs`
- `src/RedisServiceWrapper/Utils/ProcessController.cs`
- `src/RedisServiceWrapper/RedisServiceWrapper.csproj`

### Scripts
- `src/RedisServiceWrapper/Scripts/install-service.ps1`
- `src/RedisServiceWrapper/Scripts/uninstall-service.ps1`
- `src/RedisServiceWrapper/Scripts/register-eventlog.ps1`

### Tests
- `tests/RedisServiceWrapper.Tests/` (unit tests)
- `tests/Integration/` (integration tests)
- `tests/Test-Phase3.ps1` (validation script)

### Documentation
- `src/RedisServiceWrapper/README.md`
- Updated `docs/BUILD.md` with service build instructions
- Updated `docs/INSTALL.md` with service installation steps

---

## Progress Tracking

- [ ] Group A: Project Setup & Structure (Tasks 3.1-3.4)
- [ ] Group B: Configuration Management (Tasks 3.5-3.7)
- [ ] Group C: Backend Management (Tasks 3.8-3.11)
- [ ] Group D: Service Lifecycle Management (Tasks 3.12-3.15)
- [ ] Group E: Health Monitoring (Tasks 3.16-3.18)
- [ ] Group F: Process & Resource Management (Tasks 3.19-3.21)
- [ ] Group G: Event Logging & Diagnostics (Tasks 3.22-3.24)
- [ ] Group H: Installation & Service Registration (Tasks 3.25-3.27)
- [ ] Group I: Testing & Validation (Tasks 3.28-3.31)

**Total Tasks:** 31 tasks across 9 groups

---

## Dependencies

### External
- .NET 8 SDK
- Visual Studio 2022 or VS Code with C# extension
- WSL2 or Docker Desktop (for testing)

### NuGet Packages

**Production:**
- `Microsoft.Extensions.Hosting.WindowsServices` (8.0.0+)
- `StackExchange.Redis` (2.7.0+)
- `Newtonsoft.Json` (13.0.3+)
- `Microsoft.Extensions.Configuration.Json` (8.0.0+)
- `System.ServiceProcess.ServiceController` (8.0.0+)

**Testing (BDD with SpecFlow):**
- `SpecFlow` (3.9+) - BDD framework (C# equivalent of Cucumber)
- `SpecFlow.xUnit` or `SpecFlow.NUnit` (3.9+)
- `xUnit` (2.5+) or `NUnit` (3.13+)
- `FluentAssertions` (6.12+) - Better assertion syntax
- `Moq` (4.20+) - Mocking framework

### Previous Phases
- Phase 1: Project structure and documentation
- Phase 2: PowerShell detection and installation module

---

## Estimated Effort

| Group | Tasks | Complexity | Est. Time |
|-------|-------|------------|-----------|
| A - Project Setup | 4 | Low | 30-45 min |
| B - Configuration | 3 | Medium | 45-60 min |
| C - Backend Management | 4 | High | 90-120 min |
| D - Service Lifecycle | 4 | Medium | 60-90 min |
| E - Health Monitoring | 3 | Medium | 45-60 min |
| F - Process Management | 3 | Medium | 45-60 min |
| G - Event Logging | 3 | Low | 30-45 min |
| H - Installation | 3 | Medium | 45-60 min |
| I - Testing | 4 | Medium | 60-90 min |
| **Total** | **31** | - | **7-10 hours** |

---

## Success Criteria

### Functional Requirements
- ✅ Service installs and uninstalls cleanly
- ✅ Service starts and stops Redis successfully
- ✅ Health monitoring detects and recovers from failures
- ✅ Works with both WSL2 and Docker backends
- ✅ Proper Windows Event Log integration
- ✅ Configuration changes apply correctly

### Non-Functional Requirements
- ✅ Service starts within 30 seconds
- ✅ Memory footprint < 50MB
- ✅ Health check overhead < 1% CPU
- ✅ Graceful shutdown within 10 seconds
- ✅ Automatic recovery from failures
- ✅ Comprehensive error logging

### Quality Gates
- ✅ All unit tests pass
- ✅ Integration tests pass on both backends
- ✅ No memory leaks detected
- ✅ Service runs stable for 24+ hours
- ✅ Handles Redis crashes gracefully
- ✅ Code follows C# best practices

---

## Risk Mitigation

### Potential Risks
1. **WSL2 Process Management** - WSL processes might be hard to monitor
   - *Mitigation:* Use redis-cli for health checks instead of process monitoring

2. **Docker Container Lifecycle** - Container restart complexity
   - *Mitigation:* Use Docker SDK or CLI with proper error handling

3. **Service Dependencies** - WSL/Docker services might not be available
   - *Mitigation:* Check dependencies on service start, fail gracefully with clear error

4. **Configuration Changes** - Hot reload might be complex
   - *Mitigation:* Require service restart for configuration changes initially

5. **Permission Issues** - Service account might not have Docker/WSL access
   - *Mitigation:* Run as LocalSystem, document permission requirements

---

## Notes

- Start with WSL2 backend implementation (simpler than Docker)
- Keep Docker backend interface identical for easier testing
- Focus on reliability over features
- Log extensively for debugging
- Consider future Phase 3.5 for GUI management tool

### BDD Testing with SpecFlow

**Why SpecFlow?**
- C# equivalent of Cucumber (uses Gherkin syntax)
- Readable .feature files for non-technical stakeholders
- Living documentation - tests describe behavior
- Better collaboration between developers and business
- Easy onboarding for new team members

**Example SpecFlow Test:**
```gherkin
Feature: Redis Service Management
    As a system administrator
    I want to manage Redis as a Windows Service
    So that Redis starts automatically and can be easily controlled

Scenario: Start Redis Service with WSL2 Backend
    Given WSL2 is installed with Ubuntu distribution
    And Redis is installed in WSL2
    And the service configuration specifies WSL2 backend
    When I start the Redis service
    Then the service status should be "Running"
    And Redis should respond to PING command
    And the service should log "Redis started successfully"

Scenario: Handle Redis Crash with Auto-Recovery
    Given the Redis service is running
    And auto-recovery is enabled
    When Redis process crashes
    Then the service should detect the failure within 30 seconds
    And the service should automatically restart Redis
    And Redis should be accessible again
```

**Test Organization:**
```
tests/RedisServiceWrapper.Tests/
├── Features/
│   ├── ServiceLifecycle.feature
│   ├── BackendManagement.feature
│   ├── HealthMonitoring.feature
│   └── Configuration.feature
├── StepDefinitions/
│   ├── ServiceLifecycleSteps.cs
│   ├── BackendManagementSteps.cs
│   ├── HealthMonitoringSteps.cs
│   └── ConfigurationSteps.cs
└── Support/
    ├── TestConfiguration.cs
    └── TestHelpers.cs
```

---

## Phase 3 Status: NOT STARTED

Ready to begin implementation!

