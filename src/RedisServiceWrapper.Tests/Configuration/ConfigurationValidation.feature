Feature: Configuration Validation
  As a Redis service administrator
  I want to validate configuration files
  So that I can ensure the service will start correctly

  Background:
    Given I have a configuration service
    And I have a test configuration file

  Scenario: Valid WSL2 configuration should pass validation
    Given I have a valid WSL2 configuration
    When I validate the configuration
    Then the validation should pass
    And there should be no errors
    And there should be no warnings

  Scenario: Valid Docker configuration should pass validation
    Given I have a valid Docker configuration
    When I validate the configuration
    Then the validation should pass
    And there should be no errors
    And there should be no warnings

  Scenario: Invalid backend type should fail validation
    Given I have a configuration with invalid backend type "InvalidBackend"
    When I validate the configuration
    Then the validation should fail
    And there should be at least 1 error
    And the error should contain "Invalid backend type"

  Scenario: Invalid Redis port should fail validation
    Given I have a configuration with Redis port -1
    When I validate the configuration
    When I validate the configuration
    Then the validation should fail
    And there should be at least 1 error
    And the error should contain "Invalid Redis port"

  Scenario: Missing password when required should fail validation
    Given I have a configuration with Redis authentication enabled
    And the password is empty
    When I validate the configuration
    Then the validation should fail
    And there should be at least 1 error
    And the error should contain "Redis password is required"

  Scenario: Configuration with warnings should pass validation with warnings
    Given I have a configuration with debug log level
    When I validate the configuration
    Then the validation should pass
    And there should be at least 1 warning
    And the warning should contain "Debug log level"

  Scenario: Production readiness check should identify issues
    Given I have a configuration without authentication
    And persistence is disabled
    When I validate the configuration
    Then the validation should pass
    And there should be warnings about production readiness
    And the warnings should contain "Authentication disabled"
    And the warnings should contain "Persistence disabled"
