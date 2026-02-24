using LightQueryProfiler.Shared.Enums;
using LightQueryProfiler.Shared.Services;

namespace LightQueryProfiler.Shared.UnitTests.Services;

/// <summary>
/// Tests for DatabaseEngineDetector service.
/// Note: DatabaseEngineDetector is now only used when AuthenticationMode is NOT AzureSQLDatabase.
/// For AzureSQLDatabase auth mode, the engine type is directly inferred without detection.
/// </summary>
[TestFixture]
public class DatabaseEngineDetectorTests
{
    [Test]
    public void DetectEngineTypeAsync_WhenDbContextIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var detector = new DatabaseEngineDetector();

        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await detector.DetectEngineTypeAsync(null!));
    }

    /// <summary>
    /// Tests the IsAzureSqlDatabase helper method.
    /// This is used to determine if special Azure SQL Database queries should be used.
    /// </summary>
    [TestCase(DatabaseEngineType.AzureSqlDatabase, true)]
    [TestCase(DatabaseEngineType.SqlServer, false)]
    public void IsAzureSqlDatabase_WhenEngineTypeProvided_ReturnsExpectedResult(
        DatabaseEngineType engineType,
        bool expectedResult)
    {
        // Arrange
        var detector = new DatabaseEngineDetector();

        // Act
        var result = detector.IsAzureSqlDatabase(engineType);

        // Assert
        Assert.That(result, Is.EqualTo(expectedResult));
    }
}
