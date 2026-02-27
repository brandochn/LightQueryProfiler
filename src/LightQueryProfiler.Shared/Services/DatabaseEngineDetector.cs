using LightQueryProfiler.Shared.Data;
using LightQueryProfiler.Shared.Enums;
using LightQueryProfiler.Shared.Services.Interfaces;
using Microsoft.Data.SqlClient;

namespace LightQueryProfiler.Shared.Services;

/// <summary>
/// Service for detecting the type of database engine from a connection
/// </summary>
public class DatabaseEngineDetector : IDatabaseEngineDetector
{
    /// <summary>
    /// Detects the database engine type by querying server properties
    /// </summary>
    /// <param name="dbContext">The database context to use for detection</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The detected database engine type</returns>
    public async Task<DatabaseEngineType> DetectEngineTypeAsync(IApplicationDbContext dbContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        using var connection = dbContext.GetConnection();
        await connection.OpenAsync(cancellationToken);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT CAST(SERVERPROPERTY('Edition') AS sysname)";

        var edition = (await cmd.ExecuteScalarAsync(cancellationToken))?.ToString();

        if (!string.IsNullOrWhiteSpace(edition) && edition.Contains("SQL Azure", StringComparison.OrdinalIgnoreCase))
        {
            return DatabaseEngineType.AzureSqlDatabase;
        }

        return DatabaseEngineType.SqlServer;
    }

    /// <summary>
    /// Determines if the engine type is Azure SQL Database
    /// </summary>
    /// <param name="engineType">The engine type to check</param>
    /// <returns>True if the engine is Azure SQL Database, false otherwise</returns>
    public bool IsAzureSqlDatabase(DatabaseEngineType engineType)
    {
        return engineType == DatabaseEngineType.AzureSqlDatabase;
    }
}
