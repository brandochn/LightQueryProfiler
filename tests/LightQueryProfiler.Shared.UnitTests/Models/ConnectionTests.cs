using LightQueryProfiler.Shared.Enums;
using LightQueryProfiler.Shared.Models;

namespace LightQueryProfiler.Shared.UnitTests.Models
{
    public class ConnectionTests
    {
        [Fact]
        public void Constructor_WhenCalledWithAllParameters_SetsPropertiesCorrectly()
        {
            // Arrange
            var id = 1;
            var initialCatalog = "TestDB";
            var creationDate = DateTime.UtcNow;
            var dataSource = "localhost";
            var integratedSecurity = true;
            var password = "password123";
            var userId = "testuser";
            var engineType = DatabaseEngineType.SqlServer;
            var authMode = AuthenticationMode.WindowsAuth;

            // Act
            var connection = new Connection(
                id,
                initialCatalog,
                creationDate,
                dataSource,
                integratedSecurity,
                password,
                userId,
                engineType,
                authMode);

            // Assert
            Assert.Equal(id, connection.Id);
            Assert.Equal(initialCatalog, connection.InitialCatalog);
            Assert.Equal(creationDate, connection.CreationDate);
            Assert.Equal(dataSource, connection.DataSource);
            Assert.Equal(integratedSecurity, connection.IntegratedSecurity);
            Assert.Equal(password, connection.Password);
            Assert.Equal(userId, connection.UserId);
            Assert.Equal(engineType, connection.EngineType);
            Assert.Equal(authMode, connection.AuthenticationMode);
        }

        [Fact]
        public void Constructor_WhenAuthenticationModeNotProvided_DefaultsToWindowsAuth()
        {
            // Arrange
            var id = 1;
            var initialCatalog = "TestDB";
            var creationDate = DateTime.UtcNow;
            var dataSource = "localhost";
            var integratedSecurity = true;
            var password = "password123";
            var userId = "testuser";
            var engineType = DatabaseEngineType.SqlServer;

            // Act
            var connection = new Connection(
                id,
                initialCatalog,
                creationDate,
                dataSource,
                integratedSecurity,
                password,
                userId,
                engineType);

            // Assert
            Assert.Equal(AuthenticationMode.WindowsAuth, connection.AuthenticationMode);
        }

        [Fact]
        public void Constructor_WhenCreatingAzureSqlDatabaseConnection_StoresAuthenticationModeCorrectly()
        {
            // Arrange
            var id = 2;
            var initialCatalog = "AzureTestDB";
            var creationDate = DateTime.UtcNow;
            var dataSource = "myserver.database.windows.net";
            var integratedSecurity = false;
            var password = "SecurePassword123!";
            var userId = "azureuser";
            var engineType = DatabaseEngineType.SqlServer;
            var authMode = AuthenticationMode.AzureSQLDatabase;

            // Act
            var connection = new Connection(
                id,
                initialCatalog,
                creationDate,
                dataSource,
                integratedSecurity,
                password,
                userId,
                engineType,
                authMode);

            // Assert
            Assert.Equal(AuthenticationMode.AzureSQLDatabase, connection.AuthenticationMode);
            Assert.Equal(initialCatalog, connection.InitialCatalog);
            Assert.Equal(dataSource, connection.DataSource);
        }

        [Fact]
        public void Constructor_WhenCreatingSqlServerAuthConnection_StoresAuthenticationModeCorrectly()
        {
            // Arrange
            var id = 3;
            var initialCatalog = "SqlServerDB";
            var creationDate = DateTime.UtcNow;
            var dataSource = "sqlserver.local";
            var integratedSecurity = false;
            var password = "SqlPassword123!";
            var userId = "sqluser";
            var engineType = DatabaseEngineType.SqlServer;
            var authMode = AuthenticationMode.SQLServerAuth;

            // Act
            var connection = new Connection(
                id,
                initialCatalog,
                creationDate,
                dataSource,
                integratedSecurity,
                password,
                userId,
                engineType,
                authMode);

            // Assert
            Assert.Equal(AuthenticationMode.SQLServerAuth, connection.AuthenticationMode);
            Assert.Equal(integratedSecurity, connection.IntegratedSecurity);
        }

        [Fact]
        public void Constructor_WhenEngineTypeIsNull_AcceptsNullValue()
        {
            // Arrange
            var id = 4;
            var initialCatalog = "TestDB";
            var creationDate = DateTime.UtcNow;
            var dataSource = "localhost";
            var integratedSecurity = true;
            var password = "password123";
            var userId = "testuser";
            DatabaseEngineType? engineType = null;
            var authMode = AuthenticationMode.WindowsAuth;

            // Act
            var connection = new Connection(
                id,
                initialCatalog,
                creationDate,
                dataSource,
                integratedSecurity,
                password,
                userId,
                engineType,
                authMode);

            // Assert
            Assert.Null(connection.EngineType);
        }

        [Fact]
        public void Constructor_WhenPasswordIsNull_AcceptsNullValue()
        {
            // Arrange
            var id = 5;
            var initialCatalog = "TestDB";
            var creationDate = DateTime.UtcNow;
            var dataSource = "localhost";
            var integratedSecurity = true;
            string? password = null;
            string? userId = null;
            var engineType = DatabaseEngineType.SqlServer;
            var authMode = AuthenticationMode.WindowsAuth;

            // Act
            var connection = new Connection(
                id,
                initialCatalog,
                creationDate,
                dataSource,
                integratedSecurity,
                password,
                userId,
                engineType,
                authMode);

            // Assert
            Assert.Null(connection.Password);
            Assert.Null(connection.UserId);
        }

        [Theory]
        [InlineData(AuthenticationMode.WindowsAuth)]
        [InlineData(AuthenticationMode.SQLServerAuth)]
        [InlineData(AuthenticationMode.AzureSQLDatabase)]
        public void Constructor_WithDifferentAuthenticationModes_StoresCorrectMode(AuthenticationMode authMode)
        {
            // Arrange
            var id = 6;
            var initialCatalog = "TestDB";
            var creationDate = DateTime.UtcNow;
            var dataSource = "localhost";
            var integratedSecurity = authMode == AuthenticationMode.WindowsAuth;
            var password = "password123";
            var userId = "testuser";
            var engineType = DatabaseEngineType.SqlServer;

            // Act
            var connection = new Connection(
                id,
                initialCatalog,
                creationDate,
                dataSource,
                integratedSecurity,
                password,
                userId,
                engineType,
                authMode);

            // Assert
            Assert.Equal(authMode, connection.AuthenticationMode);
        }
    }
}
