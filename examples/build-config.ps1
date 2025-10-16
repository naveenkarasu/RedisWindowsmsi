# Redis Service Wrapper Configuration Builder
# This script demonstrates how to build configuration files using the fluent builder pattern

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("WSL2", "Docker")]
    [string]$BackendType,
    
    [Parameter(Mandatory=$true)]
    [string]$OutputPath,
    
    [string]$ServiceName = "RedisService",
    [string]$DisplayName = "Redis Service Wrapper",
    [int]$Port = 6379,
    [string]$MaxMemory = "1gb",
    [string]$Distribution = "Ubuntu-22.04",
    [string]$DockerImage = "redis:7-alpine",
    [string]$ContainerName = "redis-service",
    [switch]$EnableAuth,
    [string]$Password = "",
    [switch]$Production,
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

# Function to build WSL2 configuration
function Build-WSL2Configuration {
    param(
        [hashtable]$Params
    )
    
    $config = @{
        SchemaVersion = "1.0.0"
        BackendType = "WSL2"
        Wsl = @{
            Distribution = $Params.Distribution
            RedisPath = "/usr/bin/redis-server"
            RedisCliPath = "/usr/bin/redis-cli"
            WindowsDataPath = "C:\ProgramData\Redis\data"
            WindowsConfigPath = "C:\ProgramData\Redis\config"
        }
        Redis = @{
            Port = $Params.Port
            BindAddress = if ($Params.Production) { "0.0.0.0" } else { "127.0.0.1" }
            MaxMemory = $Params.MaxMemory
            PersistenceMode = if ($Params.Production) { "both" } else { "rdb" }
            EnablePersistence = $true
            EnableAOF = $Params.Production
            RequirePassword = $Params.EnableAuth
            Password = $Params.Password
            LogLevel = if ($Params.Production) { "warning" } else { "notice" }
        }
        Service = @{
            ServiceName = $Params.ServiceName
            DisplayName = $Params.DisplayName
            Description = "Redis service running on WSL2"
            StartType = "Automatic"
            FailureActions = @{
                ResetPeriod = 86400
                RestartDelay = if ($Params.Production) { 10000 } else { 5000 }
                Actions = @(
                    @{
                        Type = "restart"
                        Delay = 1000
                    },
                    @{
                        Type = "restart"
                        Delay = 2000
                    },
                    @{
                        Type = "restart"
                        Delay = if ($Params.Production) { 10000 } else { 5000 }
                    }
                )
            }
        }
        Monitoring = @{
            EnableHealthCheck = $true
            HealthCheckInterval = if ($Params.Production) { 15 } else { 30 }
            HealthCheckTimeout = if ($Params.Production) { 5 } else { 10 }
            EnableWindowsEventLog = $true
            EventLogSource = $Params.ServiceName
            EnableFileLogging = $true
            LogFilePath = "C:\ProgramData\Redis\logs\service.log"
            MaxLogSizeMB = if ($Params.Production) { 500 } else { 100 }
            MaxLogFiles = if ($Params.Production) { 10 } else { 5 }
            LogLevel = if ($Params.Production) { "Warning" } else { "Info" }
        }
        Performance = @{
            EnableAutoRestart = $true
            MaxRestartAttempts = if ($Params.Production) { 5 } else { 3 }
            RestartCooldown = if ($Params.Production) { 60 } else { 30 }
            MemoryWarningThreshold = if ($Params.Production) { 85 } else { 80 }
            MemoryErrorThreshold = 95
            EnableSlowLogMonitoring = $true
            SlowLogThreshold = if ($Params.Production) { 5000 } else { 10000 }
        }
        Advanced = @{
            CustomStartupArgs = @()
            EnvironmentVariables = @{}
            PreStartScript = ""
            PostStartScript = ""
            PreStopScript = ""
            PostStopScript = ""
        }
        Metadata = @{
            ConfigVersion = "1.0.0"
            CreatedBy = "Redis Windows Installer"
            CreatedDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
            LastModifiedDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
            Description = if ($Params.Production) { "WSL2 Redis configuration for production environment" } else { "WSL2 Redis configuration for development environment" }
        }
    }
    
    return $config
}

# Function to build Docker configuration
function Build-DockerConfiguration {
    param(
        [hashtable]$Params
    )
    
    $config = @{
        SchemaVersion = "1.0.0"
        BackendType = "Docker"
        Docker = @{
            ImageName = $Params.DockerImage
            ContainerName = $Params.ContainerName
            PortMapping = "$($Params.Port):$($Params.Port)"
            VolumeMappings = @(
                "C:\ProgramData\Redis\data:/data",
                "C:\ProgramData\Redis\config:/usr/local/etc/redis"
            )
            ResourceLimits = @{
                Memory = $Params.MaxMemory
                Cpus = if ($Params.Production) { "2.0" } else { "1.0" }
            }
            EnvironmentVariables = @{}
            RestartPolicy = "unless-stopped"
        }
        Redis = @{
            Port = $Params.Port
            BindAddress = "0.0.0.0"
            MaxMemory = $Params.MaxMemory
            PersistenceMode = if ($Params.Production) { "both" } else { "rdb" }
            EnablePersistence = $true
            EnableAOF = $Params.Production
            RequirePassword = $Params.EnableAuth
            Password = $Params.Password
            LogLevel = if ($Params.Production) { "warning" } else { "notice" }
        }
        Service = @{
            ServiceName = $Params.ServiceName
            DisplayName = if ($Params.Production) { "Redis Production Service" } else { "Redis Service Wrapper (Docker)" }
            Description = "Redis service running in Docker container"
            StartType = "Automatic"
            FailureActions = @{
                ResetPeriod = 86400
                RestartDelay = if ($Params.Production) { 10000 } else { 5000 }
                Actions = @(
                    @{
                        Type = "restart"
                        Delay = 1000
                    },
                    @{
                        Type = "restart"
                        Delay = 2000
                    },
                    @{
                        Type = "restart"
                        Delay = if ($Params.Production) { 10000 } else { 5000 }
                    }
                )
            }
        }
        Monitoring = @{
            EnableHealthCheck = $true
            HealthCheckInterval = if ($Params.Production) { 15 } else { 30 }
            HealthCheckTimeout = if ($Params.Production) { 5 } else { 10 }
            EnableWindowsEventLog = $true
            EventLogSource = $Params.ServiceName
            EnableFileLogging = $true
            LogFilePath = "C:\ProgramData\Redis\logs\service.log"
            MaxLogSizeMB = if ($Params.Production) { 500 } else { 100 }
            MaxLogFiles = if ($Params.Production) { 10 } else { 5 }
            LogLevel = if ($Params.Production) { "Warning" } else { "Info" }
        }
        Performance = @{
            EnableAutoRestart = $true
            MaxRestartAttempts = if ($Params.Production) { 5 } else { 3 }
            RestartCooldown = if ($Params.Production) { 60 } else { 30 }
            MemoryWarningThreshold = if ($Params.Production) { 85 } else { 80 }
            MemoryErrorThreshold = 95
            EnableSlowLogMonitoring = $true
            SlowLogThreshold = if ($Params.Production) { 5000 } else { 10000 }
        }
        Advanced = @{
            CustomStartupArgs = if ($Params.Production) {
                @(
                    "--appendonly yes",
                    "--appendfsync everysec",
                    "--save 900 1",
                    "--save 300 10",
                    "--save 60 10000"
                )
            } else {
                @()
            }
            EnvironmentVariables = @{}
            PreStartScript = ""
            PostStartScript = ""
            PreStopScript = ""
            PostStopScript = ""
        }
        Metadata = @{
            ConfigVersion = "1.0.0"
            CreatedBy = "Redis Windows Installer"
            CreatedDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
            LastModifiedDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
            Description = if ($Params.Production) { "Docker Redis configuration for production environment" } else { "Docker Redis configuration for development environment" }
        }
    }
    
    # Add production-specific Docker settings
    if ($Params.Production) {
        $config.Docker.VolumeMappings += "C:\ProgramData\Redis\logs:/var/log/redis"
        $config.Docker.EnvironmentVariables = @{
            REDIS_REPLICATION_MODE = "master"
        }
        $config.Advanced.EnvironmentVariables = @{
            REDIS_PASSWORD_ENV_VAR = "REDIS_PASSWORD_ENV_VAR"
        }
        $config.Advanced.PreStartScript = "C:\ProgramData\Redis\scripts\pre-start.bat"
        $config.Advanced.PostStartScript = "C:\ProgramData\Redis\scripts\post-start.bat"
        $config.Advanced.PreStopScript = "C:\ProgramData\Redis\scripts\pre-stop.bat"
        $config.Advanced.PostStopScript = "C:\ProgramData\Redis\scripts\post-stop.bat"
    }
    
    return $config
}

# Function to save configuration to file
function Save-Configuration {
    param(
        [hashtable]$Config,
        [string]$Path
    )
    
    try {
        # Convert hashtable to JSON with proper formatting
        $json = $Config | ConvertTo-Json -Depth 10
        
        # Ensure directory exists
        $directory = Split-Path $Path -Parent
        if (-not (Test-Path $directory)) {
            New-Item -ItemType Directory -Path $directory -Force | Out-Null
        }
        
        # Save to file
        $json | Out-File -FilePath $Path -Encoding UTF8
        
        Write-ColorOutput "✓ Configuration saved to: $Path" "Green"
        return $true
    } catch {
        Write-ColorOutput "ERROR: Failed to save configuration: $($_.Exception.Message)" "Red"
        return $false
    }
}

# Function to display configuration summary
function Show-ConfigurationSummary {
    param(
        [hashtable]$Config
    )
    
    Write-ColorOutput "`nConfiguration Summary:" "Cyan"
    Write-ColorOutput "  Schema Version: $($Config.SchemaVersion)" "White"
    Write-ColorOutput "  Backend Type: $($Config.BackendType)" "White"
    
    if ($Config.BackendType -eq "WSL2") {
        Write-ColorOutput "  WSL Distribution: $($Config.Wsl.Distribution)" "White"
        Write-ColorOutput "  Redis Path: $($Config.Wsl.RedisPath)" "White"
    } elseif ($Config.BackendType -eq "Docker") {
        Write-ColorOutput "  Docker Image: $($Config.Docker.ImageName)" "White"
        Write-ColorOutput "  Container Name: $($Config.Docker.ContainerName)" "White"
        Write-ColorOutput "  Port Mapping: $($Config.Docker.PortMapping)" "White"
        Write-ColorOutput "  Resource Limits: $($Config.Docker.ResourceLimits.Memory) memory, $($Config.Docker.ResourceLimits.Cpus) CPUs" "White"
    }
    
    Write-ColorOutput "  Redis Port: $($Config.Redis.Port)" "White"
    Write-ColorOutput "  Redis Bind Address: $($Config.Redis.BindAddress)" "White"
    Write-ColorOutput "  Redis Max Memory: $($Config.Redis.MaxMemory)" "White"
    Write-ColorOutput "  Redis Persistence: $($Config.Redis.PersistenceMode)" "White"
    Write-ColorOutput "  Redis Authentication: $($Config.Redis.RequirePassword)" "White"
    Write-ColorOutput "  Service Name: $($Config.Service.ServiceName)" "White"
    Write-ColorOutput "  Service Start Type: $($Config.Service.StartType)" "White"
    Write-ColorOutput "  Health Checks: $($Config.Monitoring.EnableHealthCheck)" "White"
    Write-ColorOutput "  Auto Restart: $($Config.Performance.EnableAutoRestart)" "White"
    Write-ColorOutput "  Max Restart Attempts: $($Config.Performance.MaxRestartAttempts)" "White"
}

# Main execution
Write-ColorOutput "Redis Service Wrapper Configuration Builder" "Cyan"
Write-ColorOutput "===========================================" "Cyan"

# Prepare parameters
$params = @{
    BackendType = $BackendType
    ServiceName = $ServiceName
    DisplayName = $DisplayName
    Port = $Port
    MaxMemory = $MaxMemory
    Distribution = $Distribution
    DockerImage = $DockerImage
    ContainerName = $ContainerName
    EnableAuth = $EnableAuth
    Password = $Password
    Production = $Production
}

Write-ColorOutput "Building configuration with parameters:" "Yellow"
Write-ColorOutput "  Backend Type: $BackendType" "White"
Write-ColorOutput "  Service Name: $ServiceName" "White"
Write-ColorOutput "  Port: $Port" "White"
Write-ColorOutput "  Max Memory: $MaxMemory" "White"
Write-ColorOutput "  Production Mode: $Production" "White"
Write-ColorOutput "  Authentication: $EnableAuth" "White"

# Build configuration based on backend type
if ($BackendType -eq "WSL2") {
    Write-ColorOutput "  WSL Distribution: $Distribution" "White"
    $config = Build-WSL2Configuration -Params $params
} else {
    Write-ColorOutput "  Docker Image: $DockerImage" "White"
    Write-ColorOutput "  Container Name: $ContainerName" "White"
    $config = Build-DockerConfiguration -Params $params
}

# Save configuration
$success = Save-Configuration -Config $config -Path $OutputPath

if ($success) {
    if ($Verbose) {
        Show-ConfigurationSummary -Config $config
    }
    
    Write-ColorOutput "`n✓ Configuration built successfully!" "Green"
    Write-ColorOutput "You can now use this configuration file with the Redis Service Wrapper." "Cyan"
    
    # Suggest next steps
    Write-ColorOutput "`nNext steps:" "Yellow"
    Write-ColorOutput "1. Review the configuration file: $OutputPath" "White"
    Write-ColorOutput "2. Validate the configuration using: .\validate-config.ps1 -ConfigPath '$OutputPath'" "White"
    Write-ColorOutput "3. Install the Redis Service Wrapper with this configuration" "White"
    
    exit 0
} else {
    Write-ColorOutput "`n✗ Failed to build configuration!" "Red"
    exit 1
}
