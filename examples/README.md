# Redis Service Wrapper Configuration Examples

This directory contains example configurations and tools for the Redis Service Wrapper.

## Files

### Configuration Examples

- **`backend-wsl2.json`** - WSL2 backend configuration for development
- **`backend-docker.json`** - Docker backend configuration for development
- **`backend-production.json`** - Production-ready Docker configuration with high availability
- **`backend-minimal.json`** - Minimal configuration for testing

### Tools

- **`validate-config.ps1`** - PowerShell script to validate configuration files
- **`build-config.ps1`** - PowerShell script to build configuration files using a fluent interface

## Quick Start

### 1. Choose Your Backend

**For Development (WSL2):**
```powershell
# Copy the WSL2 example
Copy-Item "backend-wsl2.json" "C:\ProgramData\Redis\backend.json"
```

**For Production (Docker):**
```powershell
# Copy the Docker example
Copy-Item "backend-docker.json" "C:\ProgramData\Redis\backend.json"
```

### 2. Validate Your Configuration

```powershell
# Validate the configuration file
.\validate-config.ps1 -ConfigPath "C:\ProgramData\Redis\backend.json" -Verbose
```

### 3. Build Custom Configuration

```powershell
# Build WSL2 configuration
.\build-config.ps1 -BackendType WSL2 -OutputPath "C:\ProgramData\Redis\backend.json" -ServiceName "MyRedis" -Port 6380

# Build Docker configuration for production
.\build-config.ps1 -BackendType Docker -OutputPath "C:\ProgramData\Redis\backend.json" -Production -EnableAuth -Password "secure-password"
```

## Configuration Examples

### Development Environment (WSL2)

The `backend-wsl2.json` example is perfect for development environments:

- Uses WSL2 backend for simplicity
- No authentication (easier for development)
- RDB persistence only
- Verbose logging
- Automatic restart on failure

### Development Environment (Docker)

The `backend-docker.json` example uses Docker for development:

- Uses Docker backend for containerization
- Basic authentication setup
- RDB persistence
- Standard monitoring

### Production Environment

The `backend-production.json` example is production-ready:

- Uses Docker backend for isolation
- Strong authentication with environment variables
- Both RDB and AOF persistence
- Comprehensive monitoring and health checks
- Resource limits and performance tuning
- High availability configuration

### Minimal Configuration

The `backend-minimal.json` example is for testing:

- Minimal settings
- Manual start type
- No health checks
- No auto-restart
- Small memory limit

## Tools Usage

### Configuration Validator

The `validate-config.ps1` script validates configuration files and provides detailed feedback:

```powershell
# Basic validation
.\validate-config.ps1 -ConfigPath "backend.json"

# Verbose validation with summary
.\validate-config.ps1 -ConfigPath "backend.json" -Verbose
```

**Features:**
- JSON syntax validation
- Schema validation
- Business logic validation
- Production readiness checks
- Detailed error and warning messages

### Configuration Builder

The `build-config.ps1` script builds configuration files using a fluent interface:

```powershell
# WSL2 configuration
.\build-config.ps1 -BackendType WSL2 -OutputPath "backend.json" -ServiceName "RedisDev" -Port 6379

# Docker configuration
.\build-config.ps1 -BackendType Docker -OutputPath "backend.json" -DockerImage "redis:7-alpine" -ContainerName "redis-dev"

# Production configuration
.\build-config.ps1 -BackendType Docker -OutputPath "backend.json" -Production -EnableAuth -Password "secure-password" -MaxMemory "4gb"
```

**Parameters:**
- `-BackendType`: "WSL2" or "Docker"
- `-OutputPath`: Path to save the configuration file
- `-ServiceName`: Windows service name
- `-DisplayName`: Service display name
- `-Port`: Redis port (default: 6379)
- `-MaxMemory`: Redis memory limit (default: "1gb")
- `-Distribution`: WSL distribution name (WSL2 only)
- `-DockerImage`: Docker image name (Docker only)
- `-ContainerName`: Docker container name (Docker only)
- `-EnableAuth`: Enable Redis authentication
- `-Password`: Redis password
- `-Production`: Use production-ready settings
- `-Verbose`: Show detailed output

## Environment Variables

For production environments, use environment variables for sensitive data:

```powershell
# Set environment variables
$env:REDIS_PASSWORD = "your-secure-password"
$env:REDIS_DOCKER_IMAGE = "redis:7-alpine"

# Build configuration with environment variables
.\build-config.ps1 -BackendType Docker -OutputPath "backend.json" -Production -EnableAuth -Password "REDIS_PASSWORD"
```

## Best Practices

### Security

1. **Use Environment Variables**: Store passwords and sensitive data in environment variables
2. **Enable Authentication**: Always enable Redis authentication in production
3. **Restrict Network Access**: Use appropriate bind addresses
4. **Regular Updates**: Keep Redis and Docker images updated

### Performance

1. **Set Memory Limits**: Always configure Redis MaxMemory
2. **Enable Persistence**: Configure appropriate persistence for your use case
3. **Monitor Performance**: Enable health checks and slow log monitoring
4. **Resource Limits**: Set appropriate Docker resource limits

### Reliability

1. **Enable Auto-Restart**: Configure automatic restart on failure
2. **Health Monitoring**: Enable health checks with appropriate intervals
3. **Logging**: Enable both Windows Event Log and file logging
4. **Backup Strategy**: Implement regular data backups

## Troubleshooting

### Common Issues

1. **Configuration Validation Errors**
   - Use the validation script to identify issues
   - Check schema version compatibility
   - Verify all required fields are present

2. **Service Won't Start**
   - Check configuration file syntax
   - Verify WSL distribution exists (WSL2 backend)
   - Ensure Docker is running (Docker backend)
   - Check Windows Event Log for errors

3. **Redis Connection Issues**
   - Verify port configuration
   - Check bind address settings
   - Ensure firewall allows Redis port
   - Verify authentication settings

### Getting Help

1. Use the configuration validator to identify issues
2. Check the Windows Event Log for detailed error messages
3. Review the service log files
4. Consult the main configuration documentation
5. Check Redis, WSL2, or Docker documentation for backend-specific issues

## Examples in Action

### Scenario 1: Development Setup

```powershell
# Build WSL2 configuration for development
.\build-config.ps1 -BackendType WSL2 -OutputPath "dev-config.json" -ServiceName "RedisDev" -Verbose

# Validate the configuration
.\validate-config.ps1 -ConfigPath "dev-config.json" -Verbose
```

### Scenario 2: Production Setup

```powershell
# Build Docker configuration for production
.\build-config.ps1 -BackendType Docker -OutputPath "prod-config.json" -Production -EnableAuth -Password "REDIS_PASSWORD" -MaxMemory "4gb" -Verbose

# Validate the configuration
.\validate-config.ps1 -ConfigPath "prod-config.json" -Verbose
```

### Scenario 3: Testing Setup

```powershell
# Copy minimal configuration for testing
Copy-Item "backend-minimal.json" "test-config.json"

# Validate the configuration
.\validate-config.ps1 -ConfigPath "test-config.json" -Verbose
```

## Next Steps

1. **Review Examples**: Study the example configurations to understand the options
2. **Use Tools**: Use the validation and builder scripts to create your configuration
3. **Test Configuration**: Validate your configuration before deployment
4. **Deploy**: Install the Redis Service Wrapper with your configuration
5. **Monitor**: Use the built-in monitoring to ensure everything works correctly

For more detailed information, see the main [Configuration Guide](../docs/CONFIGURATION.md).
