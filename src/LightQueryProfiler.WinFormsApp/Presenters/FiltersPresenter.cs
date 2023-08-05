using LightQueryProfiler.Shared.Models;
using LightQueryProfiler.WinFormsApp.Views;

namespace LightQueryProfiler.WinFormsApp.Presenters
{
    public class FiltersPresenter
    {
        private readonly IFiltersView view;

        public FiltersPresenter(IFiltersView filtersView)
        {
            view = filtersView;
            view.OnApply += OnApply;
            view.OnClose += OnClose;
        }

        public EventFilter GetEventFilter() => view.EventFilter;

        public void SetEventFilter(EventFilter eventFilter)
        {
            view.EventFilter = eventFilter;
        }
        private void OnApply(object? sender, EventArgs e)
        {
            view.Form.DialogResult = DialogResult.OK;
            view.Form.Close();
        }

        private void OnClose(object? sender, EventArgs e)
        {
            view.Form.DialogResult = DialogResult.Cancel;
            view.Form.Close();
        }
    }
}