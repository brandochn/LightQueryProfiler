using System.Text.Json;

namespace LightQueryProfiler.Shared.Services;

/// <summary>
/// Service for exporting and importing profiler events to/from JSON files
/// </summary>
public class EventExportImportService
{
    private const string RowIndexField = "__RowIndex";
    private const string TimestampField = "__Timestamp";
    private const string MetadataPrefix = "__";

    private static readonly JsonSerializerOptions ExportJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Exports events to JSON file maintaining row order
    /// </summary>
    /// <param name="events">List of event data dictionaries to export</param>
    /// <param name="filePath">Destination file path for JSON export</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static async Task ExportEventsAsync(
        List<Dictionary<string, object?>> events,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
        }

        if (events.Count == 0)
        {
            throw new InvalidOperationException("No events to export");
        }

        List<Dictionary<string, object?>> eventsToExport = [];

        for (int rowIndex = 0; rowIndex < events.Count; rowIndex++)
        {
            var eventData = new Dictionary<string, object?>(events[rowIndex])
            {
                // Add row index for preserving order
                [RowIndexField] = rowIndex
            };

            // If timestamp exists, also store it as metadata for alternative sorting
            if (eventData.TryGetValue("Timestamp", out var timestamp) && timestamp != null)
            {
                eventData[TimestampField] = timestamp.ToString();
            }

            eventsToExport.Add(eventData);
        }

        await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        await JsonSerializer.SerializeAsync(fileStream, eventsToExport, ExportJsonOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Imports events from JSON file
    /// </summary>
    /// <param name="filePath">Source JSON file path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Import result containing events and column names</returns>
    public static async Task<ImportResult> ImportEventsAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Import file not found", filePath);
        }

        List<Dictionary<string, object?>> events;

        try
        {
            await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);

            var jsonDocument = await JsonDocument.ParseAsync(fileStream, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (jsonDocument.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new JsonException("JSON file must contain an array of events");
            }

            events = [];

            foreach (var element in jsonDocument.RootElement.EnumerateArray())
            {
                var eventData = new Dictionary<string, object?>();

                foreach (var property in element.EnumerateObject())
                {
                    eventData[property.Name] = property.Value.ValueKind switch
                    {
                        JsonValueKind.String => property.Value.GetString(),
                        JsonValueKind.Number => property.Value.GetRawText(),
                        JsonValueKind.True => "True",
                        JsonValueKind.False => "False",
                        JsonValueKind.Null => null,
                        _ => property.Value.GetRawText()
                    };
                }

                events.Add(eventData);
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Invalid JSON format in import file", ex);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException("Error reading import file", ex);
        }

        if (events.Count == 0)
        {
            throw new InvalidOperationException("No events found in import file");
        }

        // Sort events by __RowIndex if available, otherwise by __Timestamp, otherwise keep original order
        var sortedEvents = events
            .Select((e, index) => new { Event = e, OriginalIndex = index })
            .OrderBy(x =>
            {
                if (x.Event.TryGetValue(RowIndexField, out var rowIndex) && int.TryParse(rowIndex?.ToString(), out var idx))
                {
                    return idx;
                }
                return x.OriginalIndex;
            })
            .Select(x => x.Event)
            .ToList();

        // Extract column names (exclude metadata fields starting with __)
        var columnNames = new List<string>();
        var allKeys = sortedEvents
            .SelectMany(e => e.Keys)
            .Distinct()
            .Where(key => !key.StartsWith(MetadataPrefix))
            .ToList();

        // Preserve column order from first event, then add any additional columns
        if (sortedEvents.Count > 0)
        {
            var firstEventColumns = sortedEvents[0].Keys
                .Where(key => !key.StartsWith(MetadataPrefix))
                .ToList();

            columnNames.AddRange(firstEventColumns);

            var remainingColumns = allKeys.Except(columnNames).ToList();
            columnNames.AddRange(remainingColumns);
        }

        return new ImportResult
        {
            Events = sortedEvents,
            ColumnNames = columnNames
        };
    }
}

/// <summary>
/// Result of import operation
/// </summary>
public class ImportResult
{
    /// <summary>
    /// Imported events in correct order
    /// </summary>
    public required List<Dictionary<string, object?>> Events { get; init; }

    /// <summary>
    /// Column names extracted from events (excluding metadata fields)
    /// </summary>
    public required List<string> ColumnNames { get; init; }
}
