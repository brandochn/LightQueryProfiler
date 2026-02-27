using LightQueryProfiler.Shared.Enums;
using LightQueryProfiler.Shared.Extensions;

namespace LightQueryProfiler.Shared.UnitTests.Enums
{
    public class AuthenticationModeTests
    {
        [Fact]
        public void AuthenticationMode_WindowsAuth_HasCorrectValue()
        {
            // Arrange & Act
            var mode = AuthenticationMode.WindowsAuth;

            // Assert
            Assert.Equal(0, (int)mode);
        }

        [Fact]
        public void AuthenticationMode_SQLServerAuth_HasCorrectValue()
        {
            // Arrange & Act
            var mode = AuthenticationMode.SQLServerAuth;

            // Assert
            Assert.Equal(1, (int)mode);
        }

        [Fact]
        public void AuthenticationMode_AzureSQLDatabase_HasCorrectValue()
        {
            // Arrange & Act
            var mode = AuthenticationMode.AzureSQLDatabase;

            // Assert
            Assert.Equal(2, (int)mode);
        }

        [Fact]
        public void GetString_WhenWindowsAuth_ReturnsCorrectString()
        {
            // Arrange
            var mode = AuthenticationMode.WindowsAuth;

            // Act
            var result = mode.GetString();

            // Assert
            Assert.Equal("Windows Authentication", result);
        }

        [Fact]
        public void GetString_WhenSQLServerAuth_ReturnsCorrectString()
        {
            // Arrange
            var mode = AuthenticationMode.SQLServerAuth;

            // Act
            var result = mode.GetString();

            // Assert
            Assert.Equal("SQL Server Authentication", result);
        }

        [Fact]
        public void GetString_WhenAzureSQLDatabase_ReturnsCorrectString()
        {
            // Arrange
            var mode = AuthenticationMode.AzureSQLDatabase;

            // Act
            var result = mode.GetString();

            // Assert
            Assert.Equal("Azure SQL Database", result);
        }

        [Fact]
        public void GetString_WhenInvalidValue_ReturnsEmptyString()
        {
            // Arrange
            var mode = (AuthenticationMode)999;

            // Act
            var result = mode.GetString();

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void AuthenticationMode_AllEnumValues_HaveUniqueIntegerValues()
        {
            // Arrange
            var allModes = Enum.GetValues(typeof(AuthenticationMode)).Cast<AuthenticationMode>().ToList();
            var intValues = allModes.Select(m => (int)m).ToList();

            // Act & Assert
            Assert.Equal(allModes.Count, intValues.Distinct().Count());
        }

        [Fact]
        public void AuthenticationMode_AllEnumValues_HaveNonEmptyStringRepresentation()
        {
            // Arrange
            var allModes = Enum.GetValues(typeof(AuthenticationMode)).Cast<AuthenticationMode>().ToList();

            // Act & Assert
            foreach (var mode in allModes)
            {
                var stringValue = mode.GetString();
                Assert.False(string.IsNullOrWhiteSpace(stringValue));
            }
        }
    }
}
