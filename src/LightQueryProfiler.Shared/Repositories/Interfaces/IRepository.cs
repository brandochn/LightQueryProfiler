using System.Linq.Expressions;

namespace LightQueryProfiler.Shared.Repositories.Interfaces
{
    public interface IRepository<T>
    {
        Task<T> GetByIdAsync(int id);

        Task<IList<T>> GetAllAsync();

        Task AddAsync(T entity);

        Task UpdateAsync(T entity);

        Task Delete(int id);

        Task<T?> Find(Func<T, bool> predicate);
    }
}
