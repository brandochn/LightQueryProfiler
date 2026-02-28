namespace LightQueryProfiler.WinFormsApp.Helpers;

/// <summary>
/// Helper class for DataGridView operations
/// </summary>
public static class DataGridViewHelper
{
    /// <summary>
    /// Creates event data list from DataGridView rows and columns
    /// </summary>
    /// <param name="rows">DataGridView row collection</param>
    /// <param name="columns">DataGridView column collection</param>
    /// <returns>List of event dictionaries</returns>
    public static List<Dictionary<string, object?>> CreateEventDataFromGrid(
        DataGridViewRowCollection rows,
        DataGridViewColumnCollection columns)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(columns);

        var events = new List<Dictionary<string, object?>>();

        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];

            // Skip new row placeholder
            if (row.IsNewRow)
            {
                continue;
            }

            var eventData = new Dictionary<string, object?>();

            // Extract all column values
            foreach (DataGridViewColumn column in columns)
            {
                var cellValue = row.Cells[column.Index].Value;
                var columnName = column.Name;

                // Store the value (convert to string for consistency)
                eventData[columnName] = cellValue?.ToString();
            }

            events.Add(eventData);
        }

        if (events.Count == 0)
        {
            throw new InvalidOperationException("No valid events to export");
        }

        return events;
    }
}
