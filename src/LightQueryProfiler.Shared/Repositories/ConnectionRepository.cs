using LightQueryProfiler.Shared.Models;
using LightQueryProfiler.Shared.Repositories.Interfaces;
using Microsoft.Data.Sqlite;

namespace LightQueryProfiler.Shared.Repositories
{
    public class ConnectionRepository : IRepository<Connection>
    {
        private readonly IDatabaseContext _context;

        public ConnectionRepository(IDatabaseContext context)
        {
            _context = context;
        }

        public async Task AddAsync(Connection entity)
        {
            const string sql = @"INSERT INTO Connections (DataSource, InitialCatalog, UserId, Password, IntegratedSecurity, CreationDate)
                                   VALUES (@DataSource, @InitialCatalog, @UserId, @Password, @IntegratedSecurity, @CreationDate)";

            await using var db = _context.GetConnection() as SqliteConnection;
            await using SqliteCommand sqliteCommand = new SqliteCommand(sql, db);
            sqliteCommand.Parameters.AddWithValue("@DataSource", entity.DataSource);
            sqliteCommand.Parameters.AddWithValue("@InitialCatalog", entity.InitialCatalog);
            sqliteCommand.Parameters.AddWithValue("@UserId", entity.UserId);
            sqliteCommand.Parameters.AddWithValue("@Password", entity.Password);
            sqliteCommand.Parameters.AddWithValue("@IntegratedSecurity", entity.IntegratedSecurity);
            sqliteCommand.Parameters.AddWithValue("@CreationDate", entity.CreationDate);

            await db.OpenAsync();
            await sqliteCommand.ExecuteNonQueryAsync();
        }

        public async Task<Connection?> Find(Func<Connection, bool> predicate)
        {
            var all = await GetAllAsync();
            if (all?.Count > 0 && predicate != null)
            {
                return all.FirstOrDefault(predicate);
            }

            return null;
        }

        public async Task Delete(int id)
        {
            const string sql = "DELETE FROM Connections WHERE Id = @Id";
            await using var db = _context.GetConnection() as SqliteConnection;
            await using SqliteCommand sqliteCommand = new SqliteCommand(sql, db);
            sqliteCommand.Parameters.AddWithValue("@Id", id);

            await db.OpenAsync();
            await sqliteCommand.ExecuteNonQueryAsync();
        }

        public async Task<IList<Connection>> GetAllAsync()
        {
            const string sql = "SELECT Id, InitialCatalog, CreationDate, DataSource, IntegratedSecurity, Password, UserId FROM Connections";
            List<Connection> connections = new List<Connection>();
            await using var db = _context.GetConnection() as SqliteConnection;
            await using SqliteCommand sqliteCommand = new SqliteCommand(sql, db);

            await db.OpenAsync();
            await using var query = await sqliteCommand.ExecuteReaderAsync();

            int index;
            while (query.Read())
            {
                index = 0;
                connections.Add(new Connection(
                                               query.GetInt32(index++),
                                               query.GetString(index++),
                                               query.GetDateTime(index++),
                                               query.GetString(index++),
                                               query.GetBoolean(index++),
                                               query.GetString(index++),
                                               query.GetString(index++)
                                               )
);
            }

            return connections;
        }

        public async Task<Connection> GetByIdAsync(int id)
        {
            const string sql = "SELECT Id, InitialCatalog, CreationDate, DataSource, IntegratedSecurity, Password, UserId FROM Connections WHERE Id = @Id";
            Connection? connection = null;
            await using var db = _context.GetConnection() as SqliteConnection;
            await using SqliteCommand sqliteCommand = new SqliteCommand(sql, db);
            sqliteCommand.Parameters.AddWithValue("@Id", id);

            await db.OpenAsync();
            await using var query = await sqliteCommand.ExecuteReaderAsync();

            int index;
            while (query.Read())
            {
                index = 0;
                connection = new Connection(query.GetInt32(index++),
                                               query.GetString(index++),
                                               query.GetDateTime(index++),
                                               query.GetString(index++),
                                               query.GetBoolean(index++),
                                               query.GetString(index++),
                                               query.GetString(index++));
            }

            if (connection == null)
            {
                throw new Exception("Connection cannot be null.");
            }

            return connection;
        }

        public async Task UpdateAsync(Connection entity)
        {
            const string sql = "UPDATE Connections SET DataSource=@DataSource, InitialCatalog=@InitialCatalog, UserId=@UserId, Password=@Password, IntegratedSecurity=@IntegratedSecurity WHERE Id = @Id";
            await using var db = _context.GetConnection() as SqliteConnection;
            await using SqliteCommand sqliteCommand = new SqliteCommand(sql, db);
            sqliteCommand.Parameters.AddWithValue("@Id", entity);
            sqliteCommand.Parameters.AddWithValue("@DataSource", entity.DataSource);
            sqliteCommand.Parameters.AddWithValue("@InitialCatalog", entity.InitialCatalog);
            sqliteCommand.Parameters.AddWithValue("@UserId", entity.UserId);
            sqliteCommand.Parameters.AddWithValue("@Password", entity.Password);
            sqliteCommand.Parameters.AddWithValue("@IntegratedSecurity", entity.IntegratedSecurity);
            sqliteCommand.Parameters.AddWithValue("@CreationDate", entity.CreationDate);

            await db.OpenAsync();
            await sqliteCommand.ExecuteNonQueryAsync();
        }
    }
}