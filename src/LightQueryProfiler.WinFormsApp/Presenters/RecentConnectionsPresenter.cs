using LightQueryProfiler.Shared.Models;
using LightQueryProfiler.Shared.Repositories.Interfaces;
using LightQueryProfiler.WinFormsApp.Views;
using System.Data;

namespace LightQueryProfiler.WinFormsApp.Presenters
{
    public class RecentConnectionsPresenter
    {
        private readonly IRecentConnectionsView view;

        private readonly IRepository<Connection>? _connectionRepository;

        private Connection? _connection;

        public RecentConnectionsPresenter(IRecentConnectionsView recentConnectionsView, IRepository<Connection>? connectionRepository)
        {
            _connectionRepository = connectionRepository;
            view = recentConnectionsView;
            view.OnFormLoad += View_OnFormLoad;
            view.OnTextChange += View_OnTextChangeAsync;
            view.CellFormatting += View_RecentConnectionsGridView_CellFormatting;
            view.CellDoubleClick += View_CellDoubleClick;
        }

        private void View_CellDoubleClick(object? sender, EventArgs e)
        {
            DataGridViewCellEventArgs? dataGridViewCellEventArgs = e as DataGridViewCellEventArgs;
            if (dataGridViewCellEventArgs != null)
            {
                var row = view.RecentConnectionsGridView.Rows[dataGridViewCellEventArgs.RowIndex];
                if (row != null)
                {
                    Connection connection = new(Convert.ToInt32(row.Cells[nameof(Connection.Id)].Value),
                                                string.Empty,
                                                Convert.ToDateTime(row.Cells["Creation Date"].Value),
                                                row.Cells[nameof(Connection.DataSource)].Value?.ToString() ?? string.Empty,
                                                Convert.ToBoolean(row.Cells[nameof(Connection.IntegratedSecurity)].Value),
                                                row.Cells[nameof(Connection.Password)].Value?.ToString() ?? string.Empty,
                                                row.Cells[nameof(Connection.UserId)].Value?.ToString() ?? string.Empty
                                                );
                    SetConnection(connection);
                    view.Form.DialogResult = DialogResult.OK;
                    view.Form.Close();
                }
            }
        }

        public Connection? GetConnection()
        {
            return _connection;
        }

        private void SetConnection(Connection? connection)
        {
            if (connection != null)
            {
                _connection = connection;
            }
        }

        private async void View_OnTextChangeAsync(object? sender, EventArgs e)
        {
            await SearchGridValueAsync(view.SearchValue?.Trim() ?? "");
        }

        private async Task SearchGridValueAsync(string value)
        {
            if (_connectionRepository == null)
            {
                throw new Exception("connectionRepository cannot be null.");
            }
            var connections = await _connectionRepository.GetAllAsync() ?? new List<Connection>();
            if (connections?.Count > 0)
            {
                List<Connection> result = new List<Connection>();
                if (string.IsNullOrWhiteSpace(value))
                {
                    result = connections.ToList();
                }
                else
                {
                    foreach (var c in connections)
                    {
                        if (c.DataSource.Contains(value, StringComparison.InvariantCultureIgnoreCase) ||
                            (c.UserId?.Contains(value, StringComparison.InvariantCultureIgnoreCase) == true))
                        {
                            result.Add(c);
                        }
                    }
                }
                view.RecentConnectionsGridView.DataSource = GetDataTable(result);
            }
        }

        private async void View_OnFormLoad(object? sender, EventArgs e)
        {
            if (_connectionRepository == null)
            {
                throw new Exception("connectionRepository cannot be null.");
            }
            var connections = await _connectionRepository.GetAllAsync() ?? new List<Connection>();
            view.RecentConnectionsGridView.DataSource = GetDataTable(connections.ToList());
        }

        private void View_RecentConnectionsGridView_CellFormatting(object? sender, EventArgs e)
        {
            DataGridViewCellFormattingEventArgs? _event = e as DataGridViewCellFormattingEventArgs;
            if (_event != null)
            {
                if (view.RecentConnectionsGridView.Columns[_event.ColumnIndex].Name == nameof(Connection.Password) && _event.Value != null)
                {
                    _event.Value = new string('*', _event.Value.ToString()?.Length ?? 0);
                    _event.FormattingApplied = true;
                }
            }
        }

        private DataTable GetDataTable(List<Connection> connections)
        {
            DataTable table = new DataTable();
            table.Columns.Add(nameof(Connection.Id), typeof(int));
            table.Columns.Add(nameof(Connection.DataSource), typeof(string));
            table.Columns.Add(nameof(Connection.UserId), typeof(string));
            table.Columns.Add(nameof(Connection.Password), typeof(string));
            table.Columns.Add(nameof(Connection.IntegratedSecurity), typeof(bool));
            table.Columns.Add("Creation Date", typeof(DateTime));
            table.Columns.Add(nameof(Connection.EngineType), typeof(string));

            if (connections?.Count > 0)
            {
                foreach (var c in connections)
                {
                    DataRow row = table.NewRow();
                    row[nameof(Connection.Id)] = c.Id;
                    row[nameof(Connection.DataSource)] = c.DataSource;
                    row[nameof(Connection.UserId)] = (object?)c.UserId ?? DBNull.Value;
                    row[nameof(Connection.Password)] = (object?)c.Password ?? DBNull.Value;
                    row[nameof(Connection.IntegratedSecurity)] = c.IntegratedSecurity;
                    row["Creation Date"] = c.CreationDate;
                    row[nameof(Connection.EngineType)] = c.EngineType;
                    table.Rows.Add(row);
                }
            }

            return table;
        }
    }
}
