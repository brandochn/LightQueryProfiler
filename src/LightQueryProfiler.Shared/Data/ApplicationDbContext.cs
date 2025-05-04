using Microsoft.Data.SqlClient;
using System.Data.Common;

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