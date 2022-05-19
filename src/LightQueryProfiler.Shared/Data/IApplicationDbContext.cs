using System.Data.Common;

namespace LightQueryProfiler.Shared.Data
{
    public interface IApplicationDbContext
    {
        DbConnection GetConnection();
    }
}