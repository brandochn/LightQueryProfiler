using LightQueryProfiler.WinFormsApp.Models;

namespace LightQueryProfiler.WinFormsApp.Views
{
    public interface IMainView
    {
        event EventHandler OnPause;

        event EventHandler OnResume;

        event EventHandler OnStart;

        event EventHandler OnStop;
        event EventHandler RowEnter;

        IList<AuthenticationMode> AuthenticationModes { set; }
        string? Password { get; set; }
        Button PauseButton { get; }
        DataGridViewColumn[] ProfilerColumns { set; }
        ListView ProfilerDetails { get; }
        DataGridView ProfilerGridView { get; }
        Button ResumeButton { get; }
        object? SelectedAuthenticationMode { get; set; }
        string? Server { get; set; }
        string SessionName { get; }
        string? SqlTextArea { get; set; }
        Button StartButton { get; }
        Button StopButton { get; }
        string? User { get; set; }

        void Show();
    }
}