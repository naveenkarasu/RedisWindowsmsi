using LanguageExt;
using static LanguageExt.Prelude;
using RedisServiceWrapper.Configuration;
using RedisServiceWrapper.Configuration.Validation;
using RedisServiceWrapper.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using TechTalk.SpecFlow;
using FluentAssertions;
using CustomLogger = RedisServiceWrapper.Logging.ILogger;

namespace RedisServiceWrapper.Tests.Configuration;

[Binding]
public class ConfigurationValidationSteps
{
    private readonly ScenarioContext _scenarioContext;
    private readonly CustomLogger _logger;
    private ConfigurationService? _service;
    private ServiceConfiguration? _configuration;
    private ValidationReport? _validationReport;
    private string? _testConfigPath;

    public ConfigurationValidationSteps(ScenarioContext scenarioContext)
    {
        _scenarioContext = scenarioContext;
        _logger = new TestLogger();
    }

    [Given(@"I have a configuration service")]
    public void GivenIHaveAConfigurationService()
    {
        _testConfigPath = Path.Combine(Path.GetTempPath(), $"test-config-{Guid.NewGuid()}.json");
        _service = new ConfigurationService(_logger, _testConfigPath, enableFileWatching: false);
    }

    [Given(@"I have a test configuration file")]
    public void GivenIHaveATestConfigurationFile()
    {
        // Configuration file path is already set in the service
        _testConfigPath.Should().NotBeNull();
    }

    [Given(@"I have a valid WSL2 configuration")]
    public void GivenIHaveAValidWSL2Configuration()
    {
        _configuration = new ServiceConfiguration
        {
            BackendType = Constants.BackendTypeWSL2,
            Wsl = new WslConfiguration
            {
                Distribution = Constants.DefaultWSLDistribution,
                RedisPath = "/usr/bin/redis-server",
                RedisCliPath = "/usr/bin/redis-cli"
            },
            Redis = new RedisConfiguration
            {
                Port = 6379,
                BindAddress = "127.0.0.1",
                RequirePassword = false,
                EnablePersistence = true
            },
            Service = new ServiceSettings
            {
                ServiceName = "TestRedisService",
                DisplayName = "Test Redis Service",
                StartType = "Automatic"
            },
            Monitoring = new MonitoringConfiguration
            {
                EnableHealthCheck = true,
                LogLevel = "Info"
            },
            Performance = new PerformanceConfiguration
            {
                EnableAutoRestart = true,
                MaxRestartAttempts = 3
            }
        };
    }

    [Given(@"I have a valid Docker configuration")]
    public void GivenIHaveAValidDockerConfiguration()
    {
        _configuration = new ServiceConfiguration
        {
            BackendType = Constants.BackendTypeDocker,
            Docker = new DockerConfiguration
            {
                ImageName = Constants.DefaultDockerImage,
                ContainerName = "test-redis-container",
                PortMapping = "6379:6379"
            },
            Redis = new RedisConfiguration
            {
                Port = 6379,
                BindAddress = "127.0.0.1",
                RequirePassword = false,
                EnablePersistence = true
            },
            Service = new ServiceSettings
            {
                ServiceName = "TestRedisService",
                DisplayName = "Test Redis Service",
                StartType = "Automatic"
            },
            Monitoring = new MonitoringConfiguration
            {
                EnableHealthCheck = true,
                LogLevel = "Info"
            },
            Performance = new PerformanceConfiguration
            {
                EnableAutoRestart = true,
                MaxRestartAttempts = 3
            }
        };
    }

    [Given(@"I have a configuration with invalid backend type ""(.*)""")]
    public void GivenIHaveAConfigurationWithInvalidBackendType(string backendType)
    {
        _configuration = new ServiceConfiguration
        {
            BackendType = backendType,
            Redis = new RedisConfiguration
            {
                Port = 6379,
                BindAddress = "127.0.0.1"
            }
        };
    }

    [Given(@"I have a configuration with Redis port (.*)")]
    public void GivenIHaveAConfigurationWithRedisPort(int port)
    {
        _configuration = new ServiceConfiguration
        {
            BackendType = Constants.BackendTypeWSL2,
            Redis = new RedisConfiguration
            {
                Port = port,
                BindAddress = "127.0.0.1"
            }
        };
    }

    [Given(@"I have a configuration with Redis authentication enabled")]
    public void GivenIHaveAConfigurationWithRedisAuthenticationEnabled()
    {
        _configuration = new ServiceConfiguration
        {
            BackendType = Constants.BackendTypeWSL2,
            Redis = new RedisConfiguration
            {
                Port = 6379,
                BindAddress = "127.0.0.1",
                RequirePassword = true,
                Password = "" // Empty password
            }
        };
    }

    [Given(@"the password is empty")]
    public void GivenThePasswordIsEmpty()
    {
        _configuration = _configuration! with
        {
            Redis = _configuration.Redis with { Password = "" }
        };
    }

    [Given(@"I have a configuration with debug log level")]
    public void GivenIHaveAConfigurationWithDebugLogLevel()
    {
        _configuration = new ServiceConfiguration
        {
            BackendType = Constants.BackendTypeWSL2,
            Redis = new RedisConfiguration
            {
                Port = 6379,
                BindAddress = "127.0.0.1"
            },
            Monitoring = new MonitoringConfiguration
            {
                LogLevel = "Debug"
            }
        };
    }

    [Given(@"I have a configuration without authentication")]
    public void GivenIHaveAConfigurationWithoutAuthentication()
    {
        _configuration = new ServiceConfiguration
        {
            BackendType = Constants.BackendTypeWSL2,
            Redis = new RedisConfiguration
            {
                Port = 6379,
                BindAddress = "127.0.0.1",
                RequirePassword = false
            }
        };
    }

    [Given(@"persistence is disabled")]
    public void GivenPersistenceIsDisabled()
    {
        _configuration = _configuration! with
        {
            Redis = _configuration.Redis with { EnablePersistence = false }
        };
    }

    [When(@"I validate the configuration")]
    public void WhenIValidateTheConfiguration()
    {
        _configuration.Should().NotBeNull("configuration should be set");
        _service.Should().NotBeNull("service should be set");

        _validationReport = _service!.ValidateConfiguration(_configuration!);
    }

    [Then(@"the validation should pass")]
    public void ThenTheValidationShouldPass()
    {
        _validationReport.Should().NotBeNull("validation report should be generated");
        _validationReport!.Result.IsSuccess.Should().BeTrue("validation should pass");
    }

    [Then(@"the validation should fail")]
    public void ThenTheValidationShouldFail()
    {
        _validationReport.Should().NotBeNull("validation report should be generated");
        _validationReport!.Result.IsSuccess.Should().BeFalse("validation should fail");
    }

    [Then(@"there should be no errors")]
    public void ThenThereShouldBeNoErrors()
    {
        _validationReport.Should().NotBeNull("validation report should be generated");
        _validationReport!.ErrorCount.Should().Be(0, "there should be no errors");
    }

    [Then(@"there should be at least (.*) error")]
    public void ThenThereShouldBeAtLeastError(int errorCount)
    {
        _validationReport.Should().NotBeNull("validation report should be generated");
        _validationReport!.ErrorCount.Should().BeGreaterOrEqualTo(errorCount, $"there should be at least {errorCount} error(s)");
    }

    [Then(@"there should be no warnings")]
    public void ThenThereShouldBeNoWarnings()
    {
        _validationReport.Should().NotBeNull("validation report should be generated");
        _validationReport!.WarningCount.Should().Be(0, "there should be no warnings");
    }

    [Then(@"there should be at least (.*) warning")]
    public void ThenThereShouldBeAtLeastWarning(int warningCount)
    {
        _validationReport.Should().NotBeNull("validation report should be generated");
        _validationReport!.WarningCount.Should().BeGreaterOrEqualTo(warningCount, $"there should be at least {warningCount} warning(s)");
    }

    [Then(@"the error should contain ""(.*)""")]
    public void ThenTheErrorShouldContain(string expectedText)
    {
        _validationReport.Should().NotBeNull("validation report should be generated");
        _validationReport!.Result.Errors.Should().Contain(error => 
            error.Message.Contains(expectedText, StringComparison.OrdinalIgnoreCase),
            $"error should contain '{expectedText}'");
    }

    [Then(@"the warning should contain ""(.*)""")]
    public void ThenTheWarningShouldContain(string expectedText)
    {
        _validationReport.Should().NotBeNull("validation report should be generated");
        _validationReport!.Result.Warnings.Should().Contain(warning => 
            warning.Message.Contains(expectedText, StringComparison.OrdinalIgnoreCase),
            $"warning should contain '{expectedText}'");
    }

    [Then(@"there should be warnings about production readiness")]
    public void ThenThereShouldBeWarningsAboutProductionReadiness()
    {
        _validationReport.Should().NotBeNull("validation report should be generated");
        _validationReport!.WarningCount.Should().BeGreaterThan(0, "there should be production readiness warnings");
    }

    [Then(@"the warnings should contain ""(.*)""")]
    public void ThenTheWarningsShouldContain(string expectedText)
    {
        _validationReport.Should().NotBeNull("validation report should be generated");
        _validationReport!.Result.Warnings.Should().Contain(warning => 
            warning.Message.Contains(expectedText, StringComparison.OrdinalIgnoreCase),
            $"warnings should contain '{expectedText}'");
    }

    [AfterScenario]
    public void AfterScenario()
    {
        _service?.Dispose();
        
        if (!string.IsNullOrEmpty(_testConfigPath) && File.Exists(_testConfigPath))
        {
            File.Delete(_testConfigPath);
        }
    }
}
