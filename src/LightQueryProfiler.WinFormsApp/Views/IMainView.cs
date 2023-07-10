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

        ComboBox AuthenticationComboBox { get; }

        IList<AuthenticationMode> AuthenticationModes { set; }
        string? Password { get; set; }
        TextBox PasswordTextBox { get; }
        Button PauseButton { get; }
        DataGridViewColumn[] ProfilerColumns { set; }
        ListView ProfilerDetails { get; }
        DataGridView ProfilerGridView { get; }
        Button ResumeButton { get; }
        object? SelectedAuthenticationMode { get; set; }
        string? Server { get; set; }
        TextBox ServerTexBox { get; }
        string SessionName { get; }
        string? SqlTextArea { get; set; }
        Button StartButton { get; }
        Button StopButton { get; }
        string? User { get; set; }
        TextBox UserTextBox { get; }

        void Show();
    }
}