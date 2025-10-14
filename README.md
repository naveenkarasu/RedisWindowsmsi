# Redis for Windows - MSI Installer

A seamless Windows MSI installer for Redis that leverages WSL2 or Docker Desktop to run Redis natively on Linux while providing a Windows-native experience.

## Overview

This project provides a production-ready Windows installer for Redis that:

- **Auto-detects and installs prerequisites** - WSL2 (preferred) or Docker Desktop (fallback)
- **Runs as a Windows Service** - Start/stop Redis like any Windows service
- **Provides transparent CLI tools** - Use `redis-cli` and other tools directly from PowerShell/CMD
- **Ensures data persistence** - Proper volume mounting and configuration
- **Supports latest Redis versions** - Currently targeting Redis 7.2+

## Why This Approach?

Redis is designed for Linux and doesn't have official Windows support for modern versions (7.0+). Instead of attempting a native Windows port (which has proven unstable), this installer:

- Uses WSL2 or Docker to run the official Linux Redis binary
- Wraps it with Windows Service management
- Provides a familiar Windows installation experience

**Benefits:**
- Get the official, stable Linux Redis
- Near-native performance with WSL2 (~95% of Linux performance)
- Easy installation and management for Windows users
- No dependency on unofficial/outdated Windows ports

## Prerequisites

### System Requirements
- Windows 10 version 2004+ (Build 19041+) or Windows 11
- x64 processor with virtualization support
- At least 4GB RAM (8GB recommended)
- Administrator privileges

### Software Requirements (Auto-installed by MSI)
The installer will automatically install ONE of the following:
- **WSL2** (preferred) - Lighter, better integrated
- **Docker Desktop** (fallback) - More isolated

## Installation

### Quick Start

1. Download `Redis-Setup.msi` from [Releases](https://github.com/naveenkarasu/RedisWindowsmsi/releases)
2. Run the installer (requires admin rights)
3. The installer will:
   - Detect if WSL2/Docker is installed
   - Install WSL2 if neither is present (may require restart)
   - Install Redis and configure it as a Windows Service
   - Set up CLI tools

4. After installation, Redis runs automatically as a service

### Manual Build

See [BUILD.md](docs/BUILD.md) for instructions on building the installer yourself.

## Usage

### Using Redis CLI

After installation, you can use Redis CLI directly from any terminal:

```powershell
# Connect to Redis
redis-cli

# Test connection
redis-cli ping

# Set and get values
redis-cli set mykey "Hello Redis"
redis-cli get mykey
```

### Managing the Redis Service

```powershell
# Check service status
Get-Service Redis

# Stop Redis
Stop-Service Redis

# Start Redis
Start-Service Redis

# Restart Redis
Restart-Service Redis
```

Or use the Windows Services GUI (`services.msc`).

### Configuration

Redis configuration file: `C:\Program Files\Redis\conf\redis.conf`

After modifying configuration:
```powershell
Restart-Service Redis
```

### Data and Logs

- **Data Directory:** `C:\ProgramData\Redis\data\`
- **Log File:** `C:\ProgramData\Redis\logs\redis.log`
- **Configuration:** `C:\Program Files\Redis\conf\`

## Architecture

```
┌─────────────────────────────────────┐
│   Windows User / Applications       │
│  (localhost:6379 connections)       │
└──────────────┬──────────────────────┘
               │
┌──────────────▼──────────────────────┐
│   Redis Windows Service Wrapper     │
│   (C# .NET 8 Service)                │
└──────────────┬──────────────────────┘
               │
       ┌───────┴───────┐
       │               │
┌──────▼──────┐ ┌─────▼──────┐
│    WSL2     │ │   Docker   │
│  (Ubuntu)   │ │  Container │
└──────┬──────┘ └─────┬──────┘
       │               │
       └───────┬───────┘
               │
        ┌──────▼──────┐
        │    Redis    │
        │ (Official)  │
        └─────────────┘
```

## Project Structure

```
redis-windows-msi/
├── src/
│   ├── RedisServiceWrapper/        # C# Windows Service
│   ├── RedisCliWrapper/            # CLI wrapper tools
│   ├── PowerShellModules/          # Detection & installation scripts
│   └── ConfigTemplates/            # Redis configuration templates
├── installer/
│   ├── Product.wxs                 # WiX installer definition
│   ├── CustomActions/              # WiX custom actions (C#)
│   └── resources/                  # Icons, licenses, banners
├── build/                          # Build output
├── tests/                          # Test scripts
└── docs/                           # Documentation
```

## Development

### Building from Source

Requirements:
- Visual Studio 2022 with C++ and .NET workloads
- .NET 8 SDK
- WiX Toolset v4
- PowerShell 7+

```powershell
# Clone the repository
git clone https://github.com/naveenkarasu/RedisWindowsmsi.git
cd RedisWindowsmsi

# Build
.\build.ps1 -Configuration Release

# Output: build\Release\Redis-Setup.msi
```

### Testing

See [docs/TESTING.md](docs/TESTING.md) for testing procedures.

## Troubleshooting

See [TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md) for common issues and solutions.

### Quick Checks

1. **Service won't start:**
   - Check Event Viewer (Application logs)
   - Verify WSL2/Docker is running
   - Check logs: `C:\ProgramData\Redis\logs\redis.log`

2. **Connection refused:**
   - Verify service is running: `Get-Service Redis`
   - Check firewall settings
   - Verify port 6379 is not in use by another application

3. **Performance issues:**
   - WSL2: Ensure virtualization is enabled in BIOS
   - Docker: Increase Docker Desktop resource allocation

## Known Limitations

1. **WSL2 Installation** - May require a system restart
2. **Docker Desktop License** - Commercial use requires a license
3. **Performance** - Slight overhead compared to native Linux (~5%)
4. **Cluster Mode** - Limited support in beta version (coming in v2.0)

## Contributing

Contributions are welcome! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## License

MIT License - See [LICENSE](LICENSE) for details.

## Disclaimer

**This is an unofficial community project.** Redis is a registered trademark of Redis Ltd. This project is not affiliated with, endorsed by, or supported by Redis Ltd.

Use in production at your own risk. While this installer uses the official Redis binaries, the Windows Service wrapper and installation process are community-maintained.

## Roadmap

- [x] Beta v1.0 - Windows Service wrapper with WSL2/Docker support
- [ ] v1.5 - Transparent CLI tools (native .exe wrappers)
- [ ] v2.0 - Full Windows integration with connection pooling
- [ ] v2.5 - GUI management tool
- [ ] v3.0 - Redis Cluster and Sentinel support

## Support

- **Issues:** [GitHub Issues](https://github.com/naveenkarasu/RedisWindowsmsi/issues)
- **Discussions:** [GitHub Discussions](https://github.com/naveenkarasu/RedisWindowsmsi/discussions)
- **Documentation:** [Wiki](https://github.com/naveenkarasu/RedisWindowsmsi/wiki)

## Acknowledgments

- Redis team for the amazing in-memory database
- Microsoft WSL2 team for making Linux on Windows seamless
- WiX Toolset for the installer framework
- Community contributors

---

**Star this project** if you find it useful! ⭐

