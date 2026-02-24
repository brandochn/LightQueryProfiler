using LightQueryProfiler.Shared.Enums;
using LightQueryProfiler.Shared.Factories;
using LightQueryProfiler.Shared.Models;

namespace LightQueryProfiler.Shared.UnitTests.Factories;

[TestFixture]
public class ProfilerSessionTemplateFactoryTests
{
    [Test]
    public void CreateTemplate_WhenEngineTypeIsSqlServer_ReturnsDefaultProfilerSessionTemplate()
    {
        // Arrange
        var engineType = DatabaseEngineType.SqlServer;

        // Act
        var result = ProfilerSessionTemplateFactory.CreateTemplate(engineType);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.TypeOf<DefaultProfilerSessionTemplate>());
        Assert.That(result.Name, Is.EqualTo("Default"));
    }

    [Test]
    public void CreateTemplate_WhenEngineTypeIsAzureSqlDatabase_ReturnsAzureSqlProfilerSessionTemplate()
    {
        // Arrange
        var engineType = DatabaseEngineType.AzureSqlDatabase;

        // Act
        var result = ProfilerSessionTemplateFactory.CreateTemplate(engineType);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.TypeOf<AzureSqlProfilerSessionTemplate>());
        Assert.That(result.Name, Is.EqualTo("Azure SQL Database Default"));
    }

    [Test]
    public void CreateTemplate_WhenSqlServerTemplate_CreatesSQLStatementWithOnServer()
    {
        // Arrange
        var engineType = DatabaseEngineType.SqlServer;
        var sessionName = "TestSession";

        // Act
        var template = ProfilerSessionTemplateFactory.CreateTemplate(engineType);
        var sqlStatement = template.CreateSQLStatement(sessionName);

        // Assert
        Assert.That(sqlStatement, Does.Contain("ON SERVER"));
        Assert.That(sqlStatement, Does.Contain("sys.server_event_sessions"));
        Assert.That(sqlStatement, Does.Not.Contain("ON DATABASE"));
        Assert.That(sqlStatement, Does.Not.Contain("sys.database_event_sessions"));
    }

    [Test]
    public void CreateTemplate_WhenAzureSqlDatabaseTemplate_CreatesSQLStatementWithOnDatabase()
    {
        // Arrange
        var engineType = DatabaseEngineType.AzureSqlDatabase;
        var sessionName = "TestSession";

        // Act
        var template = ProfilerSessionTemplateFactory.CreateTemplate(engineType);
        var sqlStatement = template.CreateSQLStatement(sessionName);

        // Assert
        Assert.That(sqlStatement, Does.Contain("ON DATABASE"));
        Assert.That(sqlStatement, Does.Contain("sys.database_event_sessions"));
        Assert.That(sqlStatement, Does.Not.Contain("ON SERVER"));
        Assert.That(sqlStatement, Does.Not.Contain("sys.server_event_sessions"));
    }

    [Test]
    public void CreateTemplate_BothTemplates_ReturnSameDefaultView()
    {
        // Arrange
        var sqlServerTemplate = ProfilerSessionTemplateFactory.CreateTemplate(DatabaseEngineType.SqlServer);
        var azureTemplate = ProfilerSessionTemplateFactory.CreateTemplate(DatabaseEngineType.AzureSqlDatabase);

        // Act
        var sqlServerView = sqlServerTemplate.GetDefaultView();
        var azureView = azureTemplate.GetDefaultView();

        // Assert
        Assert.That(sqlServerView, Is.EqualTo("DefaultProfilerViewTemplate"));
        Assert.That(azureView, Is.EqualTo("DefaultProfilerViewTemplate"));
    }
}
