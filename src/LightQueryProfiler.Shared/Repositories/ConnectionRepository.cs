using LightQueryProfiler.Shared.Enums;
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
            await using var db = _context.GetConnection() as SqliteConnection ?? throw new Exception("db cannot be null or empty");
            await db.OpenAsync();

            // Try with EngineType column first
            try
            {
                const string sqlWithAuthMode = @"INSERT INTO Connections (DataSource, InitialCatalog, UserId, Password, IntegratedSecurity, CreationDate, EngineType, AuthenticationMode)
                                       VALUES (@DataSource, @InitialCatalog, @UserId, @Password, @IntegratedSecurity, @CreationDate, @EngineType, @AuthenticationMode)";

                await using SqliteCommand sqliteCommand = new SqliteCommand(sqlWithAuthMode, db);
                sqliteCommand.Parameters.AddWithValue("@DataSource", entity.DataSource);
                sqliteCommand.Parameters.AddWithValue("@InitialCatalog", entity.InitialCatalog);
                sqliteCommand.Parameters.AddWithValue("@UserId", entity.UserId);
                sqliteCommand.Parameters.AddWithValue("@Password", entity.Password);
                sqliteCommand.Parameters.AddWithValue("@IntegratedSecurity", entity.IntegratedSecurity);
                sqliteCommand.Parameters.AddWithValue("@CreationDate", entity.CreationDate);
                sqliteCommand.Parameters.AddWithValue("@EngineType", entity.EngineType.HasValue ? (int)entity.EngineType.Value : DBNull.Value);
                sqliteCommand.Parameters.AddWithValue("@AuthenticationMode", (int)entity.AuthenticationMode);
                await sqliteCommand.ExecuteNonQueryAsync();
            }
            catch (SqliteException)
            {
                // Fallback for databases without AuthenticationMode column
                try
                {
                    const string sqlWithEngineType = @"INSERT INTO Connections (DataSource, InitialCatalog, UserId, Password, IntegratedSecurity, CreationDate, EngineType)
                                           VALUES (@DataSource, @InitialCatalog, @UserId, @Password, @IntegratedSecurity, @CreationDate, @EngineType)";

                    await using SqliteCommand sqliteCommand = new SqliteCommand(sqlWithEngineType, db);
                    sqliteCommand.Parameters.AddWithValue("@DataSource", entity.DataSource);
                    sqliteCommand.Parameters.AddWithValue("@InitialCatalog", entity.InitialCatalog);
                    sqliteCommand.Parameters.AddWithValue("@UserId", entity.UserId);
                    sqliteCommand.Parameters.AddWithValue("@Password", entity.Password);
                    sqliteCommand.Parameters.AddWithValue("@IntegratedSecurity", entity.IntegratedSecurity);
                    sqliteCommand.Parameters.AddWithValue("@CreationDate", entity.CreationDate);
                    sqliteCommand.Parameters.AddWithValue("@EngineType", entity.EngineType.HasValue ? (int)entity.EngineType.Value : DBNull.Value);
                    await sqliteCommand.ExecuteNonQueryAsync();
                }
                catch (SqliteException)
                {
                    // Fallback for databases without EngineType or AuthenticationMode column
                    const string sqlWithoutEngineType = @"INSERT INTO Connections (DataSource, InitialCatalog, UserId, Password, IntegratedSecurity, CreationDate)
                                           VALUES (@DataSource, @InitialCatalog, @UserId, @Password, @IntegratedSecurity, @CreationDate)";

                    await using SqliteCommand sqliteCommand = new SqliteCommand(sqlWithoutEngineType, db);
                    sqliteCommand.Parameters.AddWithValue("@DataSource", entity.DataSource);
                    sqliteCommand.Parameters.AddWithValue("@InitialCatalog", entity.InitialCatalog);
                    sqliteCommand.Parameters.AddWithValue("@UserId", entity.UserId);
                    sqliteCommand.Parameters.AddWithValue("@Password", entity.Password);
                    sqliteCommand.Parameters.AddWithValue("@IntegratedSecurity", entity.IntegratedSecurity);
                    sqliteCommand.Parameters.AddWithValue("@CreationDate", entity.CreationDate);
                    await sqliteCommand.ExecuteNonQueryAsync();
                }
            }
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
            await using var db = _context.GetConnection() as SqliteConnection ?? throw new Exception("db cannot be null or empty");
            await using SqliteCommand sqliteCommand = new SqliteCommand(sql, db);
            sqliteCommand.Parameters.AddWithValue("@Id", id);

            await db.OpenAsync();
            await sqliteCommand.ExecuteNonQueryAsync();
        }

        public async Task<IList<Connection>> GetAllAsync()
        {
            List<Connection> connections = new List<Connection>();
            await using var db = _context.GetConnection() as SqliteConnection ?? throw new Exception("db cannot be null or empty");
            await db.OpenAsync();

            // Try with EngineType column first
            try
            {
                const string sqlWithAuthMode = "SELECT Id, InitialCatalog, CreationDate, DataSource, IntegratedSecurity, Password, UserId, EngineType, AuthenticationMode FROM Connections";
                await using SqliteCommand sqliteCommand = new SqliteCommand(sqlWithAuthMode, db);
                await using var query = await sqliteCommand.ExecuteReaderAsync();

                int index;
                while (query.Read())
                {
                    index = 0;
                    var engineTypeValue = query.IsDBNull(7) ? null : (DatabaseEngineType?)query.GetInt32(7);
                    var authModeValue = query.IsDBNull(8) ? AuthenticationMode.WindowsAuth : (AuthenticationMode)query.GetInt32(8);
                    connections.Add(new Connection(
                                                   query.GetInt32(index++),
                                                   query.GetString(index++),
                                                   query.GetDateTime(index++),
                                                   query.GetString(index++),
                                                   query.GetBoolean(index++),
                                                   query.GetString(index++),
                                                   query.GetString(index++),
                                                   engineTypeValue,
                                                   authModeValue
                                                   )
    );
                }
            }
            catch (SqliteException)
            {
                // Fallback for databases without AuthenticationMode column
                try
                {
                    const string sqlWithEngineType = "SELECT Id, InitialCatalog, CreationDate, DataSource, IntegratedSecurity, Password, UserId, EngineType FROM Connections";
                    await using SqliteCommand sqliteCommand = new SqliteCommand(sqlWithEngineType, db);
                    await using var query = await sqliteCommand.ExecuteReaderAsync();

                    int index;
                    while (query.Read())
                    {
                        index = 0;
                        var engineTypeValue = query.IsDBNull(7) ? null : (DatabaseEngineType?)query.GetInt32(7);
                        connections.Add(new Connection(
                                                       query.GetInt32(index++),
                                                       query.GetString(index++),
                                                       query.GetDateTime(index++),
                                                       query.GetString(index++),
                                                       query.GetBoolean(index++),
                                                       query.GetString(index++),
                                                       query.GetString(index++),
                                                       engineTypeValue
                                                       )
        );
                    }
                }
                catch (SqliteException)
                {
                    // Fallback for databases without EngineType or AuthenticationMode column
                    const string sqlWithoutEngineType = "SELECT Id, InitialCatalog, CreationDate, DataSource, IntegratedSecurity, Password, UserId FROM Connections";
                    await using SqliteCommand sqliteCommand = new SqliteCommand(sqlWithoutEngineType, db);
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
                                                       query.GetString(index++),
                                                       null
                                                       )
        );
                    }
                }
            }

            return connections;
        }

        public async Task<Connection> GetByIdAsync(int id)
        {
            Connection? connection = null;
            await using var db = _context.GetConnection() as SqliteConnection ?? throw new Exception("db cannot be null or empty");
            await db.OpenAsync();

            // Try with EngineType column first
            try
            {
                const string sqlWithAuthMode = "SELECT Id, InitialCatalog, CreationDate, DataSource, IntegratedSecurity, Password, UserId, EngineType, AuthenticationMode FROM Connections WHERE Id = @Id";
                await using SqliteCommand sqliteCommand = new SqliteCommand(sqlWithAuthMode, db);
                sqliteCommand.Parameters.AddWithValue("@Id", id);
                await using var query = await sqliteCommand.ExecuteReaderAsync();

                int index;
                while (query.Read())
                {
                    index = 0;
                    var engineTypeValue = query.IsDBNull(7) ? null : (DatabaseEngineType?)query.GetInt32(7);
                    var authModeValue = query.IsDBNull(8) ? AuthenticationMode.WindowsAuth : (AuthenticationMode)query.GetInt32(8);
                    connection = new Connection(query.GetInt32(index++),
                                                   query.GetString(index++),
                                                   query.GetDateTime(index++),
                                                   query.GetString(index++),
                                                   query.GetBoolean(index++),
                                                   query.GetString(index++),
                                                   query.GetString(index++),
                                                   engineTypeValue,
                                                   authModeValue);
                }
            }
            catch (SqliteException)
            {
                // Fallback for databases without AuthenticationMode column
                try
                {
                    const string sqlWithEngineType = "SELECT Id, InitialCatalog, CreationDate, DataSource, IntegratedSecurity, Password, UserId, EngineType FROM Connections WHERE Id = @Id";
                    await using SqliteCommand sqliteCommand = new SqliteCommand(sqlWithEngineType, db);
                    sqliteCommand.Parameters.AddWithValue("@Id", id);
                    await using var query = await sqliteCommand.ExecuteReaderAsync();

                    int index;
                    while (query.Read())
                    {
                        index = 0;
                        var engineTypeValue = query.IsDBNull(7) ? null : (DatabaseEngineType?)query.GetInt32(7);
                        connection = new Connection(query.GetInt32(index++),
                                                       query.GetString(index++),
                                                       query.GetDateTime(index++),
                                                       query.GetString(index++),
                                                       query.GetBoolean(index++),
                                                       query.GetString(index++),
                                                       query.GetString(index++),
                                                       engineTypeValue);
                    }
                }
                catch (SqliteException)
                {
                    // Fallback for databases without EngineType or AuthenticationMode column
                    const string sqlWithoutEngineType = "SELECT Id, InitialCatalog, CreationDate, DataSource, IntegratedSecurity, Password, UserId FROM Connections WHERE Id = @Id";
                    await using SqliteCommand sqliteCommand = new SqliteCommand(sqlWithoutEngineType, db);
                    sqliteCommand.Parameters.AddWithValue("@Id", id);
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
                                                       query.GetString(index++),
                                                       null);
                    }
                }
            }

            if (connection == null)
            {
                throw new Exception("Connection cannot be null.");
            }

            return connection;
        }

        public async Task UpdateAsync(Connection entity)
        {
            await using var db = _context.GetConnection() as SqliteConnection ?? throw new Exception("db cannot be null or empty");
            await db.OpenAsync();

            // Try with EngineType column first
            try
            {
                const string sqlWithAuthMode = "UPDATE Connections SET DataSource=@DataSource, InitialCatalog=@InitialCatalog, UserId=@UserId, Password=@Password, IntegratedSecurity=@IntegratedSecurity, EngineType=@EngineType, AuthenticationMode=@AuthenticationMode WHERE Id = @Id";
                await using SqliteCommand sqliteCommand = new SqliteCommand(sqlWithAuthMode, db);
                sqliteCommand.Parameters.AddWithValue("@Id", entity.Id);
                sqliteCommand.Parameters.AddWithValue("@DataSource", entity.DataSource);
                sqliteCommand.Parameters.AddWithValue("@InitialCatalog", entity.InitialCatalog);
                sqliteCommand.Parameters.AddWithValue("@UserId", entity.UserId);
                sqliteCommand.Parameters.AddWithValue("@Password", entity.Password);
                sqliteCommand.Parameters.AddWithValue("@IntegratedSecurity", entity.IntegratedSecurity);
                sqliteCommand.Parameters.AddWithValue("@EngineType", entity.EngineType.HasValue ? (int)entity.EngineType.Value : DBNull.Value);
                sqliteCommand.Parameters.AddWithValue("@AuthenticationMode", (int)entity.AuthenticationMode);
                await sqliteCommand.ExecuteNonQueryAsync();
            }
            catch (SqliteException)
            {
                // Fallback for databases without AuthenticationMode column
                try
                {
                    const string sqlWithEngineType = "UPDATE Connections SET DataSource=@DataSource, InitialCatalog=@InitialCatalog, UserId=@UserId, Password=@Password, IntegratedSecurity=@IntegratedSecurity, EngineType=@EngineType WHERE Id = @Id";
                    await using SqliteCommand sqliteCommand = new SqliteCommand(sqlWithEngineType, db);
                    sqliteCommand.Parameters.AddWithValue("@Id", entity.Id);
                    sqliteCommand.Parameters.AddWithValue("@DataSource", entity.DataSource);
                    sqliteCommand.Parameters.AddWithValue("@InitialCatalog", entity.InitialCatalog);
                    sqliteCommand.Parameters.AddWithValue("@UserId", entity.UserId);
                    sqliteCommand.Parameters.AddWithValue("@Password", entity.Password);
                    sqliteCommand.Parameters.AddWithValue("@IntegratedSecurity", entity.IntegratedSecurity);
                    sqliteCommand.Parameters.AddWithValue("@EngineType", entity.EngineType.HasValue ? (int)entity.EngineType.Value : DBNull.Value);
                    await sqliteCommand.ExecuteNonQueryAsync();
                }
                catch (SqliteException)
                {
                    // Fallback for databases without EngineType or AuthenticationMode column
                    const string sqlWithoutEngineType = "UPDATE Connections SET DataSource=@DataSource, InitialCatalog=@InitialCatalog, UserId=@UserId, Password=@Password, IntegratedSecurity=@IntegratedSecurity WHERE Id = @Id";
                    await using SqliteCommand sqliteCommand = new SqliteCommand(sqlWithoutEngineType, db);
                    sqliteCommand.Parameters.AddWithValue("@Id", entity.Id);
                    sqliteCommand.Parameters.AddWithValue("@DataSource", entity.DataSource);
                    sqliteCommand.Parameters.AddWithValue("@InitialCatalog", entity.InitialCatalog);
                    sqliteCommand.Parameters.AddWithValue("@UserId", entity.UserId);
                    sqliteCommand.Parameters.AddWithValue("@Password", entity.Password);
                    sqliteCommand.Parameters.AddWithValue("@IntegratedSecurity", entity.IntegratedSecurity);
                    await sqliteCommand.ExecuteNonQueryAsync();
                }
            }
        }
    }
}
