namespace LightQueryProfiler.JsonRpc.Models;

/// <summary>
/// Request model for deleting a connection by its unique identifier.
/// </summary>
public record DeleteConnectionRequest
{
    /// <summary>Gets the unique identifier of the connection to delete.</summary>
    public required int Id { get; init; }
}
