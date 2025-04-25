using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace LightQueryProfiler.Shared.Data
{
    public class ApplicationDbContext : IApplicationDbContext
    {
        private readonly string _connectionString;

        public ApplicationDbContext(string connectionString)
        {
            _connectionString = connectionString;
        }

        public DbConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }
    }
}