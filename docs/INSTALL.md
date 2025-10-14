# Redis for Windows - Installation Guide

This guide covers installation, configuration, and initial setup of Redis for Windows.

## System Requirements

### Minimum Requirements
- **OS:** Windows 10 version 2004 (Build 19041) or later, Windows 11
- **Processor:** x64 (64-bit) CPU with virtualization support
- **RAM:** 4GB (8GB recommended)
- **Disk Space:** 2GB free space
- **Permissions:** Administrator account

### Prerequisites

The installer will automatically install ONE of the following:

- **WSL2 (Windows Subsystem for Linux 2)** - Preferred option
  - Lighter weight
  - Better Windows integration
  - ~95% native Linux performance
  - May require system restart

- **Docker Desktop** - Fallback option
  - More isolated
  - Familiar to DevOps users
  - Commercial use requires license

## Installation

### Standard Installation

1. **Download the Installer**
   - Get `Redis-Setup.msi` from [Releases](https://github.com/naveenkarasu/RedisWindowsmsi/releases)
   - Verify checksum (recommended)

2. **Run the Installer**
   - Double-click `Redis-Setup.msi`
   - Click "Yes" when prompted for administrator privileges

3. **Follow the Installation Wizard**
   
   **Step 1: Welcome Screen**
   - Click "Next"
   
   **Step 2: License Agreement**
   - Read and accept the license
   - Click "Next"
   
   **Step 3: Installation Location**
   - Default: `C:\Program Files\Redis`
   - Change if needed, then click "Next"
   
   **Step 4: Prerequisites Detection**
   - Installer checks for WSL2 or Docker
   - If neither found, installer will:
     - Download and install WSL2 (preferred)
     - Download Ubuntu distribution
     - **Note:** May require system restart
   
   **Step 5: Redis Configuration**
   - Choose default port (6379) or custom
   - Select memory limit
   - Configure persistence options
   - Click "Next"
   
   **Step 6: Install**
   - Click "Install" to begin
   - Wait for installation to complete
   
   **Step 7: Completion**
   - Check "Start Redis Service" (recommended)
   - Click "Finish"

4. **Post-Installation (if WSL2 was installed)**
   - Restart your computer if prompted
   - Redis service will auto-start on boot

### Silent Installation

For automated deployments:

```powershell
# Install with default settings
msiexec /i Redis-Setup.msi /quiet /qn

# Install with custom location
msiexec /i Redis-Setup.msi /quiet /qn INSTALLFOLDER="D:\Redis"

# Install with logging
msiexec /i Redis-Setup.msi /quiet /qn /l*v install.log
```

### Installation via Package Manager (Future)

```powershell
# Via Chocolatey (planned)
choco install redis-windows

# Via winget (planned)
winget install Redis.RedisWindows
```

## Post-Installation Configuration

### Verify Installation

1. **Check Service Status**
   ```powershell
   Get-Service Redis
   ```
   
   Expected output:
   ```
   Status   Name               DisplayName
   ------   ----               -----------
   Running  Redis              Redis Server
   ```

2. **Test Connection**
   ```powershell
   redis-cli ping
   ```
   
   Expected output: `PONG`

3. **Test Basic Operations**
   ```powershell
   redis-cli set test "Hello Redis"
   redis-cli get test
   ```
   
   Expected output: `"Hello Redis"`

### Configuration Files

**Redis Configuration:** `C:\Program Files\Redis\conf\redis.conf`

Common settings:
```conf
# Port
port 6379

# Bind address (0.0.0.0 = all interfaces)
bind 127.0.0.1

# Max memory (e.g., 256mb, 1gb)
maxmemory 512mb
maxmemory-policy allkeys-lru

# Persistence
save 900 1
save 300 10
save 60 10000

# Logging
loglevel notice
logfile C:\ProgramData\Redis\logs\redis.log

# Data directory
dir C:\ProgramData\Redis\data
```

**Backend Configuration:** `C:\Program Files\Redis\conf\backend.json`

```json
{
  "backendType": "WSL2",
  "wsl": {
    "distribution": "Ubuntu",
    "redisPath": "/usr/bin/redis-server"
  },
  "docker": {
    "imageName": "redis:7.2",
    "containerName": "redis",
    "portMapping": "6379:6379"
  }
}
```

### Apply Configuration Changes

After modifying `redis.conf`:

```powershell
# Restart Redis service
Restart-Service Redis

# Or via redis-cli (graceful)
redis-cli shutdown
Start-Service Redis
```

## Managing Redis

### Using Windows Services

**GUI Method:**
1. Press `Win + R`, type `services.msc`, press Enter
2. Find "Redis Server"
3. Right-click for Start/Stop/Restart options

**PowerShell Method:**
```powershell
# Start Redis
Start-Service Redis

# Stop Redis
Stop-Service Redis

# Restart Redis
Restart-Service Redis

# Check status
Get-Service Redis

# Set startup type
Set-Service Redis -StartupType Automatic
```

### Using Redis CLI

```powershell
# Interactive mode
redis-cli

# Execute single command
redis-cli ping
redis-cli set mykey "myvalue"
redis-cli get mykey

# Connect to remote Redis
redis-cli -h hostname -p 6379

# Authenticated connection
redis-cli -a password

# Database selection
redis-cli -n 1  # Select database 1
```

### Using Redis from Applications

**.NET Example:**
```csharp
using StackExchange.Redis;

var redis = ConnectionMultiplexer.Connect("localhost:6379");
var db = redis.GetDatabase();
db.StringSet("key", "value");
string value = db.StringGet("key");
```

**Python Example:**
```python
import redis

r = redis.Redis(host='localhost', port=6379, db=0)
r.set('key', 'value')
value = r.get('key')
```

**Node.js Example:**
```javascript
const redis = require('redis');
const client = redis.createClient();

await client.connect();
await client.set('key', 'value');
const value = await client.get('key');
```

## Security Configuration

### Enable Authentication

Edit `redis.conf`:
```conf
requirepass your_strong_password_here
```

Restart service and connect:
```powershell
redis-cli -a your_strong_password_here
```

### Firewall Configuration

Redis binds to `127.0.0.1` by default (localhost only). To allow remote connections:

1. Edit `redis.conf`:
   ```conf
   bind 0.0.0.0
   ```

2. Add firewall rule:
   ```powershell
   New-NetFirewallRule -DisplayName "Redis" -Direction Inbound `
       -Protocol TCP -LocalPort 6379 -Action Allow
   ```

**Warning:** Always use authentication for remote connections!

## Data Persistence

### RDB (Snapshot) Persistence

Default settings in `redis.conf`:
```conf
save 900 1      # Save after 900 seconds if at least 1 key changed
save 300 10     # Save after 300 seconds if at least 10 keys changed
save 60 10000   # Save after 60 seconds if at least 10000 keys changed
```

Manual save:
```powershell
redis-cli save      # Blocking save
redis-cli bgsave    # Background save
```

### AOF (Append-Only File) Persistence

Enable in `redis.conf`:
```conf
appendonly yes
appendfilename "appendonly.aof"
appendfsync everysec  # always, everysec, or no
```

### Data Location

Default: `C:\ProgramData\Redis\data\`

Files:
- `dump.rdb` - RDB snapshot
- `appendonly.aof` - AOF log

### Backup Strategy

```powershell
# Stop Redis
Stop-Service Redis

# Backup data
Copy-Item -Path "C:\ProgramData\Redis\data\*" `
          -Destination "D:\Backups\Redis\$(Get-Date -Format 'yyyy-MM-dd')" `
          -Recurse

# Start Redis
Start-Service Redis
```

## Upgrading Redis

1. Download new `Redis-Setup.msi`
2. Run installer (will detect existing installation)
3. Choose "Upgrade" option
4. Installer will:
   - Stop Redis service
   - Backup configuration
   - Install new version
   - Restore configuration
   - Restart service

## Uninstallation

### Standard Uninstall

1. Press `Win + R`, type `appwiz.cpl`, press Enter
2. Find "Redis for Windows"
3. Click "Uninstall"
4. Follow wizard

### Silent Uninstall

```powershell
msiexec /x Redis-Setup.msi /quiet /qn
```

### Complete Removal

Uninstaller removes:
- Program files (`C:\Program Files\Redis`)
- Windows Service registration
- Start menu shortcuts

To remove data (manual):
```powershell
Remove-Item "C:\ProgramData\Redis" -Recurse -Force
```

## Troubleshooting

See [TROUBLESHOOTING.md](TROUBLESHOOTING.md) for detailed troubleshooting guide.

### Quick Checks

1. **Service won't start:**
   ```powershell
   # Check Windows Event Log
   Get-EventLog -LogName Application -Source "Redis Service" -Newest 10
   
   # Check Redis logs
   Get-Content "C:\ProgramData\Redis\logs\redis.log" -Tail 50
   ```

2. **Connection refused:**
   ```powershell
   # Verify service is running
   Get-Service Redis
   
   # Check port binding
   netstat -ano | findstr :6379
   ```

3. **Performance issues:**
   - Check memory usage in Task Manager
   - Review `redis.conf` memory settings
   - Consider increasing `maxmemory`

## Getting Help

- **Documentation:** [docs/](.)
- **Issues:** https://github.com/naveenkarasu/RedisWindowsmsi/issues
- **Discussions:** https://github.com/naveenkarasu/RedisWindowsmsi/discussions
- **Redis Documentation:** https://redis.io/documentation

## Next Steps

- Read [USAGE.md](USAGE.md) for advanced usage scenarios
- Review [TROUBLESHOOTING.md](TROUBLESHOOTING.md) for common issues
- Check [Performance Tuning Guide](PERFORMANCE.md)

