using LightQueryProfiler.Shared.Enums;
using LightQueryProfiler.Shared.Models;
using LightQueryProfiler.Shared.Repositories.Interfaces;

namespace LightQueryProfiler.Shared.Factories;

/// <summary>
/// Factory for creating profiler session templates based on database engine type
/// </summary>
public static class ProfilerSessionTemplateFactory
{
    /// <summary>
    /// Creates the appropriate profiler session template for the given database engine type
    /// </summary>
    /// <param name="engineType">The database engine type</param>
    /// <returns>A profiler session template configured for the specified engine type</returns>
    public static BaseProfilerSessionTemplate CreateTemplate(DatabaseEngineType engineType)
    {
        return engineType switch
        {
            DatabaseEngineType.AzureSqlDatabase => new AzureSqlProfilerSessionTemplate(),
            DatabaseEngineType.SqlServer => new DefaultProfilerSessionTemplate(),
            _ => new DefaultProfilerSessionTemplate()
        };
    }
}
