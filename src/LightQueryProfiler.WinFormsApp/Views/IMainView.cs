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

        event EventHandler OnStart;

        event EventHandler OnStop;

        event EventHandler RowEnter;

        event EventHandler OnSearch;

        ToolStripComboBox AuthenticationComboBox { get; }
        IList<AuthenticationMode> AuthenticationModes { set; }
        string? Password { get; set; }
        ToolStripTextBox PasswordTextBox { get; }
        ToolStripButton PauseButton { get; }
        DataGridViewColumn[] ProfilerColumns { set; }
        ListView ProfilerDetails { get; }
        DataGridView ProfilerGridView { get; }
        ToolStripButton ResumeButton { get; }
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
        string? SearchValue { get; set; }
        ToolStripButton SearchButton { get; }


        void Show();
    }
}