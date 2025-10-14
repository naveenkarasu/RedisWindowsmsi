# Redis Configuration Templates

This directory contains configuration templates used by the Redis Windows Installer.

## Files

### 1. `redis.conf`
Standard Redis server configuration file optimized for Windows with WSL2/Docker backend.

**Key Features:**
- Binds to all interfaces (0.0.0.0) for Windows accessibility
- Default port: 6379
- RDB persistence enabled with standard save intervals
- 512MB max memory with LRU eviction policy
- Optimized for WSL2/Docker performance

**Customization:**
- Memory limits: Adjust `maxmemory` based on system resources
- Persistence: Configure RDB/AOF settings as needed
- Security: Enable `requirepass` for authentication
- Logging: Adjust `loglevel` (debug, verbose, notice, warning)

**Deployment:**
- Copied to: `C:\Program Files\Redis\conf\redis.conf`
- Mounted in WSL2: `/etc/redis/redis.conf`
- Mounted in Docker: `/usr/local/etc/redis/redis.conf`

### 2. `backend.json`
Backend configuration for the Redis Windows Service wrapper.

**Structure:**

```json
{
  "backendType": "WSL2" | "Docker",
  "wsl": { ... },
  "docker": { ... },
  "redis": { ... },
  "service": { ... },
  "monitoring": { ... },
  "performance": { ... },
  "advanced": { ... }
}
```

**Configuration Sections:**

#### `backendType`
Specifies which backend to use:
- `"WSL2"` - Use Windows Subsystem for Linux 2
- `"Docker"` - Use Docker Desktop

#### `wsl` Section
WSL2-specific configuration:
- `distribution`: WSL distribution name (e.g., "Ubuntu")
- `redisPath`: Path to redis-server binary in WSL
- `configPath`: Redis configuration file path in WSL
- `dataPath`: Redis data directory in WSL
- `windowsDataPath`: Windows path for data persistence

#### `docker` Section
Docker-specific configuration:
- `imageName`: Docker image to use (e.g., "redis:7.2-alpine")
- `containerName`: Name for the Redis container
- `portMapping`: Port forwarding configuration
- `volumeMappings`: Volume mounts for data/config
- `resourceLimits`: CPU and memory limits

#### `redis` Section
Redis server settings:
- `port`: Redis server port
- `bindAddress`: IP address to bind to
- `maxMemory`: Maximum memory allocation
- `enablePersistence`: Enable/disable data persistence
- `requirePassword`: Enable authentication

#### `service` Section
Windows Service configuration:
- `serviceName`: Windows service name
- `displayName`: Display name in Services
- `startType`: Automatic, Manual, or Disabled
- `failureActions`: Service recovery actions

#### `monitoring` Section
Monitoring and logging settings:
- `enableHealthCheck`: Enable periodic health checks
- `healthCheckInterval`: Interval in seconds
- `enableWindowsEventLog`: Log to Windows Event Log
- `logFilePath`: Service log file location

#### `performance` Section
Performance and reliability settings:
- `enableAutoRestart`: Auto-restart on failure
- `maxRestartAttempts`: Max restart attempts
- `memoryWarningThreshold`: Memory usage warning %
- `enableSlowLogMonitoring`: Monitor slow queries

#### `advanced` Section
Advanced customization:
- `customStartupArgs`: Additional Redis startup arguments
- `environmentVariables`: Environment variables
- `preStartScript`: Script to run before starting
- `postStopScript`: Script to run after stopping

**Deployment:**
- Copied to: `C:\Program Files\Redis\conf\backend.json`
- Read by: Redis Windows Service Wrapper

## Usage During Installation

1. **Initial Setup:**
   - Templates are copied to installation directory
   - `backend.json` is updated with detected backend type
   - Paths are adjusted based on installation location

2. **WSL2 Setup:**
   ```powershell
   # Redis config is mounted from Windows to WSL
   wsl -d Ubuntu -- redis-server /mnt/c/Program\ Files/Redis/conf/redis.conf
   ```

3. **Docker Setup:**
   ```powershell
   # Redis runs in container with volume mounts
   docker run -d --name redis `
     -p 6379:6379 `
     -v "C:\ProgramData\Redis\data:/data" `
     -v "C:\Program Files\Redis\conf\redis.conf:/usr/local/etc/redis/redis.conf" `
     redis:7.2-alpine redis-server /usr/local/etc/redis/redis.conf
   ```

## Modifying Configurations

### Modify Redis Settings

1. Edit `C:\Program Files\Redis\conf\redis.conf`
2. Restart Redis service:
   ```powershell
   Restart-Service Redis
   ```

### Modify Backend Settings

1. Edit `C:\Program Files\Redis\conf\backend.json`
2. Restart Redis service:
   ```powershell
   Restart-Service Redis
   ```

### Common Modifications

**Enable Authentication:**
```conf
# In redis.conf
requirepass your_strong_password
```

**Increase Memory Limit:**
```conf
# In redis.conf
maxmemory 1gb
```

**Enable AOF Persistence:**
```conf
# In redis.conf
appendonly yes
appendfsync everysec
```

**Change Docker Image:**
```json
// In backend.json
"docker": {
  "imageName": "redis:7.2"
}
```

## Validation

### Validate redis.conf
```powershell
# In WSL2
wsl -d Ubuntu -- redis-server /path/to/redis.conf --test-memory 512

# In Docker
docker run --rm -v "C:\Program Files\Redis\conf\redis.conf:/redis.conf" `
  redis:7.2-alpine redis-server /redis.conf --test-memory 512
```

### Validate backend.json
The Windows Service validates the configuration on startup and logs errors to Windows Event Log.

## Troubleshooting

**Issue: Redis won't start**
- Check redis.conf syntax
- Verify paths in backend.json
- Check Windows Event Log for errors

**Issue: Configuration changes not applied**
- Ensure you restarted the Redis service
- Check file permissions
- Verify file encoding (UTF-8 without BOM)

**Issue: Data not persisting**
- Verify `dir` setting in redis.conf
- Check volume mappings in backend.json
- Ensure data directory has write permissions

## References

- [Redis Configuration](https://redis.io/docs/management/config/)
- [Redis Persistence](https://redis.io/docs/management/persistence/)
- [Redis Security](https://redis.io/docs/management/security/)

