# Redis Service Wrapper Configuration Guide

This document provides comprehensive guidance on configuring the Redis Service Wrapper for Windows.

## Table of Contents

1. [Configuration Overview](#configuration-overview)
2. [Configuration File Structure](#configuration-file-structure)
3. [Backend Types](#backend-types)
4. [Configuration Examples](#configuration-examples)
5. [Environment Variables](#environment-variables)
6. [Validation and Error Handling](#validation-and-error-handling)
7. [Best Practices](#best-practices)
8. [Troubleshooting](#troubleshooting)

## Configuration Overview

The Redis Service Wrapper uses a JSON configuration file (`backend.json`) to define how Redis should be deployed and managed. The configuration supports both WSL2 and Docker backends, with comprehensive monitoring, performance tuning, and service management options.

### Default Configuration Location

- **File Path**: `C:\ProgramData\Redis\backend.json`
- **Schema Version**: `1.0.0`
- **Format**: JSON with validation

## Configuration File Structure

### Root Level Properties

```json
{
  "SchemaVersion": "1.0.0",
  "BackendType": "WSL2|Docker",
  "Wsl": { ... },
  "Docker": { ... },
  "Redis": { ... },
  "Service": { ... },
  "Monitoring": { ... },
  "Performance": { ... },
  "Advanced": { ... },
  "Metadata": { ... }
}
```

### Backend Types

#### WSL2 Backend

The WSL2 backend runs Redis inside a Windows Subsystem for Linux distribution.

**Required Properties:**
- `Distribution`: WSL distribution name (e.g., "Ubuntu-22.04")
- `RedisPath`: Path to Redis server executable in WSL
- `RedisCliPath`: Path to Redis CLI executable in WSL
- `WindowsDataPath`: Windows path for Redis data directory
- `WindowsConfigPath`: Windows path for Redis configuration

**Example:**
```json
{
  "BackendType": "WSL2",
  "Wsl": {
    "Distribution": "Ubuntu-22.04",
    "RedisPath": "/usr/bin/redis-server",
    "RedisCliPath": "/usr/bin/redis-cli",
    "WindowsDataPath": "C:\\ProgramData\\Redis\\data",
    "WindowsConfigPath": "C:\\ProgramData\\Redis\\config"
  }
}
```

#### Docker Backend

The Docker backend runs Redis in a Docker container.

**Required Properties:**
- `ImageName`: Docker image name (e.g., "redis:7-alpine")
- `ContainerName`: Name for the Docker container
- `PortMapping`: Port mapping in format "hostPort:containerPort"
- `VolumeMappings`: Array of volume mappings
- `ResourceLimits`: Memory and CPU limits

**Example:**
```json
{
  "BackendType": "Docker",
  "Docker": {
    "ImageName": "redis:7-alpine",
    "ContainerName": "redis-service",
    "PortMapping": "6379:6379",
    "VolumeMappings": [
      "C:\\ProgramData\\Redis\\data:/data",
      "C:\\ProgramData\\Redis\\config:/usr/local/etc/redis"
    ],
    "ResourceLimits": {
      "Memory": "1g",
      "Cpus": "1.0"
    }
  }
}
```

### Redis Configuration

Controls Redis server settings and behavior.

**Key Properties:**
- `Port`: Redis server port (default: 6379)
- `BindAddress`: IP address to bind to
- `MaxMemory`: Maximum memory usage (e.g., "1gb", "512mb")
- `PersistenceMode`: Persistence strategy ("rdb", "aof", "both", "no")
- `RequirePassword`: Enable authentication
- `Password`: Redis password (use environment variables for security)

**Example:**
```json
{
  "Redis": {
    "Port": 6379,
    "BindAddress": "127.0.0.1",
    "MaxMemory": "1gb",
    "PersistenceMode": "rdb",
    "EnablePersistence": true,
    "RequirePassword": false,
    "Password": "",
    "LogLevel": "notice"
  }
}
```

### Service Configuration

Defines Windows Service behavior and failure handling.

**Key Properties:**
- `ServiceName`: Windows service name
- `DisplayName`: Service display name
- `StartType`: Service start type ("Automatic", "Manual", "Disabled")
- `FailureActions`: Actions to take on service failure

**Example:**
```json
{
  "Service": {
    "ServiceName": "RedisService",
    "DisplayName": "Redis Service Wrapper",
    "StartType": "Automatic",
    "FailureActions": {
      "ResetPeriod": 86400,
      "RestartDelay": 5000,
      "Actions": [
        {
          "Type": "restart",
          "Delay": 1000
        }
      ]
    }
  }
}
```

### Monitoring Configuration

Configures health checks, logging, and monitoring.

**Key Properties:**
- `EnableHealthCheck`: Enable Redis health monitoring
- `HealthCheckInterval`: Health check frequency in seconds
- `EnableWindowsEventLog`: Log to Windows Event Log
- `EnableFileLogging`: Enable file-based logging
- `LogFilePath`: Path to log file

**Example:**
```json
{
  "Monitoring": {
    "EnableHealthCheck": true,
    "HealthCheckInterval": 30,
    "EnableWindowsEventLog": true,
    "EnableFileLogging": true,
    "LogFilePath": "C:\\ProgramData\\Redis\\logs\\service.log",
    "MaxLogSizeMB": 100,
    "MaxLogFiles": 5
  }
}
```

### Performance Configuration

Controls auto-restart behavior and performance monitoring.

**Key Properties:**
- `EnableAutoRestart`: Enable automatic restart on failure
- `MaxRestartAttempts`: Maximum restart attempts
- `MemoryWarningThreshold`: Memory usage warning threshold (%)
- `EnableSlowLogMonitoring`: Monitor slow Redis commands

**Example:**
```json
{
  "Performance": {
    "EnableAutoRestart": true,
    "MaxRestartAttempts": 3,
    "RestartCooldown": 30,
    "MemoryWarningThreshold": 80,
    "MemoryErrorThreshold": 95,
    "EnableSlowLogMonitoring": true,
    "SlowLogThreshold": 10000
  }
}
```

## Configuration Examples

### Development Environment (WSL2)

```json
{
  "SchemaVersion": "1.0.0",
  "BackendType": "WSL2",
  "Wsl": {
    "Distribution": "Ubuntu-22.04",
    "RedisPath": "/usr/bin/redis-server",
    "RedisCliPath": "/usr/bin/redis-cli",
    "WindowsDataPath": "C:\\ProgramData\\Redis\\data",
    "WindowsConfigPath": "C:\\ProgramData\\Redis\\config"
  },
  "Redis": {
    "Port": 6379,
    "BindAddress": "127.0.0.1",
    "MaxMemory": "1gb",
    "PersistenceMode": "rdb",
    "EnablePersistence": true,
    "RequirePassword": false,
    "LogLevel": "notice"
  },
  "Service": {
    "ServiceName": "RedisService",
    "DisplayName": "Redis Service Wrapper",
    "StartType": "Automatic"
  },
  "Monitoring": {
    "EnableHealthCheck": true,
    "EnableWindowsEventLog": true,
    "EnableFileLogging": true
  },
  "Performance": {
    "EnableAutoRestart": true,
    "MaxRestartAttempts": 3
  }
}
```

### Production Environment (Docker)

```json
{
  "SchemaVersion": "1.0.0",
  "BackendType": "Docker",
  "Docker": {
    "ImageName": "redis:7-alpine",
    "ContainerName": "redis-production",
    "PortMapping": "6379:6379",
    "VolumeMappings": [
      "C:\\ProgramData\\Redis\\data:/data",
      "C:\\ProgramData\\Redis\\config:/usr/local/etc/redis"
    ],
    "ResourceLimits": {
      "Memory": "4g",
      "Cpus": "2.0"
    }
  },
  "Redis": {
    "Port": 6379,
    "BindAddress": "0.0.0.0",
    "MaxMemory": "3gb",
    "PersistenceMode": "both",
    "EnablePersistence": true,
    "EnableAOF": true,
    "RequirePassword": true,
    "Password": "REDIS_PASSWORD_ENV_VAR",
    "LogLevel": "warning"
  },
  "Service": {
    "ServiceName": "RedisProduction",
    "DisplayName": "Redis Production Service",
    "StartType": "Automatic"
  },
  "Monitoring": {
    "EnableHealthCheck": true,
    "HealthCheckInterval": 15,
    "EnableWindowsEventLog": true,
    "EnableFileLogging": true,
    "LogLevel": "Warning"
  },
  "Performance": {
    "EnableAutoRestart": true,
    "MaxRestartAttempts": 5,
    "RestartCooldown": 60,
    "MemoryWarningThreshold": 85
  }
}
```

## Environment Variables

The configuration system supports environment variable substitution for sensitive data and dynamic configuration.

### Supported Environment Variables

- `REDIS_PASSWORD`: Redis authentication password
- `REDIS_PORT`: Redis server port
- `REDIS_BACKEND_TYPE`: Backend type (WSL2 or Docker)
- `REDIS_WSL_DISTRIBUTION`: WSL distribution name
- `REDIS_DOCKER_IMAGE`: Docker image name
- `REDIS_SERVICE_NAME`: Windows service name

### Using Environment Variables

In your configuration file, reference environment variables using the format `ENV_VAR_NAME`:

```json
{
  "Redis": {
    "Password": "REDIS_PASSWORD",
    "Port": 6379
  },
  "Docker": {
    "ImageName": "REDIS_DOCKER_IMAGE"
  }
}
```

## Validation and Error Handling

The configuration system includes comprehensive validation to ensure your configuration is correct and production-ready.

### Validation Levels

1. **Schema Validation**: Ensures required fields are present and types are correct
2. **Business Logic Validation**: Validates configuration consistency and best practices
3. **System Validation**: Checks system-level requirements (ports, paths, etc.)

### Common Validation Errors

- **Missing BackendType**: BackendType must be "WSL2" or "Docker"
- **Invalid Port**: Port must be between 1 and 65535
- **Missing Password**: Password required when RequirePassword is true
- **Invalid Memory Format**: MaxMemory must be in format like "1gb", "512mb"
- **Path Issues**: Windows paths must be valid and accessible

### Validation Warnings

- **No Memory Limit**: Redis MaxMemory not configured
- **No Authentication**: Redis authentication disabled
- **No Health Checks**: Health monitoring disabled
- **No Auto-Restart**: Automatic restart disabled

## Best Practices

### Security

1. **Use Environment Variables**: Store sensitive data like passwords in environment variables
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

### Development vs Production

**Development:**
- Use WSL2 backend for simplicity
- Disable authentication for easier testing
- Use RDB persistence only
- Enable verbose logging

**Production:**
- Use Docker backend for isolation
- Enable authentication and use strong passwords
- Use both RDB and AOF persistence
- Enable comprehensive monitoring
- Set appropriate resource limits

## Troubleshooting

### Common Issues

1. **Service Won't Start**
   - Check configuration file syntax
   - Verify WSL distribution exists
   - Ensure Docker is running (for Docker backend)
   - Check Windows Event Log for errors

2. **Redis Connection Issues**
   - Verify port configuration
   - Check bind address settings
   - Ensure firewall allows Redis port
   - Verify authentication settings

3. **Performance Issues**
   - Check memory limits
   - Monitor slow log
   - Verify resource limits (Docker)
   - Check disk space for persistence

4. **Configuration Validation Errors**
   - Use the configuration validator
   - Check schema version compatibility
   - Verify all required fields are present
   - Review validation warnings

### Debugging Tools

1. **Configuration Validator**: Built-in validation with detailed error messages
2. **Windows Event Log**: Service and application logs
3. **File Logging**: Detailed service operation logs
4. **Health Checks**: Redis connectivity and performance monitoring

### Getting Help

1. Check the Windows Event Log for detailed error messages
2. Review the service log files
3. Validate your configuration using the built-in validator
4. Consult the Redis documentation for Redis-specific issues
5. Check WSL2 or Docker documentation for backend-specific issues

## Configuration Migration

When upgrading the Redis Service Wrapper, configuration files may need to be migrated to newer schema versions. The system includes automatic migration support for common changes.

### Migration Process

1. **Backup**: Always backup your current configuration
2. **Validate**: Run configuration validation
3. **Migrate**: Use the built-in migration tools
4. **Test**: Verify the migrated configuration works correctly

### Schema Version History

- **1.0.0**: Initial schema version with WSL2 and Docker support
