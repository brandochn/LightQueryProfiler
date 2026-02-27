using LightQueryProfiler.Shared.Models;
using Microsoft.Data.Sqlite;
using System.Data;

namespace LightQueryProfiler.Shared.Data
{
    public class SqliteContext : IDatabaseContext
    {
        private const string dataBaseName = "localStorage.db";

        public IDbConnection GetConnection()
        {
            string dbPath = Path.Combine(AppContext.BaseDirectory, dataBaseName);
            return new SqliteConnection($"Filename={dbPath}");
        }

        public static async void InitializeDatabase()
        {
            string dbPath = Path.Combine(AppContext.BaseDirectory, dataBaseName);
            if (!File.Exists(dbPath))
            {
                await using FileStream fs = File.Create(dbPath);
            }

            await using SqliteConnection db = new($"Filename={dbPath}");
            db.Open();

            const string tableCommand = @"
                    CREATE TABLE IF NOT
                    EXISTS Connections
                    (
                        Id INTEGER PRIMARY KEY,
                        DataSource NVARCHAR(1000) NULL,
                        InitialCatalog NVARCHAR(100) NULL,
                        UserId NVARCHAR(100) NULL,
                        Password NVARCHAR(100) NULL,
                        IntegratedSecurity INTEGER NULL,
                        CreationDate Date,
                        EngineType INTEGER NULL,
                        AuthenticationMode INTEGER NULL
                    )";

            SqliteCommand createTable = new(tableCommand, db);
            await createTable.ExecuteReaderAsync();

            // Migration: Add EngineType column if it doesn't exist (for existing databases)
            const string addColumnCommand = @"
                    SELECT COUNT(*) as ColumnExists
                    FROM pragma_table_info('Connections')
                    WHERE name='EngineType'";

            SqliteCommand checkColumn = new(addColumnCommand, db);
            var result = await checkColumn.ExecuteScalarAsync();

            if (result != null && Convert.ToInt32(result) == 0)
            {
                const string alterTableCommand = "ALTER TABLE Connections ADD COLUMN EngineType INTEGER NULL";
                SqliteCommand alterTable = new(alterTableCommand, db);
                await alterTable.ExecuteNonQueryAsync();
            }

            // Migration: Add AuthenticationMode column if it doesn't exist (for existing databases)
            const string addAuthModeColumnCommand = @"
                    SELECT COUNT(*) as ColumnExists
                    FROM pragma_table_info('Connections')
                    WHERE name='AuthenticationMode'";

            SqliteCommand checkAuthModeColumn = new(addAuthModeColumnCommand, db);
            var authModeResult = await checkAuthModeColumn.ExecuteScalarAsync();

            if (authModeResult != null && Convert.ToInt32(authModeResult) == 0)
            {
                const string alterTableAuthModeCommand = "ALTER TABLE Connections ADD COLUMN AuthenticationMode INTEGER NULL";
                SqliteCommand alterTableAuthMode = new(alterTableAuthModeCommand, db);
                await alterTableAuthMode.ExecuteNonQueryAsync();
            }
        }
    }
}
