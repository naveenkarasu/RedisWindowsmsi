# Building Redis Windows MSI Installer

This guide explains how to build the Redis Windows MSI installer from source.

## Prerequisites

### Required Software

1. **Visual Studio 2022** (or later)
   - Workload: "Desktop development with C++"
   - Workload: ".NET desktop development"
   - Component: ".NET 8.0 Runtime"

2. **.NET 8 SDK**
   - Download: https://dotnet.microsoft.com/download/dotnet/8.0
   - Verify installation: `dotnet --version`

3. **WiX Toolset v4**
   ```powershell
   dotnet tool install --global wix
   ```
   - Verify installation: `wix --version`

4. **PowerShell 7+** (recommended)
   - Download: https://github.com/PowerShell/PowerShell/releases
   - Windows PowerShell 5.1 should also work

5. **Git for Windows**
   - Download: https://git-scm.com/download/win

### System Requirements

- Windows 10 version 2004+ or Windows 11
- 8GB RAM (minimum 4GB)
- 10GB free disk space
- Administrator privileges (for testing)

## Building the Project

### Quick Build

1. Clone the repository:
   ```powershell
   git clone https://github.com/naveenkarasu/RedisWindowsmsi.git
   cd RedisWindowsmsi
   ```

2. Run the build script:
   ```powershell
   .\build.ps1
   ```

3. Find the MSI installer:
   ```
   build\Release\Redis-Setup.msi
   ```

### Build Options

The `build.ps1` script supports several parameters:

```powershell
# Build in Debug mode
.\build.ps1 -Configuration Debug

# Clean build
.\build.ps1 -Clean

# Build only C# projects (skip installer)
.\build.ps1 -SkipInstaller

# Build only installer (skip C# projects - requires previous build)
.\build.ps1 -SkipBuild
```

### Manual Build Steps

If you prefer to build manually:

#### 1. Build C# Windows Service

```powershell
cd src\RedisServiceWrapper
dotnet publish RedisServiceWrapper.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -o ..\..\build\Release\RedisServiceWrapper
```

#### 2. Build WiX Custom Actions

```powershell
cd installer\CustomActions
dotnet build WixCustomActions.csproj `
    -c Release `
    -o ..\..\build\Release\CustomActions
```

#### 3. Build MSI Installer

```powershell
cd installer
wix build Product.wxs `
    -arch x64 `
    -d "Configuration=Release" `
    -d "BuildDir=..\build\Release" `
    -out ..\build\Release\Redis-Setup.msi
```

## Project Structure

```
redis-windows-msi/
├── src/
│   ├── RedisServiceWrapper/        # Windows Service (C# .NET 8)
│   │   ├── RedisService.cs         # Main service implementation
│   │   ├── Program.cs              # Entry point
│   │   └── RedisServiceWrapper.csproj
│   │
│   ├── RedisCliWrapper/            # CLI wrapper scripts
│   │   ├── redis-cli.ps1
│   │   ├── redis-server.ps1
│   │   └── redis-benchmark.ps1
│   │
│   ├── PowerShellModules/          # Detection & installation
│   │   └── RedisInstaller.psm1
│   │
│   └── ConfigTemplates/            # Configuration files
│       ├── redis.conf
│       └── backend.json
│
├── installer/
│   ├── Product.wxs                 # WiX installer definition
│   ├── CustomActions/              # WiX custom actions (C#)
│   │   ├── DetectBackend.cs
│   │   ├── InstallPrerequisites.cs
│   │   └── WixCustomActions.csproj
│   └── resources/                  # Icons, licenses, etc.
│
├── build/                          # Build output (gitignored)
│   ├── Debug/
│   └── Release/
│
├── tests/                          # Test scripts
├── docs/                           # Documentation
└── tools/                          # Build tools
```

## Building Individual Components

### RedisServiceWrapper (C# Windows Service)

This is the core Windows Service that manages Redis:

```powershell
cd src\RedisServiceWrapper
dotnet build
```

**Key Features:**
- Manages Redis lifecycle (start/stop/restart)
- Monitors Redis health
- Logs to Windows Event Log
- Supports both WSL2 and Docker backends

### WixCustomActions (C# Custom Actions)

Custom actions for the installer:

```powershell
cd installer\CustomActions
dotnet build
```

**Custom Actions:**
- `DetectBackend` - Detects WSL2/Docker installation
- `InstallPrerequisites` - Installs WSL2 or Docker
- `ConfigureRedis` - Generates configuration files
- `CreateRedisInstance` - Sets up initial Redis instance

## Troubleshooting Build Issues

### .NET SDK Not Found

**Error:** `dotnet: The term 'dotnet' is not recognized...`

**Solution:**
1. Install .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0
2. Restart your terminal
3. Verify: `dotnet --version`

### WiX Not Found

**Error:** `wix: The term 'wix' is not recognized...`

**Solution:**
```powershell
dotnet tool install --global wix
```

If that fails, try:
```powershell
dotnet tool update --global wix
```

### Build Fails with "Project Not Found"

**Error:** `MSBUILD : error MSB1009: Project file does not exist.`

**Solution:**
1. Ensure you're running from the repository root
2. Check that project files exist:
   - `src\RedisServiceWrapper\RedisServiceWrapper.csproj`
   - `installer\CustomActions\WixCustomActions.csproj`

### MSI Build Fails

**Common Issues:**

1. **Missing dependencies**
   - Ensure all C# projects are built first
   - Check that build output exists in `build\Release\`

2. **Invalid WiX syntax**
   - Validate XML in `Product.wxs`
   - Check for missing file references

3. **Permission denied**
   - Run PowerShell as Administrator
   - Close Visual Studio (locks DLL files)

## Development Workflow

### Setting Up Development Environment

1. Open `RedisWindowsInstaller.sln` in Visual Studio 2022
2. Set build configuration to Debug
3. Build solution (Ctrl+Shift+B)

### Testing Changes

1. Build in Debug mode:
   ```powershell
   .\build.ps1 -Configuration Debug
   ```

2. Install MSI on test VM or local machine
3. Check Windows Event Viewer for service logs
4. Test Redis connectivity:
   ```powershell
   redis-cli ping
   ```

### Debugging the Windows Service

1. Build in Debug mode
2. Install service manually:
   ```powershell
   sc.exe create Redis binPath= "C:\path\to\RedisServiceWrapper.exe"
   ```
3. Attach Visual Studio debugger to `RedisServiceWrapper.exe`

## Continuous Integration

GitHub Actions workflow (`.github/workflows/build.yml`):

```yaml
name: Build MSI

on: [push, pull_request]

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
      
      - name: Install WiX
        run: dotnet tool install --global wix
      
      - name: Build
        run: .\build.ps1 -Configuration Release
      
      - name: Upload MSI
        uses: actions/upload-artifact@v3
        with:
          name: Redis-Setup
          path: build\Release\Redis-Setup.msi
```

## Next Steps

After building successfully:

1. Read [INSTALL.md](INSTALL.md) for installation instructions
2. Review [TESTING.md](TESTING.md) for testing procedures
3. Check [CONTRIBUTING.md](../CONTRIBUTING.md) for contribution guidelines

## Getting Help

- **Issues:** https://github.com/naveenkarasu/RedisWindowsmsi/issues
- **Discussions:** https://github.com/naveenkarasu/RedisWindowsmsi/discussions
- **Wiki:** https://github.com/naveenkarasu/RedisWindowsmsi/wiki

