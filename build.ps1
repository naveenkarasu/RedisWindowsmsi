param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipBuild,
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipInstaller,
    
    [Parameter(Mandatory=$false)]
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Redis Windows MSI Build Script" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Configuration
$RootDir = $PSScriptRoot
$BuildDir = Join-Path $RootDir "build\$Configuration"
$SrcDir = Join-Path $RootDir "src"
$InstallerDir = Join-Path $RootDir "installer"
$OutputMsi = Join-Path $BuildDir "Redis-Setup.msi"

# Function to check prerequisites
function Test-Prerequisites {
    Write-Host "Checking prerequisites..." -ForegroundColor Yellow
    
    # Check .NET SDK
    $dotnetVersion = $null
    try {
        $dotnetVersion = & dotnet --version 2>$null
    } catch {}
    
    if (-not $dotnetVersion) {
        Write-Host "ERROR: .NET SDK not found. Please install .NET 8 SDK." -ForegroundColor Red
        Write-Host "Download from: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] .NET SDK version: $dotnetVersion" -ForegroundColor Green
    
    # Check WiX Toolset
    $wixVersion = $null
    try {
        $wixVersion = & wix --version 2>$null
    } catch {}
    
    if (-not $wixVersion) {
        Write-Host "WARNING: WiX Toolset not found. Installer will not be built." -ForegroundColor Yellow
        Write-Host "  To install WiX: dotnet tool install --global wix" -ForegroundColor Yellow
        return $false
    }
    Write-Host "  [OK] WiX Toolset version: $wixVersion" -ForegroundColor Green
    
    return $true
}

# Function to clean build directory
function Invoke-Clean {
    Write-Host "Cleaning build directory..." -ForegroundColor Yellow
    if (Test-Path $BuildDir) {
        Remove-Item -Path $BuildDir -Recurse -Force
        Write-Host "  [OK] Build directory cleaned" -ForegroundColor Green
    }
}

# Function to build C# projects
function Invoke-BuildProjects {
    Write-Host "Building C# projects..." -ForegroundColor Yellow
    
    # Build RedisServiceWrapper
    $serviceProject = Join-Path $SrcDir "RedisServiceWrapper\RedisServiceWrapper.csproj"
    if (Test-Path $serviceProject) {
        Write-Host "  Building RedisServiceWrapper..." -ForegroundColor Cyan
        & dotnet publish $serviceProject `
            -c $Configuration `
            -r win-x64 `
            --self-contained true `
            -p:PublishSingleFile=true `
            -p:IncludeNativeLibrariesForSelfExtract=true `
            -o "$BuildDir\RedisServiceWrapper"
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "ERROR: Failed to build RedisServiceWrapper" -ForegroundColor Red
            exit 1
        }
        Write-Host "  [OK] RedisServiceWrapper built" -ForegroundColor Green
    } else {
        Write-Host "  [SKIP] RedisServiceWrapper project not found" -ForegroundColor Yellow
    }
    
    # Build WixCustomActions
    $customActionsProject = Join-Path $InstallerDir "CustomActions\WixCustomActions.csproj"
    if (Test-Path $customActionsProject) {
        Write-Host "  Building WixCustomActions..." -ForegroundColor Cyan
        & dotnet build $customActionsProject -c $Configuration -o "$BuildDir\CustomActions"
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "ERROR: Failed to build WixCustomActions" -ForegroundColor Red
            exit 1
        }
        Write-Host "  [OK] WixCustomActions built" -ForegroundColor Green
    } else {
        Write-Host "  [SKIP] WixCustomActions project not found" -ForegroundColor Yellow
    }
}

# Function to copy artifacts
function Copy-Artifacts {
    Write-Host "Copying artifacts..." -ForegroundColor Yellow
    
    # Create output directory structure
    $binDir = Join-Path $BuildDir "bin"
    New-Item -ItemType Directory -Path $binDir -Force | Out-Null
    
    # Copy PowerShell modules
    $psModulesDir = Join-Path $SrcDir "PowerShellModules"
    if (Test-Path $psModulesDir) {
        Copy-Item -Path "$psModulesDir\*" -Destination $binDir -Recurse -Force
        Write-Host "  [OK] PowerShell modules copied" -ForegroundColor Green
    }
    
    # Copy CLI wrappers
    $cliWrapperDir = Join-Path $SrcDir "RedisCliWrapper"
    if (Test-Path $cliWrapperDir) {
        Copy-Item -Path "$cliWrapperDir\*" -Destination $binDir -Recurse -Force
        Write-Host "  [OK] CLI wrappers copied" -ForegroundColor Green
    }
    
    # Copy config templates
    $configTemplatesDir = Join-Path $SrcDir "ConfigTemplates"
    if (Test-Path $configTemplatesDir) {
        $confDir = Join-Path $BuildDir "conf"
        New-Item -ItemType Directory -Path $confDir -Force | Out-Null
        Copy-Item -Path "$configTemplatesDir\*" -Destination $confDir -Recurse -Force
        Write-Host "  [OK] Configuration templates copied" -ForegroundColor Green
    }
}

# Function to build MSI installer
function Invoke-BuildInstaller {
    param([bool]$HasWix)
    
    if (-not $HasWix) {
        Write-Host "Skipping installer build (WiX not available)" -ForegroundColor Yellow
        return
    }
    
    Write-Host "Building MSI installer..." -ForegroundColor Yellow
    
    $productWxs = Join-Path $InstallerDir "Product.wxs"
    if (-not (Test-Path $productWxs)) {
        Write-Host "  [SKIP] Product.wxs not found" -ForegroundColor Yellow
        return
    }
    
    # Build WiX project
    Push-Location $InstallerDir
    try {
        & wix build Product.wxs `
            -arch x64 `
            -d "Configuration=$Configuration" `
            -d "BuildDir=$BuildDir" `
            -out $OutputMsi
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "ERROR: Failed to build MSI installer" -ForegroundColor Red
            exit 1
        }
        
        Write-Host "  [OK] MSI installer built" -ForegroundColor Green
        Write-Host ""
        Write-Host "  Output: $OutputMsi" -ForegroundColor Cyan
    } finally {
        Pop-Location
    }
}

# Main execution
try {
    $hasWix = Test-Prerequisites
    
    if ($Clean) {
        Invoke-Clean
    }
    
    # Create build directory
    if (-not (Test-Path $BuildDir)) {
        New-Item -ItemType Directory -Path $BuildDir -Force | Out-Null
    }
    
    if (-not $SkipBuild) {
        Invoke-BuildProjects
        Copy-Artifacts
    }
    
    if (-not $SkipInstaller) {
        Invoke-BuildInstaller -HasWix $hasWix
    }
    
    Write-Host ""
    Write-Host "=====================================" -ForegroundColor Green
    Write-Host "Build completed successfully!" -ForegroundColor Green
    Write-Host "=====================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Configuration: $Configuration" -ForegroundColor Cyan
    Write-Host "Build Directory: $BuildDir" -ForegroundColor Cyan
    if ($hasWix -and -not $SkipInstaller -and (Test-Path $OutputMsi)) {
        Write-Host "MSI Installer: $OutputMsi" -ForegroundColor Cyan
    }
    Write-Host ""
    
} catch {
    Write-Host ""
    Write-Host "=====================================" -ForegroundColor Red
    Write-Host "Build failed!" -ForegroundColor Red
    Write-Host "=====================================" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
    exit 1
}

