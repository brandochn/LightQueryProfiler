using LightQueryProfiler.Shared.Models;

namespace LightQueryProfiler.WinFormsApp.Views
{
    public partial class FiltersView : Form, IFiltersView
    {
        public FiltersView()
        {
            InitializeComponent();
            CreateEventHandlers();
        }

        public event EventHandler? OnApply;

        public event EventHandler? OnClose;

        public EventFilter EventFilter { get => GetEventFilter(); set => SetEventFilter(value); }

        public Form Form => this;

        private void BtnApply_Click(object? sender, EventArgs e)
        {
            OnApply?.Invoke(this, EventArgs.Empty);
        }

        private void BtnClose_Click(object? sender, EventArgs e)
        {
            OnClose?.Invoke(this, EventArgs.Empty);
        }

        private void CreateEventHandlers()
        {
            btnApply.Click += BtnApply_Click;
            btnClose.Click += BtnClose_Click;
        }

        private EventFilter GetEventFilter()
        {
            return new()
            {
                ApplicationName = txtApplicationName.Text,
                DatabaseName = txtDatabaseName.Text,
                EventClass = txtEventClass.Text,
                LoginName = txtLoginName.Text,
                NTUserName = txtNTUserName.Text,
                TextData = txtTextData.Text
            };
        }

        private void SetEventFilter(EventFilter eventFilter)
        {
            txtApplicationName.Text = eventFilter.ApplicationName;
            txtDatabaseName.Text = eventFilter.DatabaseName;
            txtEventClass.Text = eventFilter.EventClass;
            txtLoginName.Text = eventFilter.LoginName;
            txtNTUserName.Text = eventFilter.NTUserName;
            txtTextData.Text = eventFilter.TextData;
        }
    }
}