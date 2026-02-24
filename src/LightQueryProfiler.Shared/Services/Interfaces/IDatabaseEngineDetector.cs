using LightQueryProfiler.Shared.Data;
using LightQueryProfiler.Shared.Enums;

namespace LightQueryProfiler.Shared.Services.Interfaces;

/// <summary>
/// Detects the type of database engine from a connection
/// </summary>
public interface IDatabaseEngineDetector
{
    /// <summary>
    /// Detects the database engine type by querying server properties
    /// </summary>
    /// <param name="dbContext">The database context to use for detection</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The detected database engine type</returns>
    Task<DatabaseEngineType> DetectEngineTypeAsync(IApplicationDbContext dbContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines if the engine type is Azure SQL Database
    /// </summary>
    /// <param name="engineType">The engine type to check</param>
    /// <returns>True if the engine is Azure SQL Database, false otherwise</returns>
    bool IsAzureSqlDatabase(DatabaseEngineType engineType);
}
