using LightQueryProfiler.Shared.Enums;
using LightQueryProfiler.Shared.Extensions;

namespace LightQueryProfiler.Shared.UnitTests.Enums
{
    [TestFixture]
    public class AuthenticationModeTests
    {
        [Test]
        public void AuthenticationMode_WindowsAuth_HasCorrectValue()
        {
            // Arrange & Act
            var mode = AuthenticationMode.WindowsAuth;

            // Assert
            Assert.That((int)mode, Is.EqualTo(0));
        }

        [Test]
        public void AuthenticationMode_SQLServerAuth_HasCorrectValue()
        {
            // Arrange & Act
            var mode = AuthenticationMode.SQLServerAuth;

            // Assert
            Assert.That((int)mode, Is.EqualTo(1));
        }

        [Test]
        public void AuthenticationMode_AzureSQLDatabase_HasCorrectValue()
        {
            // Arrange & Act
            var mode = AuthenticationMode.AzureSQLDatabase;

            // Assert
            Assert.That((int)mode, Is.EqualTo(2));
        }

        [Test]
        public void GetString_WhenWindowsAuth_ReturnsCorrectString()
        {
            // Arrange
            var mode = AuthenticationMode.WindowsAuth;

            // Act
            var result = mode.GetString();

            // Assert
            Assert.That(result, Is.EqualTo("Windows Authentication"));
        }

        [Test]
        public void GetString_WhenSQLServerAuth_ReturnsCorrectString()
        {
            // Arrange
            var mode = AuthenticationMode.SQLServerAuth;

            // Act
            var result = mode.GetString();

            // Assert
            Assert.That(result, Is.EqualTo("SQL Server Authentication"));
        }

        [Test]
        public void GetString_WhenAzureSQLDatabase_ReturnsCorrectString()
        {
            // Arrange
            var mode = AuthenticationMode.AzureSQLDatabase;

            // Act
            var result = mode.GetString();

            // Assert
            Assert.That(result, Is.EqualTo("Azure SQL Database"));
        }

        [Test]
        public void GetString_WhenInvalidValue_ReturnsEmptyString()
        {
            // Arrange
            var mode = (AuthenticationMode)999;

            // Act
            var result = mode.GetString();

            // Assert
            Assert.That(result, Is.EqualTo(string.Empty));
        }

        [Test]
        public void AuthenticationMode_AllEnumValues_HaveUniqueIntegerValues()
        {
            // Arrange
            var allModes = Enum.GetValues(typeof(AuthenticationMode)).Cast<AuthenticationMode>().ToList();
            var intValues = allModes.Select(m => (int)m).ToList();

            // Act & Assert
            Assert.That(intValues.Distinct().Count(), Is.EqualTo(allModes.Count),
                "All authentication modes should have unique integer values");
        }

        [Test]
        public void AuthenticationMode_AllEnumValues_HaveNonEmptyStringRepresentation()
        {
            // Arrange
            var allModes = Enum.GetValues(typeof(AuthenticationMode)).Cast<AuthenticationMode>().ToList();

            // Act & Assert
            foreach (var mode in allModes)
            {
                var stringValue = mode.GetString();
                Assert.That(string.IsNullOrWhiteSpace(stringValue), Is.False,
                    $"Authentication mode {mode} should have a non-empty string representation");
            }
        }
    }
}
