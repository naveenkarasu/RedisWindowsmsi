using LanguageExt;
using static LanguageExt.Prelude;
using RedisServiceWrapper.Configuration;
using RedisServiceWrapper.Configuration.Loading;
using RedisServiceWrapper.Configuration.Validation;
using RedisServiceWrapper.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using CustomLogger = RedisServiceWrapper.Logging.ILogger;
using LanguageExt.Common;

namespace RedisServiceWrapper.Tests.Configuration;

/// <summary>
/// Unit tests for ConfigurationService using functional programming patterns.
/// Tests cover configuration loading, validation, caching, and change monitoring.
/// </summary>
public class ConfigurationServiceTests : IDisposable
{
    private readonly CustomLogger _logger;
    private readonly string _testConfigPath;
    private readonly ConfigurationService _service;
    private bool _disposed = false;

    public ConfigurationServiceTests()
    {
        _logger = new TestLogger();
        _testConfigPath = Path.Combine(Path.GetTempPath(), $"test-config-{Guid.NewGuid()}.json");
        _service = new ConfigurationService(_logger, _testConfigPath, enableFileWatching: false);
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Arrange & Act
        var service = new ConfigurationService(_logger, _testConfigPath, false);

        // Assert
        Assert.NotNull(service);
        Assert.Equal(_testConfigPath, service.GetConfigurationFilePath());
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ConfigurationService(null!, _testConfigPath));
    }

    [Fact]
    public async Task GetCurrentConfiguration_WhenNoConfigExists_ShouldReturnDefault()
    {
        // Arrange
        // No config file exists

            // Act
            var result = await _service.GetCurrentConfiguration().IfFail(async (Exception ex) => throw new Exception(ex.Message));

            // Assert
            Assert.NotNull(result);
            Assert.Equal(Constants.BackendTypeWSL2, result.BackendType);
    }

    [Fact]
    public async Task LoadConfiguration_WithValidConfig_ShouldLoadSuccessfully()
    {
        // Arrange
        var testConfig = CreateTestConfiguration();
        await File.WriteAllTextAsync(_testConfigPath, SerializeConfiguration(testConfig));

        // Act
        var result = await _service.LoadConfiguration(_testConfigPath).IfFail(async (Exception ex) => throw new Exception(ex.Message));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(testConfig.BackendType, result.BackendType);
        Assert.Equal(testConfig.Redis.Port, result.Redis.Port);
    }

    [Fact]
    public async Task LoadConfiguration_WithInvalidJson_ShouldFail()
    {
        // Arrange
        await File.WriteAllTextAsync(_testConfigPath, "{ invalid json }");

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(async () =>
        {
            await _service.LoadConfiguration(_testConfigPath).IfFail(async (Exception ex) => throw new Exception(ex.Message));
        });
    }

    [Fact]
    public async Task SaveConfiguration_WithValidConfig_ShouldSaveSuccessfully()
    {
        // Arrange
        var testConfig = CreateTestConfiguration();

        // Act
        await _service.SaveConfiguration(testConfig, _testConfigPath).IfFail(async (Exception ex) => throw new Exception(ex.Message));

        // Assert
        Assert.True(File.Exists(_testConfigPath));
        
        var savedContent = await File.ReadAllTextAsync(_testConfigPath);
        Assert.Contains(testConfig.BackendType, savedContent);
    }

    [Fact]
    public async Task SaveConfiguration_WithInvalidConfig_ShouldFail()
    {
        // Arrange
        var invalidConfig = CreateTestConfiguration() with { BackendType = "InvalidBackend" };

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(async () =>
        {
            await _service.SaveConfiguration(invalidConfig, _testConfigPath).IfFail(async (Exception ex) => throw new Exception(ex.Message));
        });
    }

    [Fact]
    public async Task ReloadConfiguration_ShouldBypassCache()
    {
        // Arrange
        var testConfig = CreateTestConfiguration();
        await File.WriteAllTextAsync(_testConfigPath, SerializeConfiguration(testConfig));

        // Load initial configuration
        await _service.LoadConfiguration(_testConfigPath).IfFail(async (Exception ex) => throw new Exception(ex.Message));

        // Modify file
        var modifiedConfig = testConfig with { Redis = testConfig.Redis with { Port = 6380 } };
        await File.WriteAllTextAsync(_testConfigPath, SerializeConfiguration(modifiedConfig));

        // Act
        var result = await _service.ReloadConfiguration(_testConfigPath).IfFail(async (Exception ex) => throw new Exception(ex.Message));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(6380, result.Redis.Port);
    }

    [Fact]
    public void ValidateConfiguration_WithValidConfig_ShouldPass()
    {
        // Arrange
        var testConfig = CreateTestConfiguration();

        // Act
        var report = _service.ValidateConfiguration(testConfig);

        // Assert
        Assert.True(report.Result.IsSuccess);
        Assert.Equal(0, report.ErrorCount);
    }

    [Fact]
    public void ValidateConfiguration_WithInvalidConfig_ShouldFail()
    {
        // Arrange
        var invalidConfig = CreateTestConfiguration() with 
        { 
            BackendType = "InvalidBackend",
            Redis = CreateTestConfiguration().Redis with { Port = -1 }
        };

        // Act
        var report = _service.ValidateConfiguration(invalidConfig);

        // Assert
        Assert.False(report.Result.IsSuccess);
        Assert.True(report.ErrorCount > 0);
    }

    [Fact]
    public void CreateDefaultConfiguration_WithWSL2_ShouldReturnWSL2Config()
    {
        // Act
        var config = _service.CreateDefaultConfiguration(Constants.BackendTypeWSL2);

        // Assert
        Assert.Equal(Constants.BackendTypeWSL2, config.BackendType);
        Assert.NotNull(config.Wsl);
        Assert.Equal(Constants.DefaultWSLDistribution, config.Wsl.Distribution);
    }

    [Fact]
    public void CreateDefaultConfiguration_WithDocker_ShouldReturnDockerConfig()
    {
        // Act
        var config = _service.CreateDefaultConfiguration(Constants.BackendTypeDocker);

        // Assert
        Assert.Equal(Constants.BackendTypeDocker, config.BackendType);
        Assert.NotNull(config.Docker);
        Assert.Equal(Constants.DefaultDockerImage, config.Docker.ImageName);
    }

    [Fact]
    public void CreateConfigurationWithBuilder_ShouldCreateValidConfig()
    {
        // Act
        var config = _service.CreateConfigurationWithBuilder(builder =>
        {
            builder.WithBackendType(Constants.BackendTypeDocker)
                   .WithRedis(r => r.WithPort(6380))
                   .WithService(s => s.WithServiceName("TestService"));
        });

        // Assert
        Assert.Equal(Constants.BackendTypeDocker, config.BackendType);
        Assert.Equal(6380, config.Redis.Port);
        Assert.Equal("TestService", config.Service.ServiceName);
    }

    [Fact]
    public void CreateValidatedConfigurationWithBuilder_WithValidConfig_ShouldReturnRight()
    {
        // Act
        var result = _service.CreateValidatedConfigurationWithBuilder(builder =>
        {
            builder.WithBackendType(Constants.BackendTypeWSL2)
                   .WithRedis(r => r.WithPort(6379));
        });

        // Assert
        Assert.True(result.IsRight);
        var config = result.IfLeft(() => throw new InvalidOperationException());
        Assert.Equal(Constants.BackendTypeWSL2, config.BackendType);
        Assert.Equal(6379, config.Redis.Port);
    }

    [Fact]
    public void CreateValidatedConfigurationWithBuilder_WithInvalidConfig_ShouldReturnLeft()
    {
        // Act
        var result = _service.CreateValidatedConfigurationWithBuilder(builder =>
        {
            builder.WithBackendType("InvalidBackend")
                   .WithRedis(r => r.WithPort(-1));
        });

        // Assert
        Assert.True(result.IsLeft);
        var errors = result.IfRight(() => throw new InvalidOperationException());
        Assert.True(errors.Count > 0);
    }

    [Fact]
    public void CreateConfigurationFromEnvironment_ShouldUseEnvironmentVariables()
    {
        // Arrange
        Environment.SetEnvironmentVariable("REDIS_BACKEND_TYPE", Constants.BackendTypeDocker);
        Environment.SetEnvironmentVariable("REDIS_PORT", "6380");
        Environment.SetEnvironmentVariable("REDIS_REQUIRE_PASSWORD", "true");
        Environment.SetEnvironmentVariable("REDIS_PASSWORD", "testpassword");

        try
        {
            // Act
            var config = _service.CreateConfigurationFromEnvironment();

            // Assert
            Assert.Equal(Constants.BackendTypeDocker, config.BackendType);
            Assert.Equal(6380, config.Redis.Port);
            Assert.True(config.Redis.RequirePassword);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("REDIS_BACKEND_TYPE", null);
            Environment.SetEnvironmentVariable("REDIS_PORT", null);
            Environment.SetEnvironmentVariable("REDIS_REQUIRE_PASSWORD", null);
            Environment.SetEnvironmentVariable("REDIS_PASSWORD", null);
        }
    }

    [Fact]
    public void GetStatistics_ShouldReturnValidStatistics()
    {
        // Act
        var stats = _service.GetStatistics();

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(_testConfigPath, stats.DefaultConfigPath);
    }

    [Fact]
    public void ConfigurationFileExists_WhenFileExists_ShouldReturnTrue()
    {
        // Arrange
        File.WriteAllText(_testConfigPath, "{}");

        // Act
        var exists = _service.ConfigurationFileExists(_testConfigPath);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public void ConfigurationFileExists_WhenFileDoesNotExist_ShouldReturnFalse()
    {
        // Act
        var exists = _service.ConfigurationFileExists(_testConfigPath);

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public void ClearCache_ShouldClearCachedConfiguration()
    {
        // Arrange
        var testConfig = CreateTestConfiguration();
        _service.SaveConfiguration(testConfig, _testConfigPath).IfFail(async (Exception ex) => throw new Exception(ex.Message)).Wait();

        // Act
        _service.ClearCache();

        // Assert
        var stats = _service.GetStatistics();
        Assert.False(stats.HasCachedConfiguration);
    }

    [Fact]
    public void Dispose_ShouldCleanupResources()
    {
        // Arrange
        var service = new ConfigurationService(_logger, _testConfigPath, true);

        // Act
        service.Dispose();

        // Assert
        // Should not throw any exceptions
        Assert.True(true);
    }

    #region Helper Methods

    private ServiceConfiguration CreateTestConfiguration() =>
        new ServiceConfiguration
        {
            BackendType = Constants.BackendTypeWSL2,
            Redis = new RedisConfiguration
            {
                Port = 6379,
                BindAddress = "127.0.0.1",
                RequirePassword = false
            },
            Service = new ServiceSettings
            {
                ServiceName = "TestRedisService",
                DisplayName = "Test Redis Service"
            }
        };

    private string SerializeConfiguration(ServiceConfiguration config)
    {
        return System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    #endregion

    public void Dispose()
    {
        if (!_disposed)
        {
            _service?.Dispose();
            
            if (File.Exists(_testConfigPath))
            {
                File.Delete(_testConfigPath);
            }
            
            _disposed = true;
        }
    }
}

/// <summary>
/// Test logger implementation for unit tests.
/// </summary>
public class TestLogger : CustomLogger
{
    public Unit LogInfo(string message) => unit;
    public Unit LogWarning(string message) => unit;
    public Unit LogError(string message, Exception? exception = null) => unit;
    public Unit LogDebug(string message) => unit;
    public Unit LogSuccess(string message) => unit;
}
