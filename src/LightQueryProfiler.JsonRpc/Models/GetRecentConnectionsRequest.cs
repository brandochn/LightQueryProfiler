namespace LightQueryProfiler.JsonRpc.Models
{
    /// <summary>
    /// Request model for retrieving all recent connections.
    /// No parameters needed — returns all connections sorted by most recent first.
    /// Kept as a record for consistency with the existing pattern and future extensibility.
    /// </summary>
    public record GetRecentConnectionsRequest
    {
    }
}
