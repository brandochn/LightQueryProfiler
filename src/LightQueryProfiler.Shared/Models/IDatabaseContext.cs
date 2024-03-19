using System.Data;

namespace LightQueryProfiler.Shared.Models
{
    public interface IDatabaseContext
    {
        IDbConnection GetConnection();
    }
}