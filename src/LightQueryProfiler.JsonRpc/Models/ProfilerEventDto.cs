namespace LightQueryProfiler.JsonRpc.Models;

/// <summary>
/// Data transfer object for profiler events (JSON-RPC serializable).
/// </summary>
/// <remarks>
/// Property names are serialized as camelCase via the <c>CamelCasePropertyNamesContractResolver</c>
/// configured on the <c>JsonMessageFormatter</c> in <c>Program.cs</c>. This ensures the TypeScript
/// client receives <c>name</c>, <c>timestamp</c>, <c>fields</c>, <c>actions</c> to match the
/// <c>ProfilerEvent</c> interface.
/// </remarks>
public record ProfilerEventDto
{
    /// <summary>
    /// Event name (e.g., <c>sql_batch_completed</c>)
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Event timestamp in ISO 8601 format
    /// </summary>
    public string? Timestamp { get; init; }

    /// <summary>
    /// Event fields containing query-specific data (keyed by field name, values are strings)
    /// </summary>
    public Dictionary<string, object?>? Fields { get; init; }

    /// <summary>
    /// Event actions containing session and context data (keyed by action name, values are strings)
    /// </summary>
    public Dictionary<string, object?>? Actions { get; init; }
}
