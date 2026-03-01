namespace LightQueryProfiler.JsonRpc.Models;

/// <summary>
/// Request parameters for retrieving profiling events
/// </summary>
public record GetEventsRequest
{
    /// <summary>
    /// Name of the profiling session to retrieve events from
    /// </summary>
    public required string SessionName { get; init; }
}
