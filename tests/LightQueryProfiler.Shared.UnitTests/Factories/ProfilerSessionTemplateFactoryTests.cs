using LightQueryProfiler.Shared.Enums;
using LightQueryProfiler.Shared.Factories;
using LightQueryProfiler.Shared.Models;

namespace LightQueryProfiler.Shared.UnitTests.Factories;

public class ProfilerSessionTemplateFactoryTests
{
    [Fact]
    public void CreateTemplate_WhenEngineTypeIsSqlServer_ReturnsDefaultProfilerSessionTemplate()
    {
        // Arrange
        var engineType = DatabaseEngineType.SqlServer;

        // Act
        var result = ProfilerSessionTemplateFactory.CreateTemplate(engineType);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<DefaultProfilerSessionTemplate>(result);
        Assert.Equal("Default", result.Name);
    }

    [Fact]
    public void CreateTemplate_WhenEngineTypeIsAzureSqlDatabase_ReturnsAzureSqlProfilerSessionTemplate()
    {
        // Arrange
        var engineType = DatabaseEngineType.AzureSqlDatabase;

        // Act
        var result = ProfilerSessionTemplateFactory.CreateTemplate(engineType);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<AzureSqlProfilerSessionTemplate>(result);
        Assert.Equal("Azure SQL Database Default", result.Name);
    }

    [Fact]
    public void CreateTemplate_WhenSqlServerTemplate_CreatesSQLStatementWithOnServer()
    {
        // Arrange
        var engineType = DatabaseEngineType.SqlServer;
        var sessionName = "TestSession";

        // Act
        var template = ProfilerSessionTemplateFactory.CreateTemplate(engineType);
        var sqlStatement = template.CreateSQLStatement(sessionName);

        // Assert
        Assert.Contains("ON SERVER", sqlStatement);
        Assert.Contains("sys.server_event_sessions", sqlStatement);
        Assert.DoesNotContain("ON DATABASE", sqlStatement);
        Assert.DoesNotContain("sys.database_event_sessions", sqlStatement);
    }

    [Fact]
    public void CreateTemplate_WhenAzureSqlDatabaseTemplate_CreatesSQLStatementWithOnDatabase()
    {
        // Arrange
        var engineType = DatabaseEngineType.AzureSqlDatabase;
        var sessionName = "TestSession";

        // Act
        var template = ProfilerSessionTemplateFactory.CreateTemplate(engineType);
        var sqlStatement = template.CreateSQLStatement(sessionName);

        // Assert
        Assert.Contains("ON DATABASE", sqlStatement);
        Assert.Contains("sys.database_event_sessions", sqlStatement);
        Assert.DoesNotContain("ON SERVER", sqlStatement);
        Assert.DoesNotContain("sys.server_event_sessions", sqlStatement);
    }

    [Fact]
    public void CreateTemplate_BothTemplates_ReturnSameDefaultView()
    {
        // Arrange
        var sqlServerTemplate = ProfilerSessionTemplateFactory.CreateTemplate(DatabaseEngineType.SqlServer);
        var azureTemplate = ProfilerSessionTemplateFactory.CreateTemplate(DatabaseEngineType.AzureSqlDatabase);

        // Act
        var sqlServerView = sqlServerTemplate.GetDefaultView();
        var azureView = azureTemplate.GetDefaultView();

        // Assert
        Assert.Equal("DefaultProfilerViewTemplate", sqlServerView);
        Assert.Equal("DefaultProfilerViewTemplate", azureView);
    }
}
