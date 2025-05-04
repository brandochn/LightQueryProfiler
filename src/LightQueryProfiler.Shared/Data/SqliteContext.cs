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
                        CreationDate Date
                    )";

            SqliteCommand createTable = new(tableCommand, db);

            await createTable.ExecuteReaderAsync();
        }
    }
}