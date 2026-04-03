using LightQueryProfiler.Shared.Enums;
using LightQueryProfiler.Shared.Models;
using LightQueryProfiler.Shared.Repositories.Interfaces;
using LightQueryProfiler.Shared.Services.Interfaces;
using Microsoft.Data.Sqlite;

namespace LightQueryProfiler.Shared.Repositories
{
    public class ConnectionRepository : IConnectionRepository
    {
        private readonly IDatabaseContext _context;
        private readonly IPasswordProtectionService? _passwordProtectionService;

        /// <summary>
        /// Initializes the repository without password protection (plain-text storage).
        /// Provided for backward compatibility with existing tests and non-Windows environments.
        /// </summary>
        /// <param name="context">The database context used to obtain connections.</param>
        public ConnectionRepository(IDatabaseContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
        }

        /// <summary>
        /// Initializes the repository with password protection.
        /// Passwords are encrypted before storage and decrypted after retrieval.
        /// </summary>
        /// <param name="context">The database context used to obtain connections.</param>
        /// <param name="passwordProtectionService">
        /// Service that encrypts passwords on save and decrypts them on load.
        /// Pass <c>null</c> to disable encryption (plain-text behaviour).
        /// </param>
        public ConnectionRepository(IDatabaseContext context, IPasswordProtectionService? passwordProtectionService)
        {
            ArgumentNullException.ThrowIfNull(context);
            _context = context;
            _passwordProtectionService = passwordProtectionService;
        }

        /// <summary>
        /// Returns the encrypted representation of <paramref name="plainPassword"/> ready for persistence.
        /// When no service is configured the original value is returned unchanged.
        /// </summary>
        private string? EncryptPassword(string? plainPassword)
            => _passwordProtectionService?.Encrypt(plainPassword) ?? plainPassword;

        /// <summary>
        /// Returns the decrypted plain-text password from the value stored in the database.
        /// When no service is configured the original value is returned unchanged.
        /// </summary>
        private string? DecryptPassword(string? storedPassword)
            => _passwordProtectionService?.Decrypt(storedPassword) ?? storedPassword;

        private string? EncryptConnectionString(string? plainConnectionString)
            => _passwordProtectionService?.Encrypt(plainConnectionString) ?? plainConnectionString;

        private string? DecryptConnectionString(string? storedConnectionString)
            => _passwordProtectionService?.Decrypt(storedConnectionString) ?? storedConnectionString;

        public async Task AddAsync(Connection entity)
        {
            await using var db = _context.GetConnection() as SqliteConnection ?? throw new Exception("db cannot be null or empty");
            await db.OpenAsync();

            string? encryptedPassword = EncryptPassword(entity.Password);

            // Try with ConnectionString column first
            try
            {
                const string sqlWithConnString = @"INSERT INTO Connections (DataSource, InitialCatalog, UserId, Password, IntegratedSecurity, CreationDate, EngineType, AuthenticationMode, ConnectionString)
                                       VALUES (@DataSource, @InitialCatalog, @UserId, @Password, @IntegratedSecurity, @CreationDate, @EngineType, @AuthenticationMode, @ConnectionString)";

                await using SqliteCommand sqliteCommand = new SqliteCommand(sqlWithConnString, db);
                sqliteCommand.Parameters.AddWithValue("@DataSource", entity.DataSource);
                sqliteCommand.Parameters.AddWithValue("@InitialCatalog", entity.InitialCatalog);
                sqliteCommand.Parameters.AddWithValue("@UserId", (object?)entity.UserId ?? DBNull.Value);
                sqliteCommand.Parameters.AddWithValue("@Password", (object?)encryptedPassword ?? DBNull.Value);
                sqliteCommand.Parameters.AddWithValue("@IntegratedSecurity", entity.IntegratedSecurity);
                sqliteCommand.Parameters.AddWithValue("@CreationDate", entity.CreationDate);
                sqliteCommand.Parameters.AddWithValue("@EngineType", entity.EngineType.HasValue ? (int)entity.EngineType.Value : DBNull.Value);
                sqliteCommand.Parameters.AddWithValue("@AuthenticationMode", (int)entity.AuthenticationMode);
                sqliteCommand.Parameters.AddWithValue("@ConnectionString", (object?)EncryptConnectionString(entity.ConnectionString) ?? DBNull.Value);
                await sqliteCommand.ExecuteNonQueryAsync();
            }
            catch (SqliteException)
            {
                // Fallback for databases without ConnectionString column
                try
                {
                    const string sqlWithAuthMode = @"INSERT INTO Connections (DataSource, InitialCatalog, UserId, Password, IntegratedSecurity, CreationDate, EngineType, AuthenticationMode)
                                           VALUES (@DataSource, @InitialCatalog, @UserId, @Password, @IntegratedSecurity, @CreationDate, @EngineType, @AuthenticationMode)";

                    await using SqliteCommand sqliteCommand = new SqliteCommand(sqlWithAuthMode, db);
                    sqliteCommand.Parameters.AddWithValue("@DataSource", entity.DataSource);
                    sqliteCommand.Parameters.AddWithValue("@InitialCatalog", entity.InitialCatalog);
                    sqliteCommand.Parameters.AddWithValue("@UserId", (object?)entity.UserId ?? DBNull.Value);
                    sqliteCommand.Parameters.AddWithValue("@Password", (object?)encryptedPassword ?? DBNull.Value);
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
                        sqliteCommand.Parameters.AddWithValue("@UserId", (object?)entity.UserId ?? DBNull.Value);
                        sqliteCommand.Parameters.AddWithValue("@Password", (object?)encryptedPassword ?? DBNull.Value);
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
                        sqliteCommand.Parameters.AddWithValue("@UserId", (object?)entity.UserId ?? DBNull.Value);
                        sqliteCommand.Parameters.AddWithValue("@Password", (object?)encryptedPassword ?? DBNull.Value);
                        sqliteCommand.Parameters.AddWithValue("@IntegratedSecurity", entity.IntegratedSecurity);
                        sqliteCommand.Parameters.AddWithValue("@CreationDate", entity.CreationDate);
                        await sqliteCommand.ExecuteNonQueryAsync();
                    }
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

        public async Task UpsertAsync(Connection entity)
        {
            ArgumentNullException.ThrowIfNull(entity);

            // Normalise empty UserId to null so that "" and null are treated as the
            // same key — preventing duplicate rows when Windows-Auth sessions send
            // an empty string instead of null.
            var normalizedUserId = string.IsNullOrEmpty(entity.UserId) ? null : entity.UserId;

            var existing = await Find(f =>
                string.Equals(f.DataSource, entity.DataSource, StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    string.IsNullOrEmpty(f.UserId) ? null : f.UserId,
                    normalizedUserId,
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(f.InitialCatalog, entity.InitialCatalog, StringComparison.OrdinalIgnoreCase));

            if (existing == null)
            {
                await AddAsync(entity);
            }
            else
            {
                // Connection is immutable — reconstruct with the existing Id
                var updated = new Connection(
                    existing.Id,
                    entity.InitialCatalog,
                    DateTime.UtcNow,
                    entity.DataSource,
                    entity.IntegratedSecurity,
                    entity.Password,
                    entity.UserId,
                    entity.EngineType,
                    entity.AuthenticationMode,
                    entity.ConnectionString);
                await UpdateAsync(updated);
            }
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
            // SELECT column ordinals:
            // 0=Id, 1=InitialCatalog, 2=CreationDate, 3=DataSource,
            // 4=IntegratedSecurity, 5=Password, 6=UserId, [7=EngineType, [8=AuthenticationMode, [9=ConnectionString]]]
            List<Connection> connections = new List<Connection>();
            await using var db = _context.GetConnection() as SqliteConnection ?? throw new Exception("db cannot be null or empty");
            await db.OpenAsync();

            // Try with ConnectionString column first
            try
            {
                const string sqlWithConnString = "SELECT Id, InitialCatalog, CreationDate, DataSource, IntegratedSecurity, Password, UserId, EngineType, AuthenticationMode, ConnectionString FROM Connections";
                await using SqliteCommand sqliteCommand = new SqliteCommand(sqlWithConnString, db);
                await using var query = await sqliteCommand.ExecuteReaderAsync();

                while (query.Read())
                {
                    var engineTypeValue = query.IsDBNull(7) ? null : (DatabaseEngineType?)query.GetInt32(7);
                    var authModeValue = query.IsDBNull(8) ? AuthenticationMode.WindowsAuth : (AuthenticationMode)query.GetInt32(8);
                    var storedPassword = query.IsDBNull(5) ? null : query.GetString(5);
                    var storedConnectionString = query.IsDBNull(9) ? null : query.GetString(9);
                    connections.Add(new Connection(
                        query.GetInt32(0),
                        query.GetString(1),
                        query.GetDateTime(2),
                        query.GetString(3),
                        query.GetBoolean(4),
                        DecryptPassword(storedPassword),
                        query.IsDBNull(6) ? null : query.GetString(6),
                        engineTypeValue,
                        authModeValue,
                        connectionString: DecryptConnectionString(storedConnectionString)));
                }
            }
            catch (SqliteException)
            {
                // Fallback for databases without ConnectionString column
                try
                {
                    const string sqlWithAuthMode = "SELECT Id, InitialCatalog, CreationDate, DataSource, IntegratedSecurity, Password, UserId, EngineType, AuthenticationMode FROM Connections";
                    await using SqliteCommand sqliteCommand = new SqliteCommand(sqlWithAuthMode, db);
                    await using var query = await sqliteCommand.ExecuteReaderAsync();

                    while (query.Read())
                    {
                        var engineTypeValue = query.IsDBNull(7) ? null : (DatabaseEngineType?)query.GetInt32(7);
                        var authModeValue = query.IsDBNull(8) ? AuthenticationMode.WindowsAuth : (AuthenticationMode)query.GetInt32(8);
                        var storedPassword = query.IsDBNull(5) ? null : query.GetString(5);
                        connections.Add(new Connection(
                            query.GetInt32(0),
                            query.GetString(1),
                            query.GetDateTime(2),
                            query.GetString(3),
                            query.GetBoolean(4),
                            DecryptPassword(storedPassword),
                            query.IsDBNull(6) ? null : query.GetString(6),
                            engineTypeValue,
                            authModeValue));
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

                        while (query.Read())
                        {
                            var engineTypeValue = query.IsDBNull(7) ? null : (DatabaseEngineType?)query.GetInt32(7);
                            var storedPassword = query.IsDBNull(5) ? null : query.GetString(5);
                            connections.Add(new Connection(
                                query.GetInt32(0),
                                query.GetString(1),
                                query.GetDateTime(2),
                                query.GetString(3),
                                query.GetBoolean(4),
                                DecryptPassword(storedPassword),
                                query.IsDBNull(6) ? null : query.GetString(6),
                                engineTypeValue));
                        }
                    }
                    catch (SqliteException)
                    {
                        // Fallback for databases without EngineType or AuthenticationMode column
                        const string sqlWithoutEngineType = "SELECT Id, InitialCatalog, CreationDate, DataSource, IntegratedSecurity, Password, UserId FROM Connections";
                        await using SqliteCommand sqliteCommand = new SqliteCommand(sqlWithoutEngineType, db);
                        await using var query = await sqliteCommand.ExecuteReaderAsync();

                        while (query.Read())
                        {
                            var storedPassword = query.IsDBNull(5) ? null : query.GetString(5);
                            connections.Add(new Connection(
                                query.GetInt32(0),
                                query.GetString(1),
                                query.GetDateTime(2),
                                query.GetString(3),
                                query.GetBoolean(4),
                                DecryptPassword(storedPassword),
                                query.IsDBNull(6) ? null : query.GetString(6),
                                null));
                        }
                    }
                }
            }

            return connections;
        }

        public async Task<Connection> GetByIdAsync(int id)
        {
            // SELECT column ordinals:
            // 0=Id, 1=InitialCatalog, 2=CreationDate, 3=DataSource,
            // 4=IntegratedSecurity, 5=Password, 6=UserId, [7=EngineType, [8=AuthenticationMode, [9=ConnectionString]]]
            Connection? connection = null;
            await using var db = _context.GetConnection() as SqliteConnection ?? throw new Exception("db cannot be null or empty");
            await db.OpenAsync();

            // Try with ConnectionString column first
            try
            {
                const string sqlWithConnString = "SELECT Id, InitialCatalog, CreationDate, DataSource, IntegratedSecurity, Password, UserId, EngineType, AuthenticationMode, ConnectionString FROM Connections WHERE Id = @Id";
                await using SqliteCommand sqliteCommand = new SqliteCommand(sqlWithConnString, db);
                sqliteCommand.Parameters.AddWithValue("@Id", id);
                await using var query = await sqliteCommand.ExecuteReaderAsync();

                while (query.Read())
                {
                    var engineTypeValue = query.IsDBNull(7) ? null : (DatabaseEngineType?)query.GetInt32(7);
                    var authModeValue = query.IsDBNull(8) ? AuthenticationMode.WindowsAuth : (AuthenticationMode)query.GetInt32(8);
                    var storedPassword = query.IsDBNull(5) ? null : query.GetString(5);
                    var storedConnectionString = query.IsDBNull(9) ? null : query.GetString(9);
                    connection = new Connection(
                        query.GetInt32(0),
                        query.GetString(1),
                        query.GetDateTime(2),
                        query.GetString(3),
                        query.GetBoolean(4),
                        DecryptPassword(storedPassword),
                        query.IsDBNull(6) ? null : query.GetString(6),
                        engineTypeValue,
                        authModeValue,
                        connectionString: DecryptConnectionString(storedConnectionString));
                }
            }
            catch (SqliteException)
            {
                // Fallback for databases without ConnectionString column
                try
                {
                    const string sqlWithAuthMode = "SELECT Id, InitialCatalog, CreationDate, DataSource, IntegratedSecurity, Password, UserId, EngineType, AuthenticationMode FROM Connections WHERE Id = @Id";
                    await using SqliteCommand sqliteCommand = new SqliteCommand(sqlWithAuthMode, db);
                    sqliteCommand.Parameters.AddWithValue("@Id", id);
                    await using var query = await sqliteCommand.ExecuteReaderAsync();

                    while (query.Read())
                    {
                        var engineTypeValue = query.IsDBNull(7) ? null : (DatabaseEngineType?)query.GetInt32(7);
                        var authModeValue = query.IsDBNull(8) ? AuthenticationMode.WindowsAuth : (AuthenticationMode)query.GetInt32(8);
                        var storedPassword = query.IsDBNull(5) ? null : query.GetString(5);
                        connection = new Connection(
                            query.GetInt32(0),
                            query.GetString(1),
                            query.GetDateTime(2),
                            query.GetString(3),
                            query.GetBoolean(4),
                            DecryptPassword(storedPassword),
                            query.IsDBNull(6) ? null : query.GetString(6),
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

                        while (query.Read())
                        {
                            var engineTypeValue = query.IsDBNull(7) ? null : (DatabaseEngineType?)query.GetInt32(7);
                            var storedPassword = query.IsDBNull(5) ? null : query.GetString(5);
                            connection = new Connection(
                                query.GetInt32(0),
                                query.GetString(1),
                                query.GetDateTime(2),
                                query.GetString(3),
                                query.GetBoolean(4),
                                DecryptPassword(storedPassword),
                                query.IsDBNull(6) ? null : query.GetString(6),
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

                        while (query.Read())
                        {
                            var storedPassword = query.IsDBNull(5) ? null : query.GetString(5);
                            connection = new Connection(
                                query.GetInt32(0),
                                query.GetString(1),
                                query.GetDateTime(2),
                                query.GetString(3),
                                query.GetBoolean(4),
                                DecryptPassword(storedPassword),
                                query.IsDBNull(6) ? null : query.GetString(6),
                                null);
                        }
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

            string? encryptedPassword = EncryptPassword(entity.Password);

            // Try with ConnectionString column first
            try
            {
                const string sqlWithConnString = "UPDATE Connections SET DataSource=@DataSource, InitialCatalog=@InitialCatalog, UserId=@UserId, Password=@Password, IntegratedSecurity=@IntegratedSecurity, EngineType=@EngineType, AuthenticationMode=@AuthenticationMode, ConnectionString=@ConnectionString WHERE Id = @Id";
                await using SqliteCommand sqliteCommand = new SqliteCommand(sqlWithConnString, db);
                sqliteCommand.Parameters.AddWithValue("@Id", entity.Id);
                sqliteCommand.Parameters.AddWithValue("@DataSource", entity.DataSource);
                sqliteCommand.Parameters.AddWithValue("@InitialCatalog", entity.InitialCatalog);
                sqliteCommand.Parameters.AddWithValue("@UserId", (object?)entity.UserId ?? DBNull.Value);
                sqliteCommand.Parameters.AddWithValue("@Password", (object?)encryptedPassword ?? DBNull.Value);
                sqliteCommand.Parameters.AddWithValue("@IntegratedSecurity", entity.IntegratedSecurity);
                sqliteCommand.Parameters.AddWithValue("@EngineType", entity.EngineType.HasValue ? (int)entity.EngineType.Value : DBNull.Value);
                sqliteCommand.Parameters.AddWithValue("@AuthenticationMode", (int)entity.AuthenticationMode);
                sqliteCommand.Parameters.AddWithValue("@ConnectionString", (object?)EncryptConnectionString(entity.ConnectionString) ?? DBNull.Value);
                await sqliteCommand.ExecuteNonQueryAsync();
            }
            catch (SqliteException)
            {
                // Fallback for databases without ConnectionString column
                try
                {
                    const string sqlWithAuthMode = "UPDATE Connections SET DataSource=@DataSource, InitialCatalog=@InitialCatalog, UserId=@UserId, Password=@Password, IntegratedSecurity=@IntegratedSecurity, EngineType=@EngineType, AuthenticationMode=@AuthenticationMode WHERE Id = @Id";
                    await using SqliteCommand sqliteCommand = new SqliteCommand(sqlWithAuthMode, db);
                    sqliteCommand.Parameters.AddWithValue("@Id", entity.Id);
                    sqliteCommand.Parameters.AddWithValue("@DataSource", entity.DataSource);
                    sqliteCommand.Parameters.AddWithValue("@InitialCatalog", entity.InitialCatalog);
                    sqliteCommand.Parameters.AddWithValue("@UserId", (object?)entity.UserId ?? DBNull.Value);
                    sqliteCommand.Parameters.AddWithValue("@Password", (object?)encryptedPassword ?? DBNull.Value);
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
                        sqliteCommand.Parameters.AddWithValue("@UserId", (object?)entity.UserId ?? DBNull.Value);
                        sqliteCommand.Parameters.AddWithValue("@Password", (object?)encryptedPassword ?? DBNull.Value);
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
                        sqliteCommand.Parameters.AddWithValue("@UserId", (object?)entity.UserId ?? DBNull.Value);
                        sqliteCommand.Parameters.AddWithValue("@Password", (object?)encryptedPassword ?? DBNull.Value);
                        sqliteCommand.Parameters.AddWithValue("@IntegratedSecurity", entity.IntegratedSecurity);
                        await sqliteCommand.ExecuteNonQueryAsync();
                    }
                }
            }
        }
    }
}
