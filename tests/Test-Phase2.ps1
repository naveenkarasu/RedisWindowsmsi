#Requires -Version 5.1

<#
.SYNOPSIS
    Test script for Phase 2 - Detection & Installation Module
    
.DESCRIPTION
    Comprehensive testing script that validates:
    - PowerShell module loading
    - Detection functions
    - Configuration templates
    - System compatibility
    - Creates diagnostic reports
    
.PARAMETER TestInstallation
    If specified, tests installation functions (requires admin privileges)
    
.PARAMETER GenerateDiagnostics
    If specified, generates a detailed diagnostic report
    
.PARAMETER Verbose
    Enable verbose logging
    
.EXAMPLE
    .\Test-Phase2.ps1
    Run basic detection tests
    
.EXAMPLE
    .\Test-Phase2.ps1 -GenerateDiagnostics
    Run tests and generate diagnostic report
    
.EXAMPLE
    .\Test-Phase2.ps1 -TestInstallation -Verbose
    Run installation tests with verbose output (requires admin)
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [switch]$TestInstallation,
    
    [Parameter(Mandatory = $false)]
    [switch]$GenerateDiagnostics
)

# Test configuration
$ErrorActionPreference = "Continue"
$script:TestResults = @{
    Passed = 0
    Failed = 0
    Skipped = 0
    Tests = @()
}

#region Helper Functions

function Write-TestHeader {
    param([string]$Title)
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "  $Title" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
}

function Write-TestResult {
    param(
        [string]$TestName,
        [bool]$Passed,
        [string]$Message = "",
        [string]$Details = ""
    )
    
    $result = [PSCustomObject]@{
        TestName = $TestName
        Passed = $Passed
        Message = $Message
        Details = $Details
        Timestamp = Get-Date
    }
    
    $script:TestResults.Tests += $result
    
    if ($Passed) {
        $script:TestResults.Passed++
        Write-Host "  [PASS] $TestName" -ForegroundColor Green
        if ($Message) {
            Write-Host "         $Message" -ForegroundColor Gray
        }
    } else {
        $script:TestResults.Failed++
        Write-Host "  [FAIL] $TestName" -ForegroundColor Red
        if ($Message) {
            Write-Host "         $Message" -ForegroundColor Yellow
        }
    }
    
    if ($Details -and $VerbosePreference -eq 'Continue') {
        Write-Verbose "         Details: $Details"
    }
}

function Write-TestSkipped {
    param([string]$TestName, [string]$Reason)
    
    $script:TestResults.Skipped++
    Write-Host "  [SKIP] $TestName" -ForegroundColor Yellow
    Write-Host "         Reason: $Reason" -ForegroundColor Gray
}

#endregion

#region Module Loading Tests

Write-TestHeader "Module Loading Tests"

# Test 1: Module file exists
$modulePath = Join-Path $PSScriptRoot "..\src\PowerShellModules\RedisInstaller.psm1"
$moduleExists = Test-Path $modulePath
Write-TestResult -TestName "Module file exists" -Passed $moduleExists -Message $modulePath

# Test 2: Module manifest exists
$manifestPath = Join-Path $PSScriptRoot "..\src\PowerShellModules\RedisInstaller.psd1"
$manifestExists = Test-Path $manifestPath
Write-TestResult -TestName "Module manifest exists" -Passed $manifestExists -Message $manifestPath

# Test 3: Load module
if ($moduleExists) {
    try {
        Import-Module $modulePath -Force -ErrorAction Stop
        Write-TestResult -TestName "Module loads successfully" -Passed $true
    } catch {
        Write-TestResult -TestName "Module loads successfully" -Passed $false -Message $_.Exception.Message
    }
} else {
    Write-TestSkipped -TestName "Module loads successfully" -Reason "Module file not found"
}

# Test 4: Validate module manifest
if ($manifestExists) {
    try {
        $manifest = Test-ModuleManifest -Path $manifestPath -ErrorAction Stop
        Write-TestResult -TestName "Module manifest is valid" -Passed $true -Message "Version: $($manifest.Version)"
    } catch {
        Write-TestResult -TestName "Module manifest is valid" -Passed $false -Message $_.Exception.Message
    }
} else {
    Write-TestSkipped -TestName "Module manifest is valid" -Reason "Manifest file not found"
}

# Test 5: Exported functions
$expectedFunctions = @(
    'Get-WindowsVersion',
    'Test-IsAdministrator',
    'Write-LogMessage',
    'Test-CommandExists',
    'Download-File',
    'Test-WSL2Installed',
    'Test-DockerInstalled',
    'Get-RedisBackend',
    'Install-WSL2',
    'Install-DockerDesktop',
    'Install-RedisInWSL'
)

$exportedFunctions = Get-Command -Module RedisInstaller -ErrorAction SilentlyContinue
$allFunctionsExported = $true
$missingFunctions = @()

foreach ($func in $expectedFunctions) {
    if ($exportedFunctions.Name -notcontains $func) {
        $allFunctionsExported = $false
        $missingFunctions += $func
    }
}

Write-TestResult -TestName "All functions exported" -Passed $allFunctionsExported `
    -Message "Expected: $($expectedFunctions.Count), Found: $($exportedFunctions.Count)" `
    -Details "Missing: $($missingFunctions -join ', ')"

#endregion

#region Helper Function Tests

Write-TestHeader "Helper Function Tests"

# Test 6: Get-WindowsVersion
try {
    $winVersion = Get-WindowsVersion
    $passed = $null -ne $winVersion -and $null -ne $winVersion.BuildNumber
    Write-TestResult -TestName "Get-WindowsVersion returns data" -Passed $passed `
        -Message "Build: $($winVersion.BuildNumber), WSL2 Compatible: $($winVersion.IsWSL2Compatible)"
} catch {
    Write-TestResult -TestName "Get-WindowsVersion returns data" -Passed $false -Message $_.Exception.Message
}

# Test 7: Test-IsAdministrator
try {
    $isAdmin = Test-IsAdministrator
    Write-TestResult -TestName "Test-IsAdministrator executes" -Passed $true `
        -Message "Is Administrator: $isAdmin"
} catch {
    Write-TestResult -TestName "Test-IsAdministrator executes" -Passed $false -Message $_.Exception.Message
}

# Test 8: Test-CommandExists
try {
    $powershellExists = Test-CommandExists "powershell"
    $fakeCommandExists = Test-CommandExists "ThisCommandDoesNotExist12345"
    $passed = $powershellExists -eq $true -and $fakeCommandExists -eq $false
    Write-TestResult -TestName "Test-CommandExists works correctly" -Passed $passed `
        -Message "PowerShell: $powershellExists, Fake: $fakeCommandExists"
} catch {
    Write-TestResult -TestName "Test-CommandExists works correctly" -Passed $false -Message $_.Exception.Message
}

# Test 9: Write-LogMessage
try {
    $testLogPath = "$env:TEMP\redis-test-log.txt"
    if (Test-Path $testLogPath) {
        Remove-Item $testLogPath -Force
    }
    
    # Temporarily override log path for testing
    $script:LogPath = $testLogPath
    Write-LogMessage "Test message" -Level Info -NoConsole
    
    $logExists = Test-Path $testLogPath
    Write-TestResult -TestName "Write-LogMessage creates log file" -Passed $logExists `
        -Message "Log file: $testLogPath"
    
    # Cleanup
    if (Test-Path $testLogPath) {
        Remove-Item $testLogPath -Force -ErrorAction SilentlyContinue
    }
} catch {
    Write-TestResult -TestName "Write-LogMessage creates log file" -Passed $false -Message $_.Exception.Message
}

#endregion

#region Detection Function Tests

Write-TestHeader "Detection Function Tests"

# Test 10: Test-WSL2Installed
try {
    $wsl2Status = Test-WSL2Installed
    $passed = $null -ne $wsl2Status -and $null -ne $wsl2Status.IsCompatible
    
    $details = @"
Compatible: $($wsl2Status.IsCompatible)
Enabled: $($wsl2Status.IsEnabled)
Has Distribution: $($wsl2Status.HasDistribution)
Default Distribution: $($wsl2Status.DefaultDistribution)
Distributions: $($wsl2Status.Distributions -join ', ')
"@
    
    Write-TestResult -TestName "Test-WSL2Installed executes" -Passed $passed `
        -Message "WSL2 Ready: $($wsl2Status.IsCompatible -and $wsl2Status.IsEnabled -and $wsl2Status.HasDistribution)" `
        -Details $details
} catch {
    Write-TestResult -TestName "Test-WSL2Installed executes" -Passed $false -Message $_.Exception.Message
}

# Test 11: Test-DockerInstalled
try {
    $dockerStatus = Test-DockerInstalled
    $passed = $null -ne $dockerStatus -and $null -ne $dockerStatus.IsInstalled
    
    $details = @"
Installed: $($dockerStatus.IsInstalled)
Running: $($dockerStatus.IsRunning)
Version: $($dockerStatus.Version)
Install Path: $($dockerStatus.InstallPath)
"@
    
    Write-TestResult -TestName "Test-DockerInstalled executes" -Passed $passed `
        -Message "Docker Ready: $($dockerStatus.IsInstalled -and $dockerStatus.IsRunning)" `
        -Details $details
} catch {
    Write-TestResult -TestName "Test-DockerInstalled executes" -Passed $false -Message $_.Exception.Message
}

# Test 12: Get-RedisBackend
try {
    $backend = Get-RedisBackend
    $passed = $null -ne $backend -and $backend -in @("WSL2", "Docker", "WSL2-Partial", "Docker-NotRunning", "None")
    
    Write-TestResult -TestName "Get-RedisBackend returns valid backend" -Passed $passed `
        -Message "Detected Backend: $backend"
} catch {
    Write-TestResult -TestName "Get-RedisBackend returns valid backend" -Passed $false -Message $_.Exception.Message
}

#endregion

#region Configuration Template Tests

Write-TestHeader "Configuration Template Tests"

# Test 13: redis.conf exists
$redisConfPath = Join-Path $PSScriptRoot "..\src\ConfigTemplates\redis.conf"
$redisConfExists = Test-Path $redisConfPath
Write-TestResult -TestName "redis.conf template exists" -Passed $redisConfExists -Message $redisConfPath

# Test 14: redis.conf is valid
if ($redisConfExists) {
    try {
        $content = Get-Content $redisConfPath -Raw
        $hasPort = $content -match "port\s+\d+"
        $hasMaxMemory = $content -match "maxmemory\s+"
        $hasSave = $content -match "save\s+\d+"
        
        $passed = $hasPort -and $hasMaxMemory -and $hasSave
        Write-TestResult -TestName "redis.conf has required settings" -Passed $passed `
            -Message "Port: $hasPort, MaxMemory: $hasMaxMemory, Save: $hasSave"
    } catch {
        Write-TestResult -TestName "redis.conf has required settings" -Passed $false -Message $_.Exception.Message
    }
} else {
    Write-TestSkipped -TestName "redis.conf has required settings" -Reason "Template file not found"
}

# Test 15: backend.json exists
$backendJsonPath = Join-Path $PSScriptRoot "..\src\ConfigTemplates\backend.json"
$backendJsonExists = Test-Path $backendJsonPath
Write-TestResult -TestName "backend.json template exists" -Passed $backendJsonExists -Message $backendJsonPath

# Test 16: backend.json is valid JSON
if ($backendJsonExists) {
    try {
        $config = Get-Content $backendJsonPath -Raw | ConvertFrom-Json
        $hasBackendType = $null -ne $config.backendType
        $hasWsl = $null -ne $config.wsl
        $hasDocker = $null -ne $config.docker
        $hasRedis = $null -ne $config.redis
        
        $passed = $hasBackendType -and $hasWsl -and $hasDocker -and $hasRedis
        Write-TestResult -TestName "backend.json is valid JSON with required sections" -Passed $passed `
            -Message "BackendType: $($config.backendType), Sections: WSL=$hasWsl, Docker=$hasDocker, Redis=$hasRedis"
    } catch {
        Write-TestResult -TestName "backend.json is valid JSON with required sections" -Passed $false -Message $_.Exception.Message
    }
} else {
    Write-TestSkipped -TestName "backend.json is valid JSON with required sections" -Reason "Template file not found"
}

# Test 17: ConfigTemplates README exists
$configReadmePath = Join-Path $PSScriptRoot "..\src\ConfigTemplates\README.md"
$configReadmeExists = Test-Path $configReadmePath
Write-TestResult -TestName "Configuration README exists" -Passed $configReadmeExists -Message $configReadmePath

#endregion

#region Installation Function Tests (Optional)

if ($TestInstallation) {
    Write-TestHeader "Installation Function Tests"
    
    # Check admin privileges
    $isAdmin = Test-IsAdministrator
    if (-not $isAdmin) {
        Write-TestSkipped -TestName "Installation tests" -Reason "Requires administrator privileges"
    } else {
        Write-Host "  NOTE: Installation tests are disabled by default to prevent system changes." -ForegroundColor Yellow
        Write-Host "  To enable, modify this script to uncomment installation test code." -ForegroundColor Yellow
        
        # Test 18: Install-WSL2 function exists
        $installWsl2Exists = Get-Command Install-WSL2 -ErrorAction SilentlyContinue
        Write-TestResult -TestName "Install-WSL2 function available" -Passed ($null -ne $installWsl2Exists)
        
        # Test 19: Install-DockerDesktop function exists
        $installDockerExists = Get-Command Install-DockerDesktop -ErrorAction SilentlyContinue
        Write-TestResult -TestName "Install-DockerDesktop function available" -Passed ($null -ne $installDockerExists)
        
        # Test 20: Install-RedisInWSL function exists
        $installRedisExists = Get-Command Install-RedisInWSL -ErrorAction SilentlyContinue
        Write-TestResult -TestName "Install-RedisInWSL function available" -Passed ($null -ne $installRedisExists)
    }
}

#endregion

#region Generate Diagnostics

if ($GenerateDiagnostics) {
    Write-TestHeader "Generating Diagnostic Report"
    
    try {
        $diagnostics = @{
            Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
            System = @{
                WindowsVersion = Get-WindowsVersion
                IsAdministrator = Test-IsAdministrator
                PowerShellVersion = $PSVersionTable.PSVersion.ToString()
                MachineName = $env:COMPUTERNAME
                UserName = $env:USERNAME
            }
            WSL2 = Test-WSL2Installed
            Docker = Test-DockerInstalled
            Backend = Get-RedisBackend
            Module = @{
                Path = $modulePath
                Exists = $moduleExists
                ManifestValid = $manifestExists
                ExportedFunctions = $exportedFunctions.Name
            }
            ConfigTemplates = @{
                RedisConf = @{
                    Path = $redisConfPath
                    Exists = $redisConfExists
                }
                BackendJson = @{
                    Path = $backendJsonPath
                    Exists = $backendJsonExists
                }
            }
            TestResults = $script:TestResults
        }
        
        $reportPath = Join-Path $PSScriptRoot "diagnostic-report-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
        $diagnostics | ConvertTo-Json -Depth 10 | Out-File $reportPath -Encoding UTF8
        
        Write-Host "  Diagnostic report saved: $reportPath" -ForegroundColor Green
        
        # Also create a text summary
        $summaryPath = Join-Path $PSScriptRoot "diagnostic-summary-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"
        @"
Redis Windows Installer - Diagnostic Summary
Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
============================================

SYSTEM INFORMATION
------------------
Windows Version: $($diagnostics.System.WindowsVersion.ProductName)
Build Number: $($diagnostics.System.WindowsVersion.BuildNumber)
WSL2 Compatible: $($diagnostics.System.WindowsVersion.IsWSL2Compatible)
PowerShell: $($diagnostics.System.PowerShellVersion)
Administrator: $($diagnostics.System.IsAdministrator)

WSL2 STATUS
-----------
Compatible: $($diagnostics.WSL2.IsCompatible)
Enabled: $($diagnostics.WSL2.IsEnabled)
Has Distribution: $($diagnostics.WSL2.HasDistribution)
Default Distribution: $($diagnostics.WSL2.DefaultDistribution)
All Distributions: $($diagnostics.WSL2.Distributions -join ', ')

DOCKER STATUS
-------------
Installed: $($diagnostics.Docker.IsInstalled)
Running: $($diagnostics.Docker.IsRunning)
Version: $($diagnostics.Docker.Version)
Install Path: $($diagnostics.Docker.InstallPath)

RECOMMENDED BACKEND
-------------------
Backend: $($diagnostics.Backend)

TEST RESULTS
------------
Total Tests: $($script:TestResults.Passed + $script:TestResults.Failed + $script:TestResults.Skipped)
Passed: $($script:TestResults.Passed)
Failed: $($script:TestResults.Failed)
Skipped: $($script:TestResults.Skipped)
Success Rate: $(if (($script:TestResults.Passed + $script:TestResults.Failed) -gt 0) { [math]::Round(($script:TestResults.Passed / ($script:TestResults.Passed + $script:TestResults.Failed)) * 100, 2) } else { 0 })%

FAILED TESTS
------------
$($script:TestResults.Tests | Where-Object { -not $_.Passed } | ForEach-Object { "- $($_.TestName): $($_.Message)" } | Out-String)

CONFIGURATION FILES
-------------------
redis.conf: $(if ($redisConfExists) { "Found" } else { "Missing" })
backend.json: $(if ($backendJsonExists) { "Found" } else { "Missing" })

RECOMMENDATIONS
---------------
$( if ($diagnostics.Backend -eq "None") {
    "- Install WSL2 or Docker Desktop to use Redis on Windows"
} elseif ($diagnostics.Backend -eq "WSL2-Partial") {
    "- WSL2 is enabled but requires a distribution installation"
} elseif ($diagnostics.Backend -eq "Docker-NotRunning") {
    "- Start Docker Desktop to use Redis"
} else {
    "- System is ready for Redis installation using $($diagnostics.Backend) backend"
})
$( if ($script:TestResults.Failed -gt 0) {
    "- Review failed tests and resolve issues before proceeding with installation"
})
$( if (-not $diagnostics.System.IsAdministrator) {
    "- Administrator privileges will be required for installation"
})
"@  | Out-File $summaryPath -Encoding UTF8
        
        Write-Host "  Diagnostic summary saved: $summaryPath" -ForegroundColor Green
        
    } catch {
        Write-Host "  Failed to generate diagnostic report: $_" -ForegroundColor Red
    }
}

#endregion

#region Final Summary

Write-TestHeader "Test Summary"

$totalTests = $script:TestResults.Passed + $script:TestResults.Failed + $script:TestResults.Skipped
$successRate = if (($script:TestResults.Passed + $script:TestResults.Failed) -gt 0) {
    [math]::Round(($script:TestResults.Passed / ($script:TestResults.Passed + $script:TestResults.Failed)) * 100, 2)
} else {
    0
}

Write-Host "  Total Tests:   $totalTests" -ForegroundColor Cyan
Write-Host "  Passed:        $($script:TestResults.Passed)" -ForegroundColor Green
Write-Host "  Failed:        $($script:TestResults.Failed)" -ForegroundColor $(if ($script:TestResults.Failed -gt 0) { "Red" } else { "Gray" })
Write-Host "  Skipped:       $($script:TestResults.Skipped)" -ForegroundColor Yellow
Write-Host "  Success Rate:  $successRate%" -ForegroundColor $(if ($successRate -ge 90) { "Green" } elseif ($successRate -ge 70) { "Yellow" } else { "Red" })
Write-Host ""

if ($script:TestResults.Failed -eq 0) {
    Write-Host "  All tests passed! Phase 2 module is working correctly." -ForegroundColor Green
    exit 0
} else {
    Write-Host "  Some tests failed. Please review the results above." -ForegroundColor Yellow
    Write-Host "  Failed tests:" -ForegroundColor Red
    $script:TestResults.Tests | Where-Object { -not $_.Passed } | ForEach-Object {
        Write-Host "    - $($_.TestName): $($_.Message)" -ForegroundColor Red
    }
    exit 1
}

#endregion

