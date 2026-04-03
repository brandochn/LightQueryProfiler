namespace LightQueryProfiler.JsonRpc.Models
{
    /// <summary>
    /// Request model for saving (upserting) a recent connection.
    /// Passwords are accepted in plain-text and encrypted by the repository layer.
    /// </summary>
    public record SaveRecentConnectionRequest
    {
        public required string DataSource { get; init; }
        public required string InitialCatalog { get; init; }
        public string? UserId { get; init; }

        /// <summary>Plain-text password — the repository layer encrypts it before storage.</summary>
        public string? Password { get; init; }
        public bool IntegratedSecurity { get; init; }
        public int? EngineType { get; init; }
        public int? AuthenticationMode { get; init; }

        /// <summary>
        /// Gets the plain-text ADO.NET connection string provided by the user.
        /// </summary>
        /// <remarks>
        /// Only set when <c>AuthenticationMode</c> equals 3 (<c>ConnectionString</c> mode).
        /// The repository layer encrypts this value before storage. Never log this value.
        /// </remarks>
        public string? ConnectionString { get; init; }
    }
}
