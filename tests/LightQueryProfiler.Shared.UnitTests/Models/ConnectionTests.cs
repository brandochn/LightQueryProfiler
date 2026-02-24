using LightQueryProfiler.Shared.Enums;
using LightQueryProfiler.Shared.Models;

namespace LightQueryProfiler.Shared.UnitTests.Models
{
    [TestFixture]
    public class ConnectionTests
    {
        [Test]
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
            Assert.That(connection.Id, Is.EqualTo(id));
            Assert.That(connection.InitialCatalog, Is.EqualTo(initialCatalog));
            Assert.That(connection.CreationDate, Is.EqualTo(creationDate));
            Assert.That(connection.DataSource, Is.EqualTo(dataSource));
            Assert.That(connection.IntegratedSecurity, Is.EqualTo(integratedSecurity));
            Assert.That(connection.Password, Is.EqualTo(password));
            Assert.That(connection.UserId, Is.EqualTo(userId));
            Assert.That(connection.EngineType, Is.EqualTo(engineType));
            Assert.That(connection.AuthenticationMode, Is.EqualTo(authMode));
        }

        [Test]
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
            Assert.That(connection.AuthenticationMode, Is.EqualTo(AuthenticationMode.WindowsAuth));
        }

        [Test]
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
            Assert.That(connection.AuthenticationMode, Is.EqualTo(AuthenticationMode.AzureSQLDatabase));
            Assert.That(connection.InitialCatalog, Is.EqualTo(initialCatalog));
            Assert.That(connection.DataSource, Is.EqualTo(dataSource));
        }

        [Test]
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
            Assert.That(connection.AuthenticationMode, Is.EqualTo(AuthenticationMode.SQLServerAuth));
            Assert.That(connection.IntegratedSecurity, Is.EqualTo(integratedSecurity));
        }

        [Test]
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
            Assert.That(connection.EngineType, Is.Null);
        }

        [Test]
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
            Assert.That(connection.Password, Is.Null);
            Assert.That(connection.UserId, Is.Null);
        }

        [TestCase(AuthenticationMode.WindowsAuth)]
        [TestCase(AuthenticationMode.SQLServerAuth)]
        [TestCase(AuthenticationMode.AzureSQLDatabase)]
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
            Assert.That(connection.AuthenticationMode, Is.EqualTo(authMode));
        }
    }
}
