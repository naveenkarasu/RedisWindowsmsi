# Redis Windows Installer - Test Suite

This directory contains test scripts for validating the Redis Windows Installer components.

## Test Scripts

### `Test-Phase2.ps1`
Comprehensive test script for Phase 2 (Detection & Installation Module).

**What it tests:**
- PowerShell module loading
- Module manifest validation
- Helper functions (Get-WindowsVersion, Test-IsAdministrator, etc.)
- Detection functions (WSL2, Docker, Backend selection)
- Configuration templates (redis.conf, backend.json)
- Installation function availability

**Usage:**

```powershell
# Basic test (detection only)
.\tests\Test-Phase2.ps1

# With verbose output
.\tests\Test-Phase2.ps1 -Verbose

# Generate diagnostic report
.\tests\Test-Phase2.ps1 -GenerateDiagnostics

# Test installation functions (requires admin)
.\tests\Test-Phase2.ps1 -TestInstallation
```

**Test Categories:**

1. **Module Loading Tests** (5 tests)
   - Module file exists
   - Module manifest exists
   - Module loads successfully
   - Module manifest is valid
   - All functions exported

2. **Helper Function Tests** (4 tests)
   - Get-WindowsVersion returns data
   - Test-IsAdministrator executes
   - Test-CommandExists works correctly
   - Write-LogMessage creates log file

3. **Detection Function Tests** (3 tests)
   - Test-WSL2Installed executes
   - Test-DockerInstalled executes
   - Get-RedisBackend returns valid backend

4. **Configuration Template Tests** (5 tests)
   - redis.conf template exists
   - redis.conf has required settings
   - backend.json template exists
   - backend.json is valid JSON with required sections
   - Configuration README exists

5. **Installation Function Tests** (3 tests, optional)
   - Install-WSL2 function available
   - Install-DockerDesktop function available
   - Install-RedisInWSL function available

**Output:**

The script provides:
- Color-coded test results (Green=Pass, Red=Fail, Yellow=Skip)
- Test summary with success rate
- Optional diagnostic reports (JSON + TXT)
- Exit code (0=success, 1=failure)

**Example Output:**

```
========================================
  Module Loading Tests
========================================
  [PASS] Module file exists
         D:\...\src\PowerShellModules\RedisInstaller.psm1
  [PASS] Module loads successfully
  [PASS] All functions exported
         Expected: 11, Found: 11

========================================
  Detection Function Tests
========================================
  [PASS] Test-WSL2Installed executes
         WSL2 Ready: True
  [PASS] Get-RedisBackend returns valid backend
         Detected Backend: WSL2

========================================
  Test Summary
========================================
  Total Tests:   17
  Passed:        17
  Failed:        0
  Skipped:       0
  Success Rate:  100%

  All tests passed! Phase 2 module is working correctly.
```

## Diagnostic Reports

When run with `-GenerateDiagnostics`, the script generates two files:

### 1. `diagnostic-report-[timestamp].json`
Full diagnostic data in JSON format:
- System information (Windows version, PowerShell version, etc.)
- WSL2 status and details
- Docker status and details
- Detected backend
- Module information
- Configuration template status
- All test results

### 2. `diagnostic-summary-[timestamp].txt`
Human-readable summary including:
- System information
- WSL2/Docker status
- Recommended backend
- Test results summary
- Failed tests details
- Configuration file status
- Recommendations for next steps

**Example diagnostic summary:**
```
Redis Windows Installer - Diagnostic Summary
Generated: 2025-10-14 15:30:45
============================================

SYSTEM INFORMATION
------------------
Windows Version: Microsoft Windows 11 Pro
Build Number: 22631
WSL2 Compatible: True
PowerShell: 7.4.0
Administrator: True

WSL2 STATUS
-----------
Compatible: True
Enabled: True
Has Distribution: True
Default Distribution: Ubuntu

RECOMMENDED BACKEND
-------------------
Backend: WSL2

TEST RESULTS
------------
Total Tests: 17
Passed: 17
Failed: 0
Success Rate: 100%

RECOMMENDATIONS
---------------
- System is ready for Redis installation using WSL2 backend
```

## Running Tests in CI/CD

### GitHub Actions Example

```yaml
- name: Run Phase 2 Tests
  shell: pwsh
  run: |
    .\tests\Test-Phase2.ps1 -GenerateDiagnostics
    
- name: Upload Diagnostic Reports
  if: always()
  uses: actions/upload-artifact@v3
  with:
    name: diagnostic-reports
    path: tests/diagnostic-*.{json,txt}
```

### Azure DevOps Example

```yaml
- task: PowerShell@2
  displayName: 'Run Phase 2 Tests'
  inputs:
    filePath: 'tests/Test-Phase2.ps1'
    arguments: '-GenerateDiagnostics'
    pwsh: true
    
- task: PublishBuildArtifacts@1
  condition: always()
  inputs:
    PathtoPublish: 'tests'
    ArtifactName: 'diagnostic-reports'
```

## Test Development

### Adding New Tests

To add new tests to `Test-Phase2.ps1`:

1. Add test code in appropriate section:
```powershell
# Test [number]: [Description]
try {
    $result = YourTestFunction
    $passed = $result -eq $expected
    Write-TestResult -TestName "Your test name" -Passed $passed -Message "Details"
} catch {
    Write-TestResult -TestName "Your test name" -Passed $false -Message $_.Exception.Message
}
```

2. Update test count in comments
3. Run tests to verify

### Helper Functions

Available helper functions in test script:
- `Write-TestHeader`: Display section header
- `Write-TestResult`: Record and display test result
- `Write-TestSkipped`: Mark test as skipped with reason

## Troubleshooting

### Common Issues

**Issue: Module not found**
```
Solution: Ensure you're running from project root or tests directory
```

**Issue: Tests fail with "Access Denied"**
```
Solution: Run PowerShell as Administrator for installation tests
```

**Issue: WSL2 tests fail**
```
Solution: Check if WSL2 is installed and enabled on your system
```

**Issue: Docker tests fail**
```
Solution: Ensure Docker Desktop is installed and running
```

## Best Practices

1. **Run tests before committing**: Always run tests before pushing changes
2. **Generate diagnostics for bug reports**: Include diagnostic reports when reporting issues
3. **Review failed tests**: Investigate failed tests before proceeding
4. **Keep tests updated**: Update tests when adding new features

## Future Tests

Planned test additions:
- [ ] Integration tests with actual Redis installation
- [ ] Performance benchmarks
- [ ] Error handling validation
- [ ] Configuration file parsing tests
- [ ] Network connectivity tests
- [ ] Security validation tests

## Contributing

When adding new functionality:
1. Write tests first (TDD approach)
2. Ensure all tests pass
3. Update test documentation
4. Generate diagnostic report for verification

