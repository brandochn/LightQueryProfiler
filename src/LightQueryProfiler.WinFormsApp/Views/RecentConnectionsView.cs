using LightQueryProfiler.Shared.Models;

namespace LightQueryProfiler.WinFormsApp.Views
{
    public partial class RecentConnectionsView : Form, IRecentConnectionsView
    {
        public RecentConnectionsView()
        {
            InitializeComponent();
            CreateEventHandlers();
        }

        public event EventHandler? CellFormatting;

        public event EventHandler? OnFormLoad;

        public event EventHandler? OnTextChange;

        public event EventHandler? CellDoubleClick;

        public DataGridView RecentConnectionsGridView => dgvConnections;

        string? IRecentConnectionsView.SearchValue { get => txtSearch.Text; set => txtSearch.Text = value; }

        public Form Form => this;

        private void CreateEventHandlers()
        {
            Load += RecentConnectionsView_Load;
            dgvConnections.CellFormatting += DgvConnections_CellFormatting;
            txtSearch.KeyUp += TxtSearch_KeyUp;
            dgvConnections.CellDoubleClick += DgvConnections_CellDoubleClick;
        }

        private void DgvConnections_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            CellDoubleClick?.Invoke(this, e);
        }

        private void TxtSearch_KeyUp(object? sender, KeyEventArgs e)
        {
            OnTextChange?.Invoke(this, EventArgs.Empty);
        }

        private void DgvConnections_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            CellFormatting?.Invoke(this, e);
        }

        private void RecentConnectionsView_Load(object? sender, EventArgs e)
        {
            OnFormLoad?.Invoke(this, EventArgs.Empty);
        }
    }
}
