namespace LightQueryProfiler.WinFormsApp.Views
{
    public interface IRecentConnectionsView
    {
        event EventHandler? OnFormLoad;

        event EventHandler? OnTextChange;

        event EventHandler? CellFormatting;

        event EventHandler? CellDoubleClick;

        string? SearchValue { get; set; }

        DataGridView RecentConnectionsGridView { get; }

        Form Form { get; }
    }
}