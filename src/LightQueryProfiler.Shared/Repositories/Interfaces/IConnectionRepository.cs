using LightQueryProfiler.Shared.Models;

namespace LightQueryProfiler.Shared.Repositories.Interfaces
{
    /// <summary>
    /// Repository interface for <see cref="Connection"/> entities, extending the generic
    /// repository with a connection-specific upsert operation.
    /// </summary>
    public interface IConnectionRepository : IRepository<Connection>
    {
        /// <summary>
        /// Inserts <paramref name="entity"/> if no row with the same
        /// <c>DataSource</c>, <c>UserId</c>, and <c>InitialCatalog</c> exists;
        /// otherwise updates the existing row (bumping <c>CreationDate</c> to now).
        /// </summary>
        /// <param name="entity">The connection to persist.</param>
        Task UpsertAsync(Connection entity);
    }
}
