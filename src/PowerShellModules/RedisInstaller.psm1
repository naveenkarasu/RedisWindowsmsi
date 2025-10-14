#Requires -Version 5.1

<#
.SYNOPSIS
    Redis Windows Installer PowerShell Module
    
.DESCRIPTION
    This module provides functions to detect, install, and configure Redis backends
    (WSL2 or Docker Desktop) on Windows systems.
    
.NOTES
    Module Name: RedisInstaller
    Author: Redis Windows MSI Project
    Version: 1.0.0
    Requires: PowerShell 5.1+, Administrator privileges
#>

# Module Variables
$script:ModuleName = "RedisInstaller"
$script:LogPath = "$env:ProgramData\Redis\logs\installer.log"

#region Helper Functions

<#
.SYNOPSIS
    Gets the Windows version information
    
.DESCRIPTION
    Returns detailed Windows version information including build number
    
.OUTPUTS
    PSCustomObject with OS details
    
.EXAMPLE
    $winVersion = Get-WindowsVersion
    if ($winVersion.BuildNumber -ge 19041) {
        Write-Host "WSL2 compatible"
    }
#>
function Get-WindowsVersion {
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param()
    
    try {
        $os = Get-CimInstance -ClassName Win32_OperatingSystem
        $version = [System.Environment]::OSVersion.Version
        
        return [PSCustomObject]@{
            ProductName   = $os.Caption
            Version       = $os.Version
            BuildNumber   = $version.Build
            MajorVersion  = $version.Major
            MinorVersion  = $version.Minor
            IsServer      = $os.ProductType -ne 1
            Architecture  = $os.OSArchitecture
            IsWSL2Compatible = ($version.Build -ge 19041)
        }
    }
    catch {
        Write-Error "Failed to get Windows version: $_"
        return $null
    }
}

<#
.SYNOPSIS
    Tests if the current session is running with Administrator privileges
    
.DESCRIPTION
    Checks if the current PowerShell session has Administrator rights
    
.OUTPUTS
    Boolean indicating Administrator status
    
.EXAMPLE
    if (-not (Test-IsAdministrator)) {
        throw "This operation requires Administrator privileges"
    }
#>
function Test-IsAdministrator {
    [CmdletBinding()]
    [OutputType([bool])]
    param()
    
    try {
        $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
        $principal = New-Object Security.Principal.WindowsPrincipal($identity)
        $adminRole = [Security.Principal.WindowsBuiltInRole]::Administrator
        
        return $principal.IsInRole($adminRole)
    }
    catch {
        Write-Warning "Could not determine administrator status: $_"
        return $false
    }
}

<#
.SYNOPSIS
    Writes a log message to the installer log file
    
.DESCRIPTION
    Logs messages with timestamp and severity level to the installer log file.
    Creates the log directory if it doesn't exist.
    
.PARAMETER Message
    The message to log
    
.PARAMETER Level
    The severity level: Info, Warning, Error, Success
    
.PARAMETER NoConsole
    If specified, suppresses console output
    
.EXAMPLE
    Write-LogMessage "Starting WSL2 installation" -Level Info
    Write-LogMessage "Installation failed" -Level Error
#>
function Write-LogMessage {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message,
        
        [Parameter(Mandatory = $false)]
        [ValidateSet("Info", "Warning", "Error", "Success", "Debug")]
        [string]$Level = "Info",
        
        [Parameter(Mandatory = $false)]
        [switch]$NoConsole
    )
    
    try {
        # Ensure log directory exists
        $logDir = Split-Path -Path $script:LogPath -Parent
        if (-not (Test-Path $logDir)) {
            New-Item -ItemType Directory -Path $logDir -Force | Out-Null
        }
        
        # Format log entry
        $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        $logEntry = "[$timestamp] [$Level] $Message"
        
        # Write to log file
        Add-Content -Path $script:LogPath -Value $logEntry -ErrorAction SilentlyContinue
        
        # Write to console unless suppressed
        if (-not $NoConsole) {
            switch ($Level) {
                "Info"    { Write-Host $Message -ForegroundColor Cyan }
                "Warning" { Write-Warning $Message }
                "Error"   { Write-Host $Message -ForegroundColor Red }
                "Success" { Write-Host $Message -ForegroundColor Green }
                "Debug"   { Write-Verbose $Message }
            }
        }
    }
    catch {
        Write-Warning "Failed to write log message: $_"
    }
}

<#
.SYNOPSIS
    Tests if a command is available in the current session
    
.DESCRIPTION
    Checks if a command exists and is available for execution
    
.PARAMETER Command
    The command name to test
    
.OUTPUTS
    Boolean indicating if command is available
    
.EXAMPLE
    if (Test-CommandExists "docker") {
        Write-Host "Docker CLI is available"
    }
#>
function Test-CommandExists {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Command
    )
    
    try {
        $null = Get-Command $Command -ErrorAction Stop
        return $true
    }
    catch {
        return $false
    }
}

<#
.SYNOPSIS
    Downloads a file from a URL with progress indication
    
.DESCRIPTION
    Downloads a file from the specified URL to the destination path,
    showing progress in the console
    
.PARAMETER Url
    The URL to download from
    
.PARAMETER Destination
    The local file path to save to
    
.OUTPUTS
    Boolean indicating success
    
.EXAMPLE
    Download-File "https://example.com/file.exe" "C:\Temp\file.exe"
#>
function Download-File {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Url,
        
        [Parameter(Mandatory = $true)]
        [string]$Destination
    )
    
    try {
        Write-LogMessage "Downloading from $Url" -Level Info
        
        # Ensure destination directory exists
        $destDir = Split-Path -Path $Destination -Parent
        if (-not (Test-Path $destDir)) {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
        }
        
        # Download with progress
        $webClient = New-Object System.Net.WebClient
        $webClient.DownloadFile($Url, $Destination)
        
        Write-LogMessage "Download completed: $Destination" -Level Success
        return $true
    }
    catch {
        Write-LogMessage "Download failed: $_" -Level Error
        return $false
    }
    finally {
        if ($webClient) {
            $webClient.Dispose()
        }
    }
}

#endregion

#region Detection Functions

<#
.SYNOPSIS
    Tests if WSL2 is installed and properly configured
    
.DESCRIPTION
    Checks if Windows Subsystem for Linux 2 is installed, enabled, and has
    at least one distribution available. Also verifies Windows version compatibility.
    
.OUTPUTS
    PSCustomObject with installation status and details
    
.EXAMPLE
    $wsl2Status = Test-WSL2Installed
    if ($wsl2Status.IsInstalled) {
        Write-Host "WSL2 is ready with distribution: $($wsl2Status.DefaultDistribution)"
    }
#>
function Test-WSL2Installed {
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param()
    
    Write-LogMessage "Checking WSL2 installation status..." -Level Info
    
    $result = [PSCustomObject]@{
        IsInstalled = $false
        IsEnabled = $false
        HasDistribution = $false
        DefaultDistribution = $null
        Distributions = @()
        Version = $null
        IsCompatible = $false
        ErrorMessage = $null
    }
    
    try {
        # Check Windows version compatibility (WSL2 requires Build 19041 or higher)
        $winVersion = Get-WindowsVersion
        if (-not $winVersion -or -not $winVersion.IsWSL2Compatible) {
            $result.ErrorMessage = "Windows version not compatible. Requires Windows 10 Build 19041 or higher."
            Write-LogMessage $result.ErrorMessage -Level Warning
            return $result
        }
        $result.IsCompatible = $true
        
        # Check if WSL command exists
        if (-not (Test-CommandExists "wsl")) {
            $result.ErrorMessage = "WSL command not found. WSL is not installed."
            Write-LogMessage $result.ErrorMessage -Level Info
            return $result
        }
        
        # Check WSL status
        try {
            $wslStatus = wsl --status 2>&1
            if ($LASTEXITCODE -eq 0) {
                $result.IsEnabled = $true
                
                # Extract version information
                if ($wslStatus -match "Default Version:\s*(\d+)") {
                    $result.Version = $matches[1]
                    if ($result.Version -eq "2") {
                        $result.IsInstalled = $true
                    }
                }
            }
        }
        catch {
            Write-LogMessage "WSL status check failed: $_" -Level Debug
        }
        
        # Check for installed distributions
        try {
            $wslList = wsl --list --quiet 2>&1
            if ($LASTEXITCODE -eq 0 -and $wslList) {
                $distributions = $wslList -split "`n" | Where-Object { $_ -and $_.Trim() } | ForEach-Object { $_.Trim() }
                $result.Distributions = $distributions
                
                if ($distributions.Count -gt 0) {
                    $result.HasDistribution = $true
                    
                    # Get default distribution
                    $defaultDistro = wsl --list --verbose 2>&1 | Select-String -Pattern "\*.*" | Select-Object -First 1
                    if ($defaultDistro) {
                        $distroName = ($defaultDistro -replace '\*', '').Trim() -replace '\s+.*', ''
                        $result.DefaultDistribution = $distroName
                    } else {
                        $result.DefaultDistribution = $distributions[0]
                    }
                }
            }
        }
        catch {
            Write-LogMessage "Failed to list WSL distributions: $_" -Level Debug
        }
        
        # Final determination
        if ($result.IsCompatible -and $result.IsEnabled -and $result.HasDistribution) {
            Write-LogMessage "WSL2 is installed and ready (Distribution: $($result.DefaultDistribution))" -Level Success
        }
        elseif ($result.IsCompatible -and $result.IsEnabled -and -not $result.HasDistribution) {
            $result.ErrorMessage = "WSL2 is enabled but no distribution is installed"
            Write-LogMessage $result.ErrorMessage -Level Warning
        }
        else {
            Write-LogMessage "WSL2 is not fully configured" -Level Info
        }
        
        return $result
    }
    catch {
        $result.ErrorMessage = "Error checking WSL2 status: $_"
        Write-LogMessage $result.ErrorMessage -Level Error
        return $result
    }
}

<#
.SYNOPSIS
    Tests if Docker Desktop is installed and running
    
.DESCRIPTION
    Checks if Docker Desktop is installed on the system by verifying registry entries,
    command availability, and daemon connectivity.
    
.OUTPUTS
    PSCustomObject with installation status and details
    
.EXAMPLE
    $dockerStatus = Test-DockerInstalled
    if ($dockerStatus.IsInstalled -and $dockerStatus.IsRunning) {
        Write-Host "Docker Desktop is ready (Version: $($dockerStatus.Version))"
    }
#>
function Test-DockerInstalled {
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param()
    
    Write-LogMessage "Checking Docker Desktop installation status..." -Level Info
    
    $result = [PSCustomObject]@{
        IsInstalled = $false
        IsRunning = $false
        Version = $null
        InstallPath = $null
        ErrorMessage = $null
    }
    
    try {
        # Check registry for Docker Desktop installation
        $dockerRegPaths = @(
            "HKLM:\SOFTWARE\Docker Inc.\Docker",
            "HKCU:\SOFTWARE\Docker Inc.\Docker"
        )
        
        foreach ($regPath in $dockerRegPaths) {
            if (Test-Path $regPath) {
                $result.IsInstalled = $true
                try {
                    $installPath = (Get-ItemProperty -Path $regPath -ErrorAction SilentlyContinue)."InstallPath"
                    if ($installPath) {
                        $result.InstallPath = $installPath
                    }
                }
                catch {
                    Write-LogMessage "Could not read Docker install path from registry" -Level Debug
                }
                break
            }
        }
        
        # Check if Docker command exists
        if (Test-CommandExists "docker") {
            $result.IsInstalled = $true
            
            # Get Docker version
            try {
                $dockerVersion = docker --version 2>&1
                if ($LASTEXITCODE -eq 0 -and $dockerVersion) {
                    if ($dockerVersion -match "Docker version\s+([\d\.]+)") {
                        $result.Version = $matches[1]
                    }
                }
            }
            catch {
                Write-LogMessage "Could not get Docker version: $_" -Level Debug
            }
            
            # Test if Docker daemon is running
            try {
                $dockerPs = docker ps 2>&1
                if ($LASTEXITCODE -eq 0) {
                    $result.IsRunning = $true
                    Write-LogMessage "Docker Desktop is installed and running (Version: $($result.Version))" -Level Success
                }
                else {
                    $result.ErrorMessage = "Docker is installed but daemon is not running"
                    Write-LogMessage $result.ErrorMessage -Level Warning
                }
            }
            catch {
                $result.ErrorMessage = "Docker is installed but not accessible: $_"
                Write-LogMessage $result.ErrorMessage -Level Warning
            }
        }
        else {
            if (-not $result.IsInstalled) {
                $result.ErrorMessage = "Docker Desktop is not installed"
                Write-LogMessage $result.ErrorMessage -Level Info
            }
            else {
                $result.ErrorMessage = "Docker Desktop is installed but command not in PATH"
                Write-LogMessage $result.ErrorMessage -Level Warning
            }
        }
        
        return $result
    }
    catch {
        $result.ErrorMessage = "Error checking Docker status: $_"
        Write-LogMessage $result.ErrorMessage -Level Error
        return $result
    }
}

<#
.SYNOPSIS
    Detects which Redis backend (WSL2 or Docker) is available
    
.DESCRIPTION
    Auto-detects available backends in order of preference (WSL2 first, then Docker).
    Returns the recommended backend type or "None" if neither is available.
    
.OUTPUTS
    String indicating backend type: "WSL2", "Docker", or "None"
    
.EXAMPLE
    $backend = Get-RedisBackend
    switch ($backend) {
        "WSL2"   { Write-Host "Using WSL2 backend" }
        "Docker" { Write-Host "Using Docker backend" }
        "None"   { Write-Host "No backend available - installation required" }
    }
#>
function Get-RedisBackend {
    [CmdletBinding()]
    [OutputType([string])]
    param()
    
    Write-LogMessage "Auto-detecting available Redis backend..." -Level Info
    
    try {
        # Check WSL2 first (preferred)
        $wsl2Status = Test-WSL2Installed
        if ($wsl2Status.IsCompatible -and $wsl2Status.IsEnabled -and $wsl2Status.HasDistribution) {
            Write-LogMessage "Detected backend: WSL2 (Distribution: $($wsl2Status.DefaultDistribution))" -Level Success
            return "WSL2"
        }
        
        # Check Docker as fallback
        $dockerStatus = Test-DockerInstalled
        if ($dockerStatus.IsInstalled -and $dockerStatus.IsRunning) {
            Write-LogMessage "Detected backend: Docker Desktop (Version: $($dockerStatus.Version))" -Level Success
            return "Docker"
        }
        
        # Check if WSL2 is partially installed
        if ($wsl2Status.IsCompatible -and $wsl2Status.IsEnabled -and -not $wsl2Status.HasDistribution) {
            Write-LogMessage "WSL2 is enabled but needs a distribution - can be used after distribution installation" -Level Info
            return "WSL2-Partial"
        }
        
        # Check if Docker is installed but not running
        if ($dockerStatus.IsInstalled -and -not $dockerStatus.IsRunning) {
            Write-LogMessage "Docker Desktop is installed but not running - can be used after starting" -Level Info
            return "Docker-NotRunning"
        }
        
        # No backend available
        Write-LogMessage "No Redis backend detected - installation required" -Level Warning
        return "None"
    }
    catch {
        Write-LogMessage "Error detecting backend: $_" -Level Error
        return "None"
    }
}

#endregion

#region Installation Functions

<#
.SYNOPSIS
    Installs and configures WSL2 on the system
    
.DESCRIPTION
    Automatically installs Windows Subsystem for Linux 2 by:
    - Enabling required Windows features
    - Installing WSL2 kernel update
    - Installing Ubuntu distribution
    - Configuring WSL2 as default version
    
    May require system restart to complete installation.
    
.PARAMETER DistributionName
    The WSL distribution to install. Default is "Ubuntu"
    
.PARAMETER SkipRestart
    If specified, does not prompt for restart even if required
    
.OUTPUTS
    PSCustomObject with installation status and details
    
.EXAMPLE
    $result = Install-WSL2
    if ($result.Success) {
        Write-Host "WSL2 installed successfully"
        if ($result.RestartRequired) {
            Write-Host "Please restart your computer"
        }
    }
#>
function Install-WSL2 {
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory = $false)]
        [string]$DistributionName = "Ubuntu",
        
        [Parameter(Mandatory = $false)]
        [switch]$SkipRestart
    )
    
    Write-LogMessage "Starting WSL2 installation..." -Level Info
    
    $result = [PSCustomObject]@{
        Success = $false
        RestartRequired = $false
        DistributionInstalled = $false
        ErrorMessage = $null
        Steps = @()
    }
    
    try {
        # Verify administrator privileges
        if (-not (Test-IsAdministrator)) {
            $result.ErrorMessage = "Administrator privileges required for WSL2 installation"
            Write-LogMessage $result.ErrorMessage -Level Error
            return $result
        }
        
        # Check Windows version compatibility
        $winVersion = Get-WindowsVersion
        if (-not $winVersion.IsWSL2Compatible) {
            $result.ErrorMessage = "Windows version not compatible. Requires Windows 10 Build 19041 or higher."
            Write-LogMessage $result.ErrorMessage -Level Error
            return $result
        }
        
        # Step 1: Enable WSL feature
        Write-LogMessage "Enabling Windows Subsystem for Linux feature..." -Level Info
        try {
            $wslFeature = Get-WindowsOptionalFeature -Online -FeatureName Microsoft-Windows-Subsystem-Linux
            if ($wslFeature.State -ne "Enabled") {
                Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Windows-Subsystem-Linux -NoRestart -WarningAction SilentlyContinue | Out-Null
                $result.RestartRequired = $true
                $result.Steps += "Enabled WSL feature"
                Write-LogMessage "WSL feature enabled" -Level Success
            } else {
                $result.Steps += "WSL feature already enabled"
                Write-LogMessage "WSL feature already enabled" -Level Info
            }
        }
        catch {
            $result.ErrorMessage = "Failed to enable WSL feature: $_"
            Write-LogMessage $result.ErrorMessage -Level Error
            return $result
        }
        
        # Step 2: Enable Virtual Machine Platform
        Write-LogMessage "Enabling Virtual Machine Platform feature..." -Level Info
        try {
            $vmFeature = Get-WindowsOptionalFeature -Online -FeatureName VirtualMachinePlatform
            if ($vmFeature.State -ne "Enabled") {
                Enable-WindowsOptionalFeature -Online -FeatureName VirtualMachinePlatform -NoRestart -WarningAction SilentlyContinue | Out-Null
                $result.RestartRequired = $true
                $result.Steps += "Enabled Virtual Machine Platform"
                Write-LogMessage "Virtual Machine Platform enabled" -Level Success
            } else {
                $result.Steps += "Virtual Machine Platform already enabled"
                Write-LogMessage "Virtual Machine Platform already enabled" -Level Info
            }
        }
        catch {
            $result.ErrorMessage = "Failed to enable Virtual Machine Platform: $_"
            Write-LogMessage $result.ErrorMessage -Level Error
            return $result
        }
        
        # Step 3: Download and install WSL2 kernel update (if needed)
        if (-not $result.RestartRequired) {
            Write-LogMessage "Installing WSL2 kernel update..." -Level Info
            try {
                $kernelUrl = "https://wslstorestorage.blob.core.windows.net/wslblob/wsl_update_x64.msi"
                $kernelInstaller = "$env:TEMP\wsl_update_x64.msi"
                
                # Download kernel update
                if (Download-File -Url $kernelUrl -Destination $kernelInstaller) {
                    # Install kernel update silently
                    $installArgs = "/i `"$kernelInstaller`" /quiet /norestart"
                    $process = Start-Process -FilePath "msiexec.exe" -ArgumentList $installArgs -Wait -PassThru
                    
                    if ($process.ExitCode -eq 0 -or $process.ExitCode -eq 3010) {
                        $result.Steps += "WSL2 kernel update installed"
                        Write-LogMessage "WSL2 kernel update installed" -Level Success
                        
                        # Clean up installer
                        Remove-Item $kernelInstaller -Force -ErrorAction SilentlyContinue
                    } else {
                        Write-LogMessage "Kernel update installation returned exit code: $($process.ExitCode)" -Level Warning
                    }
                }
            }
            catch {
                Write-LogMessage "WSL2 kernel update installation failed (may already be installed): $_" -Level Warning
                $result.Steps += "Kernel update skipped (may already be installed)"
            }
        }
        
        # Step 4: Set WSL2 as default version
        if (-not $result.RestartRequired) {
            Write-LogMessage "Setting WSL2 as default version..." -Level Info
            try {
                $wslProcess = Start-Process -FilePath "wsl" -ArgumentList "--set-default-version 2" -Wait -PassThru -NoNewWindow
                if ($wslProcess.ExitCode -eq 0) {
                    $result.Steps += "WSL2 set as default version"
                    Write-LogMessage "WSL2 set as default version" -Level Success
                }
            }
            catch {
                Write-LogMessage "Failed to set WSL2 as default (may require restart): $_" -Level Warning
            }
        }
        
        # Step 5: Install Ubuntu distribution
        if (-not $result.RestartRequired) {
            Write-LogMessage "Installing $DistributionName distribution..." -Level Info
            try {
                # Use wsl --install command (Windows 10 version 2004 and higher)
                $installArgs = "--install -d $DistributionName"
                $wslInstall = Start-Process -FilePath "wsl" -ArgumentList $installArgs -Wait -PassThru -NoNewWindow
                
                if ($wslInstall.ExitCode -eq 0) {
                    $result.DistributionInstalled = $true
                    $result.Steps += "$DistributionName distribution installed"
                    Write-LogMessage "$DistributionName distribution installed" -Level Success
                } else {
                    Write-LogMessage "Distribution installation returned exit code: $($wslInstall.ExitCode)" -Level Warning
                    $result.Steps += "Distribution installation may require manual completion"
                }
            }
            catch {
                Write-LogMessage "Failed to install distribution: $_" -Level Warning
                $result.Steps += "Distribution installation failed - install manually after restart"
            }
        }
        
        # Determine overall success
        if ($result.RestartRequired) {
            $result.Success = $true
            Write-LogMessage "WSL2 features enabled successfully. System restart required to complete installation." -Level Success
            
            if (-not $SkipRestart) {
                Write-LogMessage "Please restart your computer and run the installer again to complete WSL2 setup." -Level Warning
            }
        }
        elseif ($result.DistributionInstalled) {
            $result.Success = $true
            Write-LogMessage "WSL2 installed and configured successfully!" -Level Success
        }
        else {
            $result.Success = $true
            Write-LogMessage "WSL2 installation partially completed. You may need to install a distribution manually." -Level Warning
        }
        
        return $result
    }
    catch {
        $result.ErrorMessage = "WSL2 installation failed: $_"
        Write-LogMessage $result.ErrorMessage -Level Error
        return $result
    }
}

<#
.SYNOPSIS
    Installs Docker Desktop on the system
    
.DESCRIPTION
    Downloads and installs Docker Desktop for Windows with silent installation.
    Waits for Docker daemon to start after installation.
    
.PARAMETER InstallerUrl
    URL to download Docker Desktop installer. If not specified, uses the latest stable version.
    
.PARAMETER SkipDaemonWait
    If specified, does not wait for Docker daemon to start after installation
    
.OUTPUTS
    PSCustomObject with installation status and details
    
.EXAMPLE
    $result = Install-DockerDesktop
    if ($result.Success) {
        Write-Host "Docker Desktop installed successfully"
    }
#>
function Install-DockerDesktop {
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory = $false)]
        [string]$InstallerUrl = "https://desktop.docker.com/win/stable/Docker%20Desktop%20Installer.exe",
        
        [Parameter(Mandatory = $false)]
        [switch]$SkipDaemonWait
    )
    
    Write-LogMessage "Starting Docker Desktop installation..." -Level Info
    
    $result = [PSCustomObject]@{
        Success = $false
        DaemonStarted = $false
        Version = $null
        ErrorMessage = $null
        Steps = @()
    }
    
    try {
        # Verify administrator privileges
        if (-not (Test-IsAdministrator)) {
            $result.ErrorMessage = "Administrator privileges required for Docker Desktop installation"
            Write-LogMessage $result.ErrorMessage -Level Error
            return $result
        }
        
        # Check if Docker is already installed
        $dockerStatus = Test-DockerInstalled
        if ($dockerStatus.IsInstalled) {
            Write-LogMessage "Docker Desktop is already installed (Version: $($dockerStatus.Version))" -Level Info
            $result.Success = $true
            $result.Version = $dockerStatus.Version
            $result.DaemonStarted = $dockerStatus.IsRunning
            $result.Steps += "Docker Desktop already installed"
            return $result
        }
        
        # Step 1: Download Docker Desktop installer
        Write-LogMessage "Downloading Docker Desktop installer..." -Level Info
        $installerPath = "$env:TEMP\DockerDesktopInstaller.exe"
        
        if (-not (Download-File -Url $InstallerUrl -Destination $installerPath)) {
            $result.ErrorMessage = "Failed to download Docker Desktop installer"
            Write-LogMessage $result.ErrorMessage -Level Error
            return $result
        }
        $result.Steps += "Downloaded installer"
        
        # Step 2: Run installer
        Write-LogMessage "Running Docker Desktop installer (this may take several minutes)..." -Level Info
        try {
            # Silent installation arguments
            $installArgs = "install --quiet --accept-license"
            $process = Start-Process -FilePath $installerPath -ArgumentList $installArgs -Wait -PassThru -NoNewWindow
            
            if ($process.ExitCode -eq 0) {
                $result.Steps += "Docker Desktop installed"
                Write-LogMessage "Docker Desktop installation completed" -Level Success
            }
            else {
                $result.ErrorMessage = "Docker Desktop installer exited with code: $($process.ExitCode)"
                Write-LogMessage $result.ErrorMessage -Level Error
                return $result
            }
        }
        catch {
            $result.ErrorMessage = "Failed to run Docker Desktop installer: $_"
            Write-LogMessage $result.ErrorMessage -Level Error
            return $result
        }
        finally {
            # Clean up installer
            if (Test-Path $installerPath) {
                Remove-Item $installerPath -Force -ErrorAction SilentlyContinue
            }
        }
        
        # Step 3: Wait for Docker daemon to start
        if (-not $SkipDaemonWait) {
            Write-LogMessage "Waiting for Docker daemon to start..." -Level Info
            $maxWaitSeconds = 180
            $waitInterval = 5
            $elapsedSeconds = 0
            
            while ($elapsedSeconds -lt $maxWaitSeconds) {
                Start-Sleep -Seconds $waitInterval
                $elapsedSeconds += $waitInterval
                
                try {
                    $dockerCheck = docker ps 2>&1
                    if ($LASTEXITCODE -eq 0) {
                        $result.DaemonStarted = $true
                        $result.Steps += "Docker daemon started"
                        Write-LogMessage "Docker daemon started successfully" -Level Success
                        break
                    }
                }
                catch {
                    # Continue waiting
                }
                
                Write-LogMessage "Waiting for Docker daemon... ($elapsedSeconds/$maxWaitSeconds seconds)" -Level Debug
            }
            
            if (-not $result.DaemonStarted) {
                Write-LogMessage "Docker daemon did not start within timeout. You may need to start Docker Desktop manually." -Level Warning
                $result.Steps += "Docker daemon start timed out"
            }
        }
        
        # Step 4: Get installed version
        try {
            $dockerVersion = docker --version 2>&1
            if ($dockerVersion -match "Docker version\s+([\d\.]+)") {
                $result.Version = $matches[1]
            }
        }
        catch {
            Write-LogMessage "Could not determine Docker version" -Level Debug
        }
        
        $result.Success = $true
        Write-LogMessage "Docker Desktop installation completed successfully!" -Level Success
        return $result
    }
    catch {
        $result.ErrorMessage = "Docker Desktop installation failed: $_"
        Write-LogMessage $result.ErrorMessage -Level Error
        return $result
    }
}

<#
.SYNOPSIS
    Installs Redis in a WSL2 distribution
    
.DESCRIPTION
    Installs Redis server in the specified WSL2 distribution using apt-get.
    Configures Redis to work with the Windows Redis Service wrapper.
    
.PARAMETER DistributionName
    The WSL distribution to install Redis in. Default is "Ubuntu"
    
.PARAMETER RedisVersion
    Specific Redis version to install. If not specified, installs latest available version.
    
.OUTPUTS
    PSCustomObject with installation status and details
    
.EXAMPLE
    $result = Install-RedisInWSL
    if ($result.Success) {
        Write-Host "Redis installed in WSL2: $($result.InstalledVersion)"
    }
#>
function Install-RedisInWSL {
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory = $false)]
        [string]$DistributionName = "Ubuntu",
        
        [Parameter(Mandatory = $false)]
        [string]$RedisVersion = $null
    )
    
    Write-LogMessage "Installing Redis in WSL2 ($DistributionName)..." -Level Info
    
    $result = [PSCustomObject]@{
        Success = $false
        InstalledVersion = $null
        ErrorMessage = $null
        Steps = @()
    }
    
    try {
        # Verify WSL2 is installed
        $wslStatus = Test-WSL2Installed
        if (-not $wslStatus.IsCompatible -or -not $wslStatus.HasDistribution) {
            $result.ErrorMessage = "WSL2 is not installed or has no distributions"
            Write-LogMessage $result.ErrorMessage -Level Error
            return $result
        }
        
        # Verify the specified distribution exists
        if ($wslStatus.Distributions -notcontains $DistributionName) {
            $result.ErrorMessage = "Distribution '$DistributionName' not found. Available: $($wslStatus.Distributions -join ', ')"
            Write-LogMessage $result.ErrorMessage -Level Error
            return $result
        }
        
        # Step 1: Update package list
        Write-LogMessage "Updating package list..." -Level Info
        try {
            $updateCmd = "sudo apt-get update"
            $updateProcess = Start-Process -FilePath "wsl" -ArgumentList "-d $DistributionName -- $updateCmd" -Wait -PassThru -NoNewWindow
            
            if ($updateProcess.ExitCode -eq 0) {
                $result.Steps += "Package list updated"
                Write-LogMessage "Package list updated" -Level Success
            }
        }
        catch {
            Write-LogMessage "Failed to update package list: $_" -Level Warning
        }
        
        # Step 2: Install Redis
        Write-LogMessage "Installing Redis server..." -Level Info
        try {
            $installCmd = if ($RedisVersion) {
                "sudo apt-get install -y redis-server=$RedisVersion"
            } else {
                "sudo apt-get install -y redis-server"
            }
            
            $installProcess = Start-Process -FilePath "wsl" -ArgumentList "-d $DistributionName -- $installCmd" -Wait -PassThru -NoNewWindow
            
            if ($installProcess.ExitCode -eq 0) {
                $result.Steps += "Redis server installed"
                Write-LogMessage "Redis server installed successfully" -Level Success
            } else {
                $result.ErrorMessage = "Redis installation failed with exit code: $($installProcess.ExitCode)"
                Write-LogMessage $result.ErrorMessage -Level Error
                return $result
            }
        }
        catch {
            $result.ErrorMessage = "Failed to install Redis: $_"
            Write-LogMessage $result.ErrorMessage -Level Error
            return $result
        }
        
        # Step 3: Get installed version
        Write-LogMessage "Verifying Redis installation..." -Level Info
        try {
            $versionCmd = "redis-server --version"
            $versionOutput = wsl -d $DistributionName -- $versionCmd 2>&1
            
            if ($versionOutput -match "v=(\d+\.\d+\.\d+)") {
                $result.InstalledVersion = $matches[1]
                $result.Steps += "Verified Redis version: $($result.InstalledVersion)"
                Write-LogMessage "Redis version: $($result.InstalledVersion)" -Level Success
            }
        }
        catch {
            Write-LogMessage "Could not verify Redis version" -Level Warning
        }
        
        # Step 4: Stop Redis service in WSL (will be managed by Windows Service)
        Write-LogMessage "Disabling Redis service in WSL (will be managed by Windows Service)..." -Level Info
        try {
            $stopCmd = "sudo service redis-server stop"
            wsl -d $DistributionName -- $stopCmd 2>&1 | Out-Null
            
            $disableCmd = "sudo systemctl disable redis-server 2>/dev/null || true"
            wsl -d $DistributionName -- $disableCmd 2>&1 | Out-Null
            
            $result.Steps += "Redis service disabled (will be managed by Windows)"
            Write-LogMessage "Redis service will be managed by Windows Service wrapper" -Level Info
        }
        catch {
            Write-LogMessage "Could not disable Redis service (this is expected on some systems)" -Level Debug
        }
        
        $result.Success = $true
        Write-LogMessage "Redis successfully installed in WSL2!" -Level Success
        return $result
    }
    catch {
        $result.ErrorMessage = "Redis installation in WSL2 failed: $_"
        Write-LogMessage $result.ErrorMessage -Level Error
        return $result
    }
}

#endregion

#region Export Module Members

# Export all functions
Export-ModuleMember -Function @(
    # Helper functions
    'Get-WindowsVersion',
    'Test-IsAdministrator',
    'Write-LogMessage',
    'Test-CommandExists',
    'Download-File',
    # Detection functions
    'Test-WSL2Installed',
    'Test-DockerInstalled',
    'Get-RedisBackend',
    # Installation functions
    'Install-WSL2',
    'Install-DockerDesktop',
    'Install-RedisInWSL'
)

#endregion

# Module initialization
Write-LogMessage "RedisInstaller module loaded (Version 1.0.0)" -Level Info

