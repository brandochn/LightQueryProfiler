using System.Text.Json;
using LightQueryProfiler.Shared.Services;

namespace LightQueryProfiler.Shared.UnitTests.Services;

public class EventExportImportServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly EventExportImportService _service;
    private readonly List<string> _filesToCleanup;

    public EventExportImportServiceTests()
    {
        _service = new EventExportImportService();
        _testDirectory = Path.Combine(Path.GetTempPath(), $"EventExportImportTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _filesToCleanup = new List<string>();
    }

    public void Dispose()
    {
        foreach (var file in _filesToCleanup)
        {
            if (File.Exists(file))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task ExportEventsAsync_WhenEventsAreNull_ThrowsArgumentNullException()
    {
        // Arrange
        var filePath = GetTestFilePath();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            EventExportImportService.ExportEventsAsync(null!, filePath));
    }

    [Fact]
    public async Task ExportEventsAsync_WhenFilePathIsEmpty_ThrowsArgumentException()
    {
        // Arrange
        var events = CreateSampleEventData();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            EventExportImportService.ExportEventsAsync(events, string.Empty));
    }

    [Fact]
    public async Task ExportEventsAsync_WhenNoEvents_ThrowsInvalidOperationException()
    {
        // Arrange
        var filePath = GetTestFilePath();
        var emptyEvents = new List<Dictionary<string, object?>>();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            EventExportImportService.ExportEventsAsync(emptyEvents, filePath));
    }

    [Fact]
    public async Task ExportEventsAsync_WithValidData_CreatesJsonFile()
    {
        // Arrange
        var filePath = GetTestFilePath();
        var events = CreateSampleEventData();

        // Act
        await EventExportImportService.ExportEventsAsync(events, filePath);

        // Assert
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public async Task ExportEventsAsync_PreservesRowIndex()
    {
        // Arrange
        var filePath = GetTestFilePath();
        var events = CreateSampleEventData();

        // Act
        await EventExportImportService.ExportEventsAsync(events, filePath);

        // Assert
        var jsonContent = await File.ReadAllTextAsync(filePath);
        var exportedEvents = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(jsonContent);

        Assert.NotNull(exportedEvents);
        Assert.Equal(3, exportedEvents.Count);
        Assert.Equal(0, exportedEvents[0]["__RowIndex"].GetInt32());
        Assert.Equal(1, exportedEvents[1]["__RowIndex"].GetInt32());
        Assert.Equal(2, exportedEvents[2]["__RowIndex"].GetInt32());
    }

    [Fact]
    public async Task ExportEventsAsync_PreservesAllColumnData()
    {
        // Arrange
        var filePath = GetTestFilePath();
        var events = CreateSampleEventData();

        // Act
        await EventExportImportService.ExportEventsAsync(events, filePath);

        // Assert
        var jsonContent = await File.ReadAllTextAsync(filePath);
        var exportedEvents = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(jsonContent);

        Assert.NotNull(exportedEvents);
        Assert.Equal("sql_batch_completed", exportedEvents[0]["EventName"].GetString());
        Assert.Equal("1234", exportedEvents[0]["Duration"].GetString());
        Assert.Equal("100", exportedEvents[0]["CPU"].GetString());
    }

    [Fact]
    public async Task ImportEventsAsync_WhenFilePathIsNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            EventExportImportService.ImportEventsAsync(null!));
    }

    [Fact]
    public async Task ImportEventsAsync_WhenFileDoesNotExist_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDirectory, "nonexistent.json");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            EventExportImportService.ImportEventsAsync(nonExistentPath));
    }

    [Fact]
    public async Task ImportEventsAsync_WithInvalidJson_ThrowsInvalidOperationException()
    {
        // Arrange
        var filePath = GetTestFilePath();
        await File.WriteAllTextAsync(filePath, "{ invalid json }");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            EventExportImportService.ImportEventsAsync(filePath));
    }

    [Fact]
    public async Task ImportEventsAsync_WithNonArrayJson_ThrowsInvalidOperationException()
    {
        // Arrange
        var filePath = GetTestFilePath();
        await File.WriteAllTextAsync(filePath, """{"key": "value"}""");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            EventExportImportService.ImportEventsAsync(filePath));
    }

    [Fact]
    public async Task ImportEventsAsync_WithEmptyArray_ThrowsInvalidOperationException()
    {
        // Arrange
        var filePath = GetTestFilePath();
        await File.WriteAllTextAsync(filePath, "[]");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            EventExportImportService.ImportEventsAsync(filePath));
    }

    [Fact]
    public async Task ImportEventsAsync_WithValidData_ReturnsCorrectEventCount()
    {
        // Arrange
        var filePath = GetTestFilePath();
        var json = """
        [
            {"EventName": "sql_batch_completed", "Duration": "1234"},
            {"EventName": "rpc_completed", "Duration": "5678"}
        ]
        """;
        await File.WriteAllTextAsync(filePath, json);

        // Act
        var result = await EventExportImportService.ImportEventsAsync(filePath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Events.Count);
    }

    [Fact]
    public async Task ImportEventsAsync_ExcludesMetadataFieldsFromColumns()
    {
        // Arrange
        var filePath = GetTestFilePath();
        var json = """
        [
            {"__RowIndex": 0, "__Timestamp": "2024-01-01", "EventName": "test", "Duration": "100"}
        ]
        """;
        await File.WriteAllTextAsync(filePath, json);

        // Act
        var result = await EventExportImportService.ImportEventsAsync(filePath);

        // Assert
        Assert.NotNull(result.ColumnNames);
        Assert.DoesNotContain("__RowIndex", result.ColumnNames);
        Assert.DoesNotContain("__Timestamp", result.ColumnNames);
        Assert.Contains("EventName", result.ColumnNames);
        Assert.Contains("Duration", result.ColumnNames);
    }

    [Fact]
    public async Task ImportEventsAsync_SortsEventsByRowIndex()
    {
        // Arrange
        var filePath = GetTestFilePath();
        var json = """
        [
            {"__RowIndex": 2, "EventName": "Third"},
            {"__RowIndex": 0, "EventName": "First"},
            {"__RowIndex": 1, "EventName": "Second"}
        ]
        """;
        await File.WriteAllTextAsync(filePath, json);

        // Act
        var result = await EventExportImportService.ImportEventsAsync(filePath);

        // Assert
        Assert.Equal("First", result.Events[0]["EventName"]);
        Assert.Equal("Second", result.Events[1]["EventName"]);
        Assert.Equal("Third", result.Events[2]["EventName"]);
    }

    [Fact]
    public async Task ImportEventsAsync_WithoutRowIndex_MaintainsOriginalOrder()
    {
        // Arrange
        var filePath = GetTestFilePath();
        var json = """
        [
            {"EventName": "First"},
            {"EventName": "Second"},
            {"EventName": "Third"}
        ]
        """;
        await File.WriteAllTextAsync(filePath, json);

        // Act
        var result = await EventExportImportService.ImportEventsAsync(filePath);

        // Assert
        Assert.Equal("First", result.Events[0]["EventName"]);
        Assert.Equal("Second", result.Events[1]["EventName"]);
        Assert.Equal("Third", result.Events[2]["EventName"]);
    }

    [Fact]
    public async Task ExportImport_PreservesDataAndOrder()
    {
        // Arrange
        var exportPath = GetTestFilePath();
        var originalEvents = CreateSampleEventData();

        // Act - Export
        await EventExportImportService.ExportEventsAsync(originalEvents, exportPath);

        // Act - Import
        var importResult = await EventExportImportService.ImportEventsAsync(exportPath);

        // Assert - Verify count
        Assert.Equal(3, importResult.Events.Count);

        // Assert - Verify column names
        Assert.Contains("EventName", importResult.ColumnNames);
        Assert.Contains("Duration", importResult.ColumnNames);
        Assert.Contains("CPU", importResult.ColumnNames);

        // Assert - Verify first row data
        Assert.Equal("sql_batch_completed", importResult.Events[0]["EventName"]);
        Assert.Equal("1234", importResult.Events[0]["Duration"]);
        Assert.Equal("100", importResult.Events[0]["CPU"]);

        // Assert - Verify order is maintained
        Assert.Equal("rpc_completed", importResult.Events[1]["EventName"]);
        Assert.Equal("sp_execute", importResult.Events[2]["EventName"]);
    }

    [Fact]
    public async Task ExportImportExport_ProducesSameDataOrder()
    {
        // Arrange
        var exportPath1 = GetTestFilePath();
        var exportPath2 = GetTestFilePath();
        var originalEvents = CreateSampleEventData();

        // Act - First Export
        await EventExportImportService.ExportEventsAsync(originalEvents, exportPath1);

        // Act - Import
        var importResult = await EventExportImportService.ImportEventsAsync(exportPath1);

        // Act - Convert import result back to event data
        var reimportedEvents = importResult.Events
            .Select(e => new Dictionary<string, object?>(e))
            .ToList();

        // Act - Second Export
        await EventExportImportService.ExportEventsAsync(reimportedEvents, exportPath2);

        // Assert - Both JSON files should have same structure and order
        var json1 = await File.ReadAllTextAsync(exportPath1);
        var json2 = await File.ReadAllTextAsync(exportPath2);

        var events1 = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json1);
        var events2 = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json2);

        Assert.Equal(events1!.Count, events2!.Count);

        for (int i = 0; i < events1.Count; i++)
        {
            Assert.Equal(events1[i]["EventName"].GetString(), events2[i]["EventName"].GetString());
            Assert.Equal(events1[i]["Duration"].GetString(), events2[i]["Duration"].GetString());
            Assert.Equal(events1[i]["CPU"].GetString(), events2[i]["CPU"].GetString());
        }
    }

    [Fact]
    public async Task ImportEventsAsync_HandlesVariousJsonDataTypes()
    {
        // Arrange
        var filePath = GetTestFilePath();
        var json = """
        [
            {
                "EventName": "test",
                "Duration": 1234,
                "IsCompleted": true,
                "ErrorCount": null
            }
        ]
        """;
        await File.WriteAllTextAsync(filePath, json);

        // Act
        var result = await EventExportImportService.ImportEventsAsync(filePath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Events.Count);
        Assert.Equal("test", result.Events[0]["EventName"]);
        Assert.Equal("1234", result.Events[0]["Duration"]);
        Assert.Equal("True", result.Events[0]["IsCompleted"]);
        Assert.Null(result.Events[0]["ErrorCount"]);
    }

    [Fact]
    public async Task ImportEventsAsync_PreservesColumnOrderFromFirstEvent()
    {
        // Arrange
        var filePath = GetTestFilePath();
        var json = """
        [
            {"Column3": "C", "Column1": "A", "Column2": "B"},
            {"Column1": "D", "Column2": "E", "Column3": "F"}
        ]
        """;
        await File.WriteAllTextAsync(filePath, json);

        // Act
        var result = await EventExportImportService.ImportEventsAsync(filePath);

        // Assert
        Assert.Equal("Column3", result.ColumnNames[0]);
        Assert.Equal("Column1", result.ColumnNames[1]);
        Assert.Equal("Column2", result.ColumnNames[2]);
    }

    [Fact]
    public async Task ImportEventsAsync_HandlesEventsWithDifferentColumns()
    {
        // Arrange
        var filePath = GetTestFilePath();
        var json = """
        [
            {"Column1": "A", "Column2": "B"},
            {"Column1": "C", "Column3": "D"}
        ]
        """;
        await File.WriteAllTextAsync(filePath, json);

        // Act
        var result = await EventExportImportService.ImportEventsAsync(filePath);

        // Assert
        Assert.Contains("Column1", result.ColumnNames);
        Assert.Contains("Column2", result.ColumnNames);
        Assert.Contains("Column3", result.ColumnNames);

        // First event should have Column2 value
        Assert.Equal("B", result.Events[0]["Column2"]);

        // Second event should not have Column2 in dictionary or it should be null
        Assert.True(!result.Events[1].ContainsKey("Column2") || result.Events[1]["Column2"] == null);
    }

    private string GetTestFilePath()
    {
        var fileName = $"test_{Guid.NewGuid()}.json";
        var filePath = Path.Combine(_testDirectory, fileName);
        _filesToCleanup.Add(filePath);
        return filePath;
    }

    private List<Dictionary<string, object?>> CreateSampleEventData()
    {
        return new List<Dictionary<string, object?>>
        {
            new()
            {
                ["EventName"] = "sql_batch_completed",
                ["Duration"] = "1234",
                ["CPU"] = "100",
                ["Reads"] = "500",
                ["Writes"] = "10"
            },
            new()
            {
                ["EventName"] = "rpc_completed",
                ["Duration"] = "5678",
                ["CPU"] = "200",
                ["Reads"] = "800",
                ["Writes"] = "20"
            },
            new()
            {
                ["EventName"] = "sp_execute",
                ["Duration"] = "9012",
                ["CPU"] = "300",
                ["Reads"] = "1200",
                ["Writes"] = "30"
            }
        };
    }
}
