# Redis for Windows - Troubleshooting Guide

Common issues and solutions for Redis on Windows.

## Service Issues

### Redis Service Won't Start

**Symptoms:**
- Service status shows "Stopped"
- Error when starting: "The service did not respond to the start or control request in a timely fashion"

**Solutions:**

1. **Check Windows Event Log**
   ```powershell
   Get-EventLog -LogName Application -Source "Redis Service" -Newest 10
   ```

2. **Check Redis Log File**
   ```powershell
   Get-Content "C:\ProgramData\Redis\logs\redis.log" -Tail 50
   ```

3. **Verify WSL2/Docker is Running**
   ```powershell
   # For WSL2
   wsl --status
   
   # For Docker
   docker ps
   ```

4. **Check Service Dependencies**
   ```powershell
   sc.exe qc Redis
   
   # Start dependency services
   Start-Service LxssManager    # For WSL2
   # or
   Start-Service com.docker.service  # For Docker
   ```

5. **Manual Service Start with Logging**
   ```powershell
   # Stop service
   Stop-Service Redis -Force
   
   # Run manually to see errors
   & "C:\Program Files\Redis\bin\RedisServiceWrapper.exe"
   ```

### Service Starts But Redis Unreachable

**Symptoms:**
- Service shows "Running"
- `redis-cli ping` fails with "Connection refused"

**Solutions:**

1. **Verify Redis is Actually Running**
   ```powershell
   # For WSL2
   wsl -d Ubuntu -- ps aux | findstr redis
   
   # For Docker
   docker ps | findstr redis
   ```

2. **Check Port Binding**
   ```powershell
   netstat -ano | findstr :6379
   ```
   
   If nothing shows, Redis didn't start properly.

3. **Check Firewall**
   ```powershell
   # Allow Redis port
   New-NetFirewallRule -DisplayName "Redis" -Direction Inbound `
       -Protocol TCP -LocalPort 6379 -Action Allow
   ```

4. **Verify Configuration**
   ```powershell
   # Check redis.conf
   Get-Content "C:\Program Files\Redis\conf\redis.conf" | Select-String -Pattern "bind|port"
   ```

5. **Test Connection Manually**
   ```powershell
   # For WSL2
   wsl -d Ubuntu -- redis-cli ping
   
   # For Docker
   docker exec -it redis redis-cli ping
   ```

## Connection Issues

### Connection Refused (ERR Connection refused)

**Cause:** Redis server is not running or not listening on the expected port.

**Solutions:**

1. **Verify Service Status**
   ```powershell
   Get-Service Redis
   ```

2. **Check if Port is in Use**
   ```powershell
   netstat -ano | findstr :6379
   ```
   
   If another process is using port 6379:
   - Stop that process
   - Or change Redis port in `redis.conf`

3. **Restart Redis Service**
   ```powershell
   Restart-Service Redis
   ```

### Connection Timeout

**Cause:** Redis is running but not responding quickly enough.

**Solutions:**

1. **Check System Resources**
   ```powershell
   # CPU and memory usage
   Get-Process redis* | Select-Object ProcessName, CPU, WorkingSet
   ```

2. **Check Slow Queries**
   ```powershell
   redis-cli slowlog get 10
   ```

3. **Increase Timeout in Client**
   ```csharp
   // .NET Example
   var options = ConfigurationOptions.Parse("localhost:6379");
   options.ConnectTimeout = 10000; // 10 seconds
   var redis = ConnectionMultiplexer.Connect(options);
   ```

### Authentication Errors (NOAUTH)

**Error:** "NOAUTH Authentication required"

**Cause:** Redis requires password but none was provided.

**Solutions:**

1. **Connect with Password**
   ```powershell
   redis-cli -a your_password
   ```

2. **Check redis.conf**
   ```powershell
   Get-Content "C:\Program Files\Redis\conf\redis.conf" | Select-String -Pattern "requirepass"
   ```

3. **Disable Authentication (Development Only)**
   - Edit `redis.conf`
   - Comment out: `# requirepass your_password`
   - Restart service

## Performance Issues

### High CPU Usage

**Solutions:**

1. **Identify Slow Commands**
   ```powershell
   redis-cli slowlog get 20
   ```

2. **Monitor Active Commands**
   ```powershell
   redis-cli monitor
   ```
   Press Ctrl+C to stop.

3. **Check for KEYS Command**
   - Never use `KEYS *` in production
   - Use `SCAN` instead:
   ```powershell
   redis-cli scan 0 MATCH pattern* COUNT 100
   ```

4. **Optimize Queries**
   - Use pipelining for multiple commands
   - Use appropriate data structures
   - Set expiration times on keys

### High Memory Usage

**Solutions:**

1. **Check Memory Stats**
   ```powershell
   redis-cli info memory
   ```

2. **Find Large Keys**
   ```powershell
   redis-cli --bigkeys
   ```

3. **Set Memory Limit**
   Edit `redis.conf`:
   ```conf
   maxmemory 512mb
   maxmemory-policy allkeys-lru
   ```

4. **Check for Memory Leaks**
   ```powershell
   redis-cli memory doctor
   ```

5. **Enable Key Eviction**
   ```conf
   # In redis.conf
   maxmemory-policy allkeys-lru
   ```

### Slow Response Times

**Solutions:**

1. **Check Latency**
   ```powershell
   redis-cli --latency
   redis-cli --latency-history
   ```

2. **Enable Latency Monitoring**
   ```powershell
   redis-cli config set latency-monitor-threshold 100
   redis-cli latency doctor
   ```

3. **Optimize WSL2 Performance**
   ```powershell
   # Create/edit %USERPROFILE%\.wslconfig
   ```
   
   ```ini
   [wsl2]
   memory=4GB
   processors=2
   ```

4. **Optimize Docker Resources**
   - Open Docker Desktop
   - Settings → Resources
   - Increase CPU/Memory allocation

## WSL2-Specific Issues

### WSL2 Not Installed

**Error:** "WSL 2 requires an update to its kernel component"

**Solution:**

1. **Update WSL Kernel**
   ```powershell
   wsl --update
   ```

2. **Manual Kernel Download**
   - Download from: https://aka.ms/wsl2kernel
   - Install the MSI package
   - Run: `wsl --set-default-version 2`

### WSL2 Distribution Not Found

**Error:** "The specified distribution does not exist"

**Solution:**

1. **List Distributions**
   ```powershell
   wsl --list --verbose
   ```

2. **Install Ubuntu**
   ```powershell
   wsl --install -d Ubuntu
   ```

3. **Update backend.json**
   ```json
   {
     "backendType": "WSL2",
     "wsl": {
       "distribution": "Ubuntu",
       "redisPath": "/usr/bin/redis-server"
     }
   }
   ```

### Redis Not Installed in WSL2

**Error:** "redis-server: command not found" (in WSL)

**Solution:**

1. **Install Redis in WSL2**
   ```powershell
   wsl -d Ubuntu
   ```
   
   Then in WSL:
   ```bash
   sudo apt update
   sudo apt install redis-server -y
   ```

2. **Verify Installation**
   ```bash
   redis-server --version
   exit
   ```

3. **Restart Redis Service (Windows)**
   ```powershell
   Restart-Service Redis
   ```

## Docker-Specific Issues

### Docker Desktop Not Running

**Error:** "Cannot connect to the Docker daemon"

**Solution:**

1. **Start Docker Desktop**
   - Launch Docker Desktop from Start menu
   - Wait for it to fully start (whale icon in system tray)

2. **Verify Docker is Running**
   ```powershell
   docker version
   docker ps
   ```

3. **Restart Redis Service**
   ```powershell
   Restart-Service Redis
   ```

### Redis Container Not Found

**Error:** "No such container: redis"

**Solution:**

1. **List Containers**
   ```powershell
   docker ps -a | findstr redis
   ```

2. **Recreate Container**
   ```powershell
   # Stop service
   Stop-Service Redis
   
   # Remove old container (if exists)
   docker rm -f redis
   
   # Start service (will recreate container)
   Start-Service Redis
   ```

3. **Manual Container Creation**
   ```powershell
   docker run -d --name redis -p 6379:6379 redis:7.2
   ```

### Docker Image Pull Fails

**Error:** "Error response from daemon: Get https://registry-1.docker.io/..."

**Solutions:**

1. **Check Internet Connection**

2. **Use Different Registry Mirror**
   - Docker Desktop → Settings → Docker Engine
   - Add mirror configuration

3. **Pull Image Manually**
   ```powershell
   docker pull redis:7.2
   ```

## Data Persistence Issues

### Data Lost After Restart

**Cause:** Persistence not configured or data directory not mounted.

**Solutions:**

1. **Check Persistence Configuration**
   ```powershell
   Get-Content "C:\Program Files\Redis\conf\redis.conf" | Select-String -Pattern "save|appendonly"
   ```

2. **Enable RDB Snapshots**
   ```conf
   # In redis.conf
   save 900 1
   save 300 10
   save 60 10000
   ```

3. **Enable AOF**
   ```conf
   appendonly yes
   appendfilename "appendonly.aof"
   ```

4. **Verify Data Directory Mount (Docker)**
   ```powershell
   docker inspect redis | findstr -i "mounts -A 10"
   ```

5. **Manual Save**
   ```powershell
   redis-cli save
   ```

### Corrupted Data Files

**Error:** "Bad file format reading the append only file" or "Short read while loading DB"

**Solutions:**

1. **Backup Corrupted Files**
   ```powershell
   Copy-Item "C:\ProgramData\Redis\data\*" -Destination "C:\Backup\Redis" -Recurse
   ```

2. **Check AOF File**
   ```powershell
   # For WSL2
   wsl -d Ubuntu -- redis-check-aof /mnt/c/ProgramData/Redis/data/appendonly.aof
   
   # Fix if needed
   wsl -d Ubuntu -- redis-check-aof --fix /mnt/c/ProgramData/Redis/data/appendonly.aof
   ```

3. **Check RDB File**
   ```powershell
   wsl -d Ubuntu -- redis-check-rdb /mnt/c/ProgramData/Redis/data/dump.rdb
   ```

4. **Start Fresh (Last Resort)**
   ```powershell
   Stop-Service Redis
   Remove-Item "C:\ProgramData\Redis\data\*" -Force
   Start-Service Redis
   ```

## Installation Issues

### Installation Fails

**Error:** "Installation failed with error code 1603"

**Solutions:**

1. **Run as Administrator**
   - Right-click MSI → "Run as administrator"

2. **Check Installation Logs**
   ```powershell
   # Install with logging
   msiexec /i Redis-Setup.msi /l*v install.log
   
   # Review log
   Get-Content install.log | Select-String -Pattern "error|failed"
   ```

3. **Disable Antivirus Temporarily**
   - Some antivirus software blocks MSI installers

4. **Clean Previous Installation**
   - Uninstall any previous Redis installations
   - Delete: `C:\Program Files\Redis`
   - Delete: `C:\ProgramData\Redis`

### WSL2 Installation Requires Restart

**Message:** "You must restart your computer to complete installation"

**Solution:**

1. **Restart Computer**
   ```powershell
   Restart-Computer
   ```

2. **After Restart, Verify WSL2**
   ```powershell
   wsl --status
   ```

3. **Start Redis Service**
   ```powershell
   Start-Service Redis
   ```

## Diagnostic Commands

### Collect System Information

```powershell
# Create diagnostic report
$diagnostics = @{
    "OS Version" = [System.Environment]::OSVersion
    "Redis Service" = (Get-Service Redis).Status
    "WSL Status" = (wsl --status | Out-String)
    "Docker Version" = (docker --version 2>$null)
    "Redis Version" = (redis-cli --version 2>$null)
    "Port 6379" = (netstat -ano | findstr :6379)
    "Redis Logs" = (Get-Content "C:\ProgramData\Redis\logs\redis.log" -Tail 20 -ErrorAction SilentlyContinue)
}

$diagnostics | ConvertTo-Json | Out-File "redis-diagnostics.json"
Write-Host "Diagnostics saved to redis-diagnostics.json"
```

### Test Full Stack

```powershell
# Comprehensive test script
Write-Host "Testing Redis installation..." -ForegroundColor Cyan

# 1. Service status
Write-Host "`n1. Service Status:" -ForegroundColor Yellow
Get-Service Redis

# 2. Connection test
Write-Host "`n2. Connection Test:" -ForegroundColor Yellow
redis-cli ping

# 3. Write test
Write-Host "`n3. Write Test:" -ForegroundColor Yellow
redis-cli set test:key "test-value"

# 4. Read test
Write-Host "`n4. Read Test:" -ForegroundColor Yellow
redis-cli get test:key

# 5. Delete test
Write-Host "`n5. Delete Test:" -ForegroundColor Yellow
redis-cli del test:key

# 6. Performance test
Write-Host "`n6. Performance Test:" -ForegroundColor Yellow
redis-benchmark -t set,get -n 10000 -q

Write-Host "`nAll tests completed!" -ForegroundColor Green
```

## Getting Help

If you're still experiencing issues:

1. **Check GitHub Issues**
   - Search: https://github.com/naveenkarasu/RedisWindowsmsi/issues
   - Create new issue with diagnostic information

2. **Provide Diagnostic Information**
   - Windows version
   - Redis version
   - Backend (WSL2/Docker)
   - Error messages
   - Logs from Event Viewer
   - Redis log file

3. **Community Support**
   - Discussions: https://github.com/naveenkarasu/RedisWindowsmsi/discussions
   - Stack Overflow: Tag with `redis` and `windows`

4. **Official Redis Resources**
   - Redis Documentation: https://redis.io/documentation
   - Redis Support: https://redis.io/support

