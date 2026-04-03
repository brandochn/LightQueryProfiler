namespace LightQueryProfiler.JsonRpc.Models
{
    /// <summary>
    /// Data transfer object representing a recent connection entry.
    /// Passwords are returned in plain-text — decryption has already been applied by the repository.
    /// </summary>
    public record RecentConnectionDto
    {
        public required int Id { get; init; }
        public required string DataSource { get; init; }
        public required string InitialCatalog { get; init; }
        public string? UserId { get; init; }

        /// <summary>Plain-text password — decrypted by the repository layer before mapping.</summary>
        public string? Password { get; init; }
        public bool IntegratedSecurity { get; init; }
        public int? EngineType { get; init; }
        public int? AuthenticationMode { get; init; }

        /// <summary>
        /// Gets the plain-text ADO.NET connection string, decrypted by the repository layer before mapping.
        /// </summary>
        /// <remarks>
        /// Only populated when <c>AuthenticationMode</c> equals 3 (<c>ConnectionString</c> mode).
        /// Never log this value — it may contain credentials.
        /// </remarks>
        public string? ConnectionString { get; init; }
    }
}
