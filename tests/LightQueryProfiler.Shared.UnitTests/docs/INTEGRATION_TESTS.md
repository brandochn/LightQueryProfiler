# Integration Tests Documentation

## Overview

This project contains both unit tests and integration tests. Integration tests require a local SQL Server instance and are marked with the `[Trait("Category", "Integration")]` attribute.

## Test Categories

### Unit Tests
- **Location**: All test files in `LightQueryProfiler.Shared.UnitTests`
- **Requirements**: No external dependencies
- **Execution**: Run automatically in CI/CD pipelines

### Integration Tests
- **Location**: `Services/ProfilerServiceUnitTests.cs`
- **Requirements**: Local SQL Server instance running on `localhost`
- **Execution**: Only run locally by developers

## Running Tests

### Running All Tests (Local Development)

To run all tests including integration tests:

```bash
dotnet test
```

### Running Only Unit Tests (CI/CD)

To run only unit tests (excluding integration tests):

```bash
dotnet test --filter "Category!=Integration"
```

This is the default behavior in the GitHub Actions CI/CD pipeline.

### Running Only Integration Tests

To run only integration tests:

```bash
dotnet test --filter "Category=Integration"
```

## Integration Test Requirements

The integration tests in `ProfilerServiceUnitTests.cs` require:

1. **SQL Server**: A local SQL Server instance accessible at `localhost`
2. **Connection String**: `Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True;`
3. **Permissions**: The Windows user running the tests must have permissions to:
   - Create and manage Extended Events sessions
   - Access the `master` database

## CI/CD Behavior

The GitHub Actions workflow (`.github/workflows/build-ci.yml`) automatically excludes integration tests using the filter `--filter "Category!=Integration"`. This prevents CI builds from failing due to missing SQL Server instances in the GitHub-hosted runners.

## Adding New Integration Tests

When adding new tests that require database connectivity or other external resources:

1. Add the `[Trait("Category", "Integration")]` attribute to the test method
2. Document any specific requirements in this file
3. Ensure the test can be skipped without breaking the CI/CD pipeline

### Example

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task MyNewTest_RequiresDatabase()
{
    // Test implementation that requires SQL Server
}
```

## Troubleshooting

### Integration Tests Fail Locally

If integration tests fail on your local machine:

1. Verify SQL Server is running: `sqlcmd -S localhost -E -Q "SELECT @@VERSION"`
2. Check Windows Authentication is enabled
3. Verify your user has necessary permissions
4. Check the connection string in `ProfilerServiceUnitTests.cs`

### Tests Run Slowly

Integration tests are slower than unit tests because they:
- Connect to a real database
- Create and manage Extended Events sessions
- Process real event data

This is expected behavior.

## Best Practices

1. **Keep integration tests separate**: Don't mix database operations in unit tests
2. **Use traits consistently**: Always use `[Trait("Category", "Integration")]` for tests requiring external resources
3. **Document dependencies**: Update this file when adding new integration test requirements
4. **Local testing**: Run integration tests locally before committing changes to database-related code