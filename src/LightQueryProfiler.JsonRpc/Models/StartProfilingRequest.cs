namespace LightQueryProfiler.JsonRpc.Models;

/// <summary>
/// Request parameters for starting a profiling session
/// </summary>
public record StartProfilingRequest
{
    /// <summary>
    /// Name of the profiling session
    /// </summary>
    public required string SessionName { get; init; }

    /// <summary>
    /// Database engine type (1 = SqlServer, 2 = AzureSqlDatabase)
    /// </summary>
    public required int EngineType { get; init; }

    /// <summary>
    /// SQL Server connection string
    /// </summary>
    public required string ConnectionString { get; init; }
}
