namespace LightQueryProfiler.JsonRpc.Models;

/// <summary>
/// Request parameters for stopping a profiling session
/// </summary>
public record StopProfilingRequest
{
    /// <summary>
    /// Name of the profiling session to stop
    /// </summary>
    public required string SessionName { get; init; }
}
