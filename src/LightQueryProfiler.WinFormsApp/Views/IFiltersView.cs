using LightQueryProfiler.Shared.Models;

namespace LightQueryProfiler.WinFormsApp.Views
{
    public interface IFiltersView
    {
        event EventHandler? OnApply;

        event EventHandler? OnClose;

        EventFilter EventFilter { get; set; }

        Form Form { get; }
    }
}