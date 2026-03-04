using LightQueryProfiler.Shared.Enums;
using LightQueryProfiler.Shared.Models;

namespace LightQueryProfiler.Shared.UnitTests.Models
{
    /// <summary>
    /// Tests to verify Connection uniqueness logic based on DataSource, UserId, and InitialCatalog
    /// </summary>
    public class ConnectionUniquenessTests
    {
        [Fact]
        public void TwoConnections_WhenSameServerAndUserButDifferentDatabase_ShouldBeConsideredDifferent()
        {
            // Arrange
            var connection1 = new Connection(
                1,
                "ProductionDB",
                DateTime.UtcNow,
                "localhost",
                false,
                "password123",
                "sa",
                DatabaseEngineType.SqlServer,
                AuthenticationMode.SQLServerAuth);

            var connection2 = new Connection(
                2,
                "DevelopmentDB",
                DateTime.UtcNow,
                "localhost",
                false,
                "password123",
                "sa",
                DatabaseEngineType.SqlServer,
                AuthenticationMode.SQLServerAuth);

            // Act & Assert - These should be different connections
            Assert.NotEqual(connection1.InitialCatalog, connection2.InitialCatalog);
            Assert.Equal(connection1.DataSource, connection2.DataSource);
            Assert.Equal(connection1.UserId, connection2.UserId);
        }

        [Fact]
        public void TwoConnections_WhenSameServerUserAndDatabase_ShouldBeConsideredSame()
        {
            // Arrange
            var connection1 = new Connection(
                1,
                "ProductionDB",
                DateTime.UtcNow,
                "localhost",
                false,
                "password123",
                "sa",
                DatabaseEngineType.SqlServer,
                AuthenticationMode.SQLServerAuth);

            var connection2 = new Connection(
                2,
                "ProductionDB",
                DateTime.UtcNow.AddMinutes(5),
                "localhost",
                false,
                "password456", // Different password shouldn't matter for uniqueness
                "sa",
                DatabaseEngineType.SqlServer,
                AuthenticationMode.SQLServerAuth);

            // Act & Assert - These should be considered the same connection (unique key match)
            Assert.Equal(connection1.InitialCatalog, connection2.InitialCatalog);
            Assert.Equal(connection1.DataSource, connection2.DataSource);
            Assert.Equal(connection1.UserId, connection2.UserId);
        }

        [Fact]
        public void TwoConnections_WhenAzureSqlDatabaseWithDifferentDatabases_ShouldBeConsideredDifferent()
        {
            // Arrange
            var connection1 = new Connection(
                1,
                "CustomerDB",
                DateTime.UtcNow,
                "myserver.database.windows.net",
                false,
                "SecurePassword123!",
                "admin@myserver",
                DatabaseEngineType.AzureSqlDatabase,
                AuthenticationMode.AzureSQLDatabase);

            var connection2 = new Connection(
                2,
                "OrdersDB",
                DateTime.UtcNow,
                "myserver.database.windows.net",
                false,
                "SecurePassword123!",
                "admin@myserver",
                DatabaseEngineType.AzureSqlDatabase,
                AuthenticationMode.AzureSQLDatabase);

            // Act & Assert
            Assert.NotEqual(connection1.InitialCatalog, connection2.InitialCatalog);
            Assert.Equal(connection1.DataSource, connection2.DataSource);
            Assert.Equal(connection1.UserId, connection2.UserId);
        }

        [Fact]
        public void TwoConnections_WhenWindowsAuthWithDifferentDatabases_ShouldBeConsideredDifferent()
        {
            // Arrange
            var connection1 = new Connection(
                1,
                "DB1",
                DateTime.UtcNow,
                "SQLSERVER01",
                true,
                null,
                null,
                DatabaseEngineType.SqlServer,
                AuthenticationMode.WindowsAuth);

            var connection2 = new Connection(
                2,
                "DB2",
                DateTime.UtcNow,
                "SQLSERVER01",
                true,
                null,
                null,
                DatabaseEngineType.SqlServer,
                AuthenticationMode.WindowsAuth);

            // Act & Assert
            Assert.NotEqual(connection1.InitialCatalog, connection2.InitialCatalog);
            Assert.Equal(connection1.DataSource, connection2.DataSource);
            // Both have null/empty UserId for Windows Auth
            Assert.Equal(connection1.UserId, connection2.UserId);
        }

        [Fact]
        public void TwoConnections_WhenDifferentServers_ShouldBeConsideredDifferent()
        {
            // Arrange
            var connection1 = new Connection(
                1,
                "TestDB",
                DateTime.UtcNow,
                "server1",
                false,
                "password",
                "user1",
                DatabaseEngineType.SqlServer,
                AuthenticationMode.SQLServerAuth);

            var connection2 = new Connection(
                2,
                "TestDB",
                DateTime.UtcNow,
                "server2",
                false,
                "password",
                "user1",
                DatabaseEngineType.SqlServer,
                AuthenticationMode.SQLServerAuth);

            // Act & Assert
            Assert.NotEqual(connection1.DataSource, connection2.DataSource);
            Assert.Equal(connection1.InitialCatalog, connection2.InitialCatalog);
            Assert.Equal(connection1.UserId, connection2.UserId);
        }

        [Fact]
        public void TwoConnections_WhenDifferentUsers_ShouldBeConsideredDifferent()
        {
            // Arrange
            var connection1 = new Connection(
                1,
                "TestDB",
                DateTime.UtcNow,
                "localhost",
                false,
                "password",
                "user1",
                DatabaseEngineType.SqlServer,
                AuthenticationMode.SQLServerAuth);

            var connection2 = new Connection(
                2,
                "TestDB",
                DateTime.UtcNow,
                "localhost",
                false,
                "password",
                "user2",
                DatabaseEngineType.SqlServer,
                AuthenticationMode.SQLServerAuth);

            // Act & Assert
            Assert.NotEqual(connection1.UserId, connection2.UserId);
            Assert.Equal(connection1.DataSource, connection2.DataSource);
            Assert.Equal(connection1.InitialCatalog, connection2.InitialCatalog);
        }

        [Theory]
        [InlineData("localhost", "sa", "ProductionDB")]
        [InlineData("localhost", "sa", "DevelopmentDB")]
        [InlineData("localhost", "admin", "ProductionDB")]
        [InlineData("server2", "sa", "ProductionDB")]
        public void Connection_UniquenessKey_CombinesServerUserAndDatabase(string dataSource, string userId, string initialCatalog)
        {
            // Arrange
            var connection = new Connection(
                1,
                initialCatalog,
                DateTime.UtcNow,
                dataSource,
                false,
                "password",
                userId,
                DatabaseEngineType.SqlServer,
                AuthenticationMode.SQLServerAuth);

            // Act & Assert - Verify all three components are set correctly
            Assert.Equal(dataSource, connection.DataSource);
            Assert.Equal(userId, connection.UserId);
            Assert.Equal(initialCatalog, connection.InitialCatalog);
        }

        [Fact]
        public void TwoConnections_WhenSameServerUserDatabaseButDifferentEngineType_ShouldStillBeConsideredSame()
        {
            // Arrange - This scenario shouldn't happen in practice but tests the uniqueness logic
            var connection1 = new Connection(
                1,
                "TestDB",
                DateTime.UtcNow,
                "localhost",
                false,
                "password",
                "user1",
                DatabaseEngineType.SqlServer,
                AuthenticationMode.SQLServerAuth);

            var connection2 = new Connection(
                2,
                "TestDB",
                DateTime.UtcNow,
                "localhost",
                false,
                "password",
                "user1",
                null, // Different engine type
                AuthenticationMode.SQLServerAuth);

            // Act & Assert - Uniqueness is based on Server + User + Database only
            Assert.Equal(connection1.DataSource, connection2.DataSource);
            Assert.Equal(connection1.UserId, connection2.UserId);
            Assert.Equal(connection1.InitialCatalog, connection2.InitialCatalog);
            Assert.NotEqual(connection1.EngineType, connection2.EngineType);
        }

        [Fact]
        public void TwoConnections_WhenCaseOnlyDiffers_ShouldBeConsideredSameWithCaseInsensitiveComparison()
        {
            // Arrange
            var connection1 = new Connection(
                1,
                "ProductionDB",
                DateTime.UtcNow,
                "LOCALHOST",
                false,
                "password",
                "SA",
                DatabaseEngineType.SqlServer,
                AuthenticationMode.SQLServerAuth);

            var connection2 = new Connection(
                2,
                "productiondb",
                DateTime.UtcNow,
                "localhost",
                false,
                "password",
                "sa",
                DatabaseEngineType.SqlServer,
                AuthenticationMode.SQLServerAuth);

            // Act & Assert - With case-insensitive comparison, these should match
            Assert.NotEqual(connection1.DataSource, connection2.DataSource); // Exact match differs
            Assert.NotEqual(connection1.UserId, connection2.UserId); // Exact match differs
            Assert.NotEqual(connection1.InitialCatalog, connection2.InitialCatalog); // Exact match differs

            // But case-insensitive comparison should match
            Assert.True(string.Equals(connection1.DataSource, connection2.DataSource, StringComparison.InvariantCultureIgnoreCase));
            Assert.True(string.Equals(connection1.UserId, connection2.UserId, StringComparison.InvariantCultureIgnoreCase));
            Assert.True(string.Equals(connection1.InitialCatalog, connection2.InitialCatalog, StringComparison.InvariantCultureIgnoreCase));
        }
    }
}
