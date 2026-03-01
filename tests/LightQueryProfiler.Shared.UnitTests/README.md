# LightQueryProfiler.Shared.UnitTests

This project contains unit and integration tests for the LightQueryProfiler.Shared library.

## Test Structure

```
LightQueryProfiler.Shared.UnitTests/
├── Enums/                    # Tests for enumerations
├── Factories/                # Tests for factory classes
├── Models/                   # Tests for data models
├── Services/                 # Tests for service classes
├── TestFiles/                # Test data files
└── docs/                     # Documentation
```

## Running Tests

### All Tests (Local Development)

```bash
dotnet test
```

### Unit Tests Only (CI/CD)

```bash
dotnet test --filter "Category!=Integration"
```

### Integration Tests Only

```bash
dotnet test --filter "Category=Integration"
```

## Test Categories

### Unit Tests (66 tests)
- No external dependencies required
- Run automatically in CI/CD pipeline
- Fast execution

### Integration Tests (3 tests)
- Require local SQL Server instance
- Only run locally by developers
- Marked with `[Trait("Category", "Integration")]`
- Skipped in GitHub Actions CI/CD

## Integration Test Requirements

Integration tests require:
- SQL Server running on `localhost`
- Windows Authentication enabled
- Permissions to create Extended Events sessions
- Access to `master` database

For more details, see [Integration Tests Documentation](docs/INTEGRATION_TESTS.md)

## Test Framework

- **Framework**: xUnit v3
- **Mocking**: Moq
- **Target Framework**: .NET 10.0

## CI/CD Integration

The GitHub Actions workflow automatically excludes integration tests using:
```bash
dotnet test --no-build --verbosity normal --filter "Category!=Integration"
```

This ensures that CI builds don't fail due to missing SQL Server instances in the build environment.

## Best Practices

1. **Keep tests independent**: Each test should be able to run in isolation
2. **Use meaningful names**: Follow the pattern `MethodName_WhenCondition_ExpectedResult`
3. **Arrange-Act-Assert**: Structure tests using the AAA pattern
4. **Mark integration tests**: Use `[Trait("Category", "Integration")]` for tests requiring external resources
5. **Avoid test interdependencies**: Tests should not rely on execution order

## Adding New Tests

When adding new tests:

1. Place tests in the appropriate folder based on the class being tested
2. Mirror the source code structure
3. Use `[Fact]` for single test cases
4. Use `[Theory]` with `[InlineData]` for parameterized tests
5. Add `[Trait("Category", "Integration")]` if the test requires external resources

## Code Coverage

To generate code coverage locally:

```bash
dotnet tool install -g dotnet-coverage
dotnet-coverage collect -f cobertura -o coverage.cobertura.xml dotnet test
```
