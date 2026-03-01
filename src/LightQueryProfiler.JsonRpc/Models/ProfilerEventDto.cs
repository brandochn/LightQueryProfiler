namespace LightQueryProfiler.JsonRpc.Models;

/// <summary>
/// Data transfer object for profiler events (JSON-RPC serializable)
/// </summary>
public record ProfilerEventDto
{
    /// <summary>
    /// Event name
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Event timestamp
    /// </summary>
    public string? Timestamp { get; init; }

    /// <summary>
    /// Event fields
    /// </summary>
    public Dictionary<string, object?>? Fields { get; init; }

    /// <summary>
    /// Event actions
    /// </summary>
    public Dictionary<string, object?>? Actions { get; init; }
}
