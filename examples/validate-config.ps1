# Redis Service Wrapper Configuration Validator
# This script demonstrates how to validate configuration files using the built-in validator

param(
    [Parameter(Mandatory=$true)]
    [string]$ConfigPath,
    
    [switch]$Verbose
)

# Function to display colored output
function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Color
}

# Function to validate configuration file
function Test-ConfigurationFile {
    param(
        [string]$Path
    )
    
    Write-ColorOutput "Validating configuration file: $Path" "Cyan"
    
    if (-not (Test-Path $Path)) {
        Write-ColorOutput "ERROR: Configuration file not found: $Path" "Red"
        return $false
    }
    
    try {
        # Read and parse JSON
        $jsonContent = Get-Content $Path -Raw
        $config = $jsonContent | ConvertFrom-Json
        
        Write-ColorOutput "✓ JSON syntax is valid" "Green"
        
        # Basic validation checks
        $errors = @()
        $warnings = @()
        
        # Check schema version
        if (-not $config.SchemaVersion) {
            $errors += "Missing SchemaVersion"
        } elseif ($config.SchemaVersion -ne "1.0.0") {
            $warnings += "Unsupported schema version: $($config.SchemaVersion)"
        }
        
        # Check backend type
        if (-not $config.BackendType) {
            $errors += "Missing BackendType"
        } elseif ($config.BackendType -notin @("WSL2", "Docker")) {
            $errors += "Invalid BackendType: $($config.BackendType). Must be 'WSL2' or 'Docker'"
        }
        
        # Validate backend-specific configuration
        if ($config.BackendType -eq "WSL2") {
            if (-not $config.Wsl) {
                $errors += "Missing WSL configuration for WSL2 backend"
            } else {
                if (-not $config.Wsl.Distribution) {
                    $errors += "Missing WSL Distribution"
                }
                if (-not $config.Wsl.RedisPath) {
                    $errors += "Missing WSL RedisPath"
                }
                if (-not $config.Wsl.RedisCliPath) {
                    $errors += "Missing WSL RedisCliPath"
                }
            }
        } elseif ($config.BackendType -eq "Docker") {
            if (-not $config.Docker) {
                $errors += "Missing Docker configuration for Docker backend"
            } else {
                if (-not $config.Docker.ImageName) {
                    $errors += "Missing Docker ImageName"
                }
                if (-not $config.Docker.ContainerName) {
                    $errors += "Missing Docker ContainerName"
                }
                if (-not $config.Docker.PortMapping) {
                    $errors += "Missing Docker PortMapping"
                }
            }
        }
        
        # Validate Redis configuration
        if (-not $config.Redis) {
            $errors += "Missing Redis configuration"
        } else {
            if ($config.Redis.Port -lt 1 -or $config.Redis.Port -gt 65535) {
                $errors += "Invalid Redis Port: $($config.Redis.Port). Must be between 1 and 65535"
            }
            if (-not $config.Redis.BindAddress) {
                $errors += "Missing Redis BindAddress"
            }
            if (-not $config.Redis.MaxMemory) {
                $warnings += "Redis MaxMemory not configured - this can lead to out-of-memory issues"
            }
            if ($config.Redis.RequirePassword -and -not $config.Redis.Password) {
                $errors += "Redis RequirePassword is true but Password is empty"
            }
        }
        
        # Validate Service configuration
        if (-not $config.Service) {
            $errors += "Missing Service configuration"
        } else {
            if (-not $config.Service.ServiceName) {
                $errors += "Missing Service Name"
            }
            if (-not $config.Service.DisplayName) {
                $errors += "Missing Service Display Name"
            }
            if ($config.Service.StartType -notin @("Automatic", "Manual", "Disabled")) {
                $errors += "Invalid Service StartType: $($config.Service.StartType)"
            }
        }
        
        # Validate Monitoring configuration
        if ($config.Monitoring) {
            if ($config.Monitoring.EnableHealthCheck -and $config.Monitoring.HealthCheckInterval -le 0) {
                $errors += "HealthCheckInterval must be greater than 0 if health checks are enabled"
            }
            if ($config.Monitoring.EnableFileLogging -and -not $config.Monitoring.LogFilePath) {
                $errors += "LogFilePath cannot be empty if file logging is enabled"
            }
        }
        
        # Validate Performance configuration
        if ($config.Performance) {
            if ($config.Performance.EnableAutoRestart -and $config.Performance.MaxRestartAttempts -le 0) {
                $errors += "MaxRestartAttempts must be greater than 0 if auto-restart is enabled"
            }
            if ($config.Performance.MemoryWarningThreshold -lt 0 -or $config.Performance.MemoryWarningThreshold -gt 100) {
                $errors += "MemoryWarningThreshold must be between 0 and 100"
            }
            if ($config.Performance.MemoryErrorThreshold -lt 0 -or $config.Performance.MemoryErrorThreshold -gt 100) {
                $errors += "MemoryErrorThreshold must be between 0 and 100"
            }
        }
        
        # Display results
        if ($errors.Count -eq 0) {
            Write-ColorOutput "✓ Configuration validation PASSED" "Green"
            $isValid = $true
        } else {
            Write-ColorOutput "✗ Configuration validation FAILED" "Red"
            $isValid = $false
        }
        
        if ($errors.Count -gt 0) {
            Write-ColorOutput "`nErrors:" "Red"
            foreach ($error in $errors) {
                Write-ColorOutput "  • $error" "Red"
            }
        }
        
        if ($warnings.Count -gt 0) {
            Write-ColorOutput "`nWarnings:" "Yellow"
            foreach ($warning in $warnings) {
                Write-ColorOutput "  • $warning" "Yellow"
            }
        }
        
        # Production readiness check
        $productionIssues = @()
        if ($config.Redis -and -not $config.Redis.RequirePassword) {
            $productionIssues += "Redis authentication is disabled - security risk for production"
        }
        if ($config.Redis -and -not $config.Redis.MaxMemory) {
            $productionIssues += "Redis MaxMemory is not configured - critical for production stability"
        }
        if ($config.Monitoring -and -not $config.Monitoring.EnableHealthCheck) {
            $productionIssues += "Health checks are disabled - critical for monitoring production service health"
        }
        if ($config.Performance -and -not $config.Performance.EnableAutoRestart) {
            $productionIssues += "Automatic restart on failure is disabled - critical for production resilience"
        }
        
        if ($productionIssues.Count -gt 0) {
            Write-ColorOutput "`nProduction Readiness Issues:" "Magenta"
            foreach ($issue in $productionIssues) {
                Write-ColorOutput "  • $issue" "Magenta"
            }
        } else {
            Write-ColorOutput "✓ Configuration appears production-ready" "Green"
        }
        
        return $isValid
        
    } catch {
        Write-ColorOutput "ERROR: Failed to parse configuration file: $($_.Exception.Message)" "Red"
        return $false
    }
}

# Function to display configuration summary
function Show-ConfigurationSummary {
    param(
        [string]$Path
    )
    
    try {
        $jsonContent = Get-Content $Path -Raw
        $config = $jsonContent | ConvertFrom-Json
        
        Write-ColorOutput "`nConfiguration Summary:" "Cyan"
        Write-ColorOutput "  Schema Version: $($config.SchemaVersion)" "White"
        Write-ColorOutput "  Backend Type: $($config.BackendType)" "White"
        
        if ($config.BackendType -eq "WSL2" -and $config.Wsl) {
            Write-ColorOutput "  WSL Distribution: $($config.Wsl.Distribution)" "White"
            Write-ColorOutput "  Redis Path: $($config.Wsl.RedisPath)" "White"
        } elseif ($config.BackendType -eq "Docker" -and $config.Docker) {
            Write-ColorOutput "  Docker Image: $($config.Docker.ImageName)" "White"
            Write-ColorOutput "  Container Name: $($config.Docker.ContainerName)" "White"
            Write-ColorOutput "  Port Mapping: $($config.Docker.PortMapping)" "White"
        }
        
        if ($config.Redis) {
            Write-ColorOutput "  Redis Port: $($config.Redis.Port)" "White"
            Write-ColorOutput "  Redis Bind Address: $($config.Redis.BindAddress)" "White"
            Write-ColorOutput "  Redis Max Memory: $($config.Redis.MaxMemory)" "White"
            Write-ColorOutput "  Redis Persistence: $($config.Redis.PersistenceMode)" "White"
            Write-ColorOutput "  Redis Authentication: $($config.Redis.RequirePassword)" "White"
        }
        
        if ($config.Service) {
            Write-ColorOutput "  Service Name: $($config.Service.ServiceName)" "White"
            Write-ColorOutput "  Service Start Type: $($config.Service.StartType)" "White"
        }
        
        if ($config.Monitoring) {
            Write-ColorOutput "  Health Checks: $($config.Monitoring.EnableHealthCheck)" "White"
            Write-ColorOutput "  Windows Event Log: $($config.Monitoring.EnableWindowsEventLog)" "White"
            Write-ColorOutput "  File Logging: $($config.Monitoring.EnableFileLogging)" "White"
        }
        
        if ($config.Performance) {
            Write-ColorOutput "  Auto Restart: $($config.Performance.EnableAutoRestart)" "White"
            Write-ColorOutput "  Max Restart Attempts: $($config.Performance.MaxRestartAttempts)" "White"
        }
        
    } catch {
        Write-ColorOutput "ERROR: Failed to display configuration summary: $($_.Exception.Message)" "Red"
    }
}

# Main execution
Write-ColorOutput "Redis Service Wrapper Configuration Validator" "Cyan"
Write-ColorOutput "=============================================" "Cyan"

$isValid = Test-ConfigurationFile -Path $ConfigPath

if ($Verbose) {
    Show-ConfigurationSummary -Path $ConfigPath
}

Write-ColorOutput "`nValidation completed." "Cyan"

if ($isValid) {
    exit 0
} else {
    exit 1
}
