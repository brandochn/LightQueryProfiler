using LightQueryProfiler.WinFormsApp.Models;

namespace LightQueryProfiler.WinFormsApp.Views
{
    public interface IMainView
    {
        event EventHandler OnClearEvents;

        event EventHandler OnClearFiltersClick;

        event EventHandler OnFiltersClick;

        event EventHandler OnPause;

        event EventHandler OnResume;

        event EventHandler OnClearSearch;

        event EventHandler OnFindNext;

        event EventHandler OnStart;

        event EventHandler OnStop;

        event EventHandler RowEnter;

        event EventHandler OnRecentConnectionsClick;

        ToolStripComboBox AuthenticationComboBox { get; }
        IList<AuthenticationMode> AuthenticationModes { set; }
        string? Database { get; set; }
        /// <summary>
        /// Gets the TextBox control for database name input
        /// </summary>
        ToolStripTextBox DatabaseTextBox { get; }
        string? Password { get; set; }
        ToolStripTextBox PasswordTextBox { get; }
        ToolStripButton PauseButton { get; }
        DataGridViewColumn[] ProfilerColumns { set; }
        ListView ProfilerDetails { get; }
        DataGridView ProfilerGridView { get; }
        ToolStripButton ResumeButton { get; }
        ToolStripButton ClearSearchButton { get; }
        ToolStripButton FindNextButton { get; }
        string? SearchValue { get; set; }
        object? SelectedAuthenticationMode { get; set; }
        string? Server { get; set; }
        ToolStripTextBox ServerTexBox { get; }
        string SessionName { get; }
        string? SqlTextArea { get; set; }
        ToolStripButton StartButton { get; }
        StatusStrip StatusBar { get; }
        ToolStripButton StopButton { get; }
        string? User { get; set; }
        ToolStripTextBox UserTextBox { get; }

        void Show();
    }
}
