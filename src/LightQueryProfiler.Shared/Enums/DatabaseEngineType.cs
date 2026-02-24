namespace LightQueryProfiler.Shared.Enums;

/// <summary>
/// Represents the type of database engine
/// </summary>
public enum DatabaseEngineType
{
    /// <summary>
    /// SQL Server (on-premises or managed instance)
    /// </summary>
    SqlServer = 1,

    /// <summary>
    /// Azure SQL Database (database-scoped)
    /// </summary>
    AzureSqlDatabase = 2
}
