using LightQueryProfiler.Shared.Enums;
using LightQueryProfiler.Shared.Services;

namespace LightQueryProfiler.Shared.UnitTests.Services;

/// <summary>
/// Tests for DatabaseEngineDetector service.
/// Note: DatabaseEngineDetector is now only used when AuthenticationMode is NOT AzureSQLDatabase.
/// For AzureSQLDatabase auth mode, the engine type is directly inferred without detection.
/// </summary>
public class DatabaseEngineDetectorTests
{
    [Fact]
    public async Task DetectEngineTypeAsync_WhenDbContextIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var detector = new DatabaseEngineDetector();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await detector.DetectEngineTypeAsync(null!));
    }

    /// <summary>
    /// Tests the IsAzureSqlDatabase helper method.
    /// This is used to determine if special Azure SQL Database queries should be used.
    /// </summary>
    [Theory]
    [InlineData(DatabaseEngineType.AzureSqlDatabase, true)]
    [InlineData(DatabaseEngineType.SqlServer, false)]
    public void IsAzureSqlDatabase_WhenEngineTypeProvided_ReturnsExpectedResult(
        DatabaseEngineType engineType,
        bool expectedResult)
    {
        // Arrange
        var detector = new DatabaseEngineDetector();

        // Act
        var result = detector.IsAzureSqlDatabase(engineType);

        // Assert
        Assert.Equal(expectedResult, result);
    }
}
