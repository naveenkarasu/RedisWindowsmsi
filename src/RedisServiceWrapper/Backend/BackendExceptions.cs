using System;
using System.Runtime.Serialization;
using RedisServiceWrapper.Configuration;

namespace RedisServiceWrapper.Backend;

/// <summary>
/// Base exception for all backend-related errors.
/// </summary>
[Serializable]
public abstract class BackendException : Exception
{
    /// <summary>
    /// Gets the backend type that caused the exception.
    /// </summary>
    public string BackendType { get; }
    
    /// <summary>
    /// Gets the operation that was being performed when the exception occurred.
    /// </summary>
    public string Operation { get; }
    
    /// <summary>
    /// Gets the configuration that was being used when the exception occurred.
    /// </summary>
    public ServiceConfiguration? Configuration { get; }

    protected BackendException(string backendType, string operation, string message, Exception? innerException = null, ServiceConfiguration? configuration = null)
        : base(message, innerException)
    {
        BackendType = backendType;
        Operation = operation;
        Configuration = configuration;
    }

    protected BackendException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        BackendType = info.GetString(nameof(BackendType)) ?? "Unknown";
        Operation = info.GetString(nameof(Operation)) ?? "Unknown";
        Configuration = (ServiceConfiguration?)info.GetValue(nameof(Configuration), typeof(ServiceConfiguration));
    }

    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(BackendType), BackendType);
        info.AddValue(nameof(Operation), Operation);
        info.AddValue(nameof(Configuration), Configuration);
    }
}

/// <summary>
/// Exception thrown when backend initialization fails.
/// </summary>
[Serializable]
public class BackendInitializationException : BackendException
{
    public BackendInitializationException(string backendType, string message, Exception? innerException = null, ServiceConfiguration? configuration = null)
        : base(backendType, "Initialize", message, innerException, configuration)
    {
    }

    protected BackendInitializationException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}

/// <summary>
/// Exception thrown when backend startup fails.
/// </summary>
[Serializable]
public class BackendStartupException : BackendException
{
    /// <summary>
    /// Gets the startup timeout that was configured.
    /// </summary>
    public TimeSpan? StartupTimeout { get; }

    public BackendStartupException(string backendType, string message, TimeSpan? startupTimeout = null, Exception? innerException = null, ServiceConfiguration? configuration = null)
        : base(backendType, "Start", message, innerException, configuration)
    {
        StartupTimeout = startupTimeout;
    }

    protected BackendStartupException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        StartupTimeout = (TimeSpan?)info.GetValue(nameof(StartupTimeout), typeof(TimeSpan?));
    }

    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(StartupTimeout), StartupTimeout);
    }
}

/// <summary>
/// Exception thrown when backend shutdown fails.
/// </summary>
[Serializable]
public class BackendShutdownException : BackendException
{
    /// <summary>
    /// Gets the shutdown timeout that was configured.
    /// </summary>
    public TimeSpan? ShutdownTimeout { get; }

    public BackendShutdownException(string backendType, string message, TimeSpan? shutdownTimeout = null, Exception? innerException = null, ServiceConfiguration? configuration = null)
        : base(backendType, "Stop", message, innerException, configuration)
    {
        ShutdownTimeout = shutdownTimeout;
    }

    protected BackendShutdownException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        ShutdownTimeout = (TimeSpan?)info.GetValue(nameof(ShutdownTimeout), typeof(TimeSpan?));
    }

    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(ShutdownTimeout), ShutdownTimeout);
    }
}

/// <summary>
/// Exception thrown when backend configuration is invalid.
/// </summary>
[Serializable]
public class BackendConfigurationException : BackendException
{
    /// <summary>
    /// Gets the configuration property that caused the error.
    /// </summary>
    public string? PropertyPath { get; }

    public BackendConfigurationException(string backendType, string message, string? propertyPath = null, Exception? innerException = null, ServiceConfiguration? configuration = null)
        : base(backendType, "ValidateConfiguration", message, innerException, configuration)
    {
        PropertyPath = propertyPath;
    }

    protected BackendConfigurationException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        PropertyPath = info.GetString(nameof(PropertyPath));
    }

    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(PropertyPath), PropertyPath);
    }
}

/// <summary>
/// Exception thrown when backend health check fails.
/// </summary>
[Serializable]
public class BackendHealthCheckException : BackendException
{
    /// <summary>
    /// Gets the health check timeout that was configured.
    /// </summary>
    public TimeSpan? HealthCheckTimeout { get; }

    public BackendHealthCheckException(string backendType, string message, TimeSpan? healthCheckTimeout = null, Exception? innerException = null, ServiceConfiguration? configuration = null)
        : base(backendType, "HealthCheck", message, innerException, configuration)
    {
        HealthCheckTimeout = healthCheckTimeout;
    }

    protected BackendHealthCheckException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        HealthCheckTimeout = (TimeSpan?)info.GetValue(nameof(HealthCheckTimeout), typeof(TimeSpan?));
    }

    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(HealthCheckTimeout), HealthCheckTimeout);
    }
}

/// <summary>
/// Exception thrown when backend connection fails.
/// </summary>
[Serializable]
public class BackendConnectionException : BackendException
{
    /// <summary>
    /// Gets the connection string that was being used.
    /// </summary>
    public string? ConnectionString { get; }

    /// <summary>
    /// Gets the connection timeout that was configured.
    /// </summary>
    public TimeSpan? ConnectionTimeout { get; }

    public BackendConnectionException(string backendType, string message, string? connectionString = null, TimeSpan? connectionTimeout = null, Exception? innerException = null, ServiceConfiguration? configuration = null)
        : base(backendType, "Connect", message, innerException, configuration)
    {
        ConnectionString = connectionString;
        ConnectionTimeout = connectionTimeout;
    }

    protected BackendConnectionException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        ConnectionString = info.GetString(nameof(ConnectionString));
        ConnectionTimeout = (TimeSpan?)info.GetValue(nameof(ConnectionTimeout), typeof(TimeSpan?));
    }

    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(ConnectionString), ConnectionString);
        info.AddValue(nameof(ConnectionTimeout), ConnectionTimeout);
    }
}

/// <summary>
/// Exception thrown when backend operation times out.
/// </summary>
[Serializable]
public class BackendTimeoutException : BackendException
{
    /// <summary>
    /// Gets the timeout that was exceeded.
    /// </summary>
    public TimeSpan Timeout { get; }

    public BackendTimeoutException(string backendType, string operation, TimeSpan timeout, string message, Exception? innerException = null, ServiceConfiguration? configuration = null)
        : base(backendType, operation, message, innerException, configuration)
    {
        Timeout = timeout;
    }

    protected BackendTimeoutException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        Timeout = (TimeSpan)info.GetValue(nameof(Timeout), typeof(TimeSpan));
    }

    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(Timeout), Timeout);
    }
}

/// <summary>
/// Exception thrown when backend resource limits are exceeded.
/// </summary>
[Serializable]
public class BackendResourceException : BackendException
{
    /// <summary>
    /// Gets the resource type that was exceeded.
    /// </summary>
    public string ResourceType { get; }

    /// <summary>
    /// Gets the resource limit that was exceeded.
    /// </summary>
    public string ResourceLimit { get; }

    /// <summary>
    /// Gets the current resource usage.
    /// </summary>
    public string CurrentUsage { get; }

    public BackendResourceException(string backendType, string resourceType, string resourceLimit, string currentUsage, string message, Exception? innerException = null, ServiceConfiguration? configuration = null)
        : base(backendType, "ResourceCheck", message, innerException, configuration)
    {
        ResourceType = resourceType;
        ResourceLimit = resourceLimit;
        CurrentUsage = currentUsage;
    }

    protected BackendResourceException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        ResourceType = info.GetString(nameof(ResourceType)) ?? "Unknown";
        ResourceLimit = info.GetString(nameof(ResourceLimit)) ?? "Unknown";
        CurrentUsage = info.GetString(nameof(CurrentUsage)) ?? "Unknown";
    }

    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(ResourceType), ResourceType);
        info.AddValue(nameof(ResourceLimit), ResourceLimit);
        info.AddValue(nameof(CurrentUsage), CurrentUsage);
    }
}

/// <summary>
/// Exception thrown when backend is in an invalid state for the requested operation.
/// </summary>
[Serializable]
public class BackendStateException : BackendException
{
    /// <summary>
    /// Gets the current backend status.
    /// </summary>
    public BackendStatus CurrentStatus { get; }

    /// <summary>
    /// Gets the required backend status for the operation.
    /// </summary>
    public BackendStatus RequiredStatus { get; }

    public BackendStateException(string backendType, string operation, BackendStatus currentStatus, BackendStatus requiredStatus, string message, Exception? innerException = null, ServiceConfiguration? configuration = null)
        : base(backendType, operation, message, innerException, configuration)
    {
        CurrentStatus = currentStatus;
        RequiredStatus = requiredStatus;
    }

    protected BackendStateException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        CurrentStatus = (BackendStatus)info.GetValue(nameof(CurrentStatus), typeof(BackendStatus));
        RequiredStatus = (BackendStatus)info.GetValue(nameof(RequiredStatus), typeof(BackendStatus));
    }

    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(CurrentStatus), CurrentStatus);
        info.AddValue(nameof(RequiredStatus), RequiredStatus);
    }
}
