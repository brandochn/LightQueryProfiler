using LightQueryProfiler.Highlight.Configuration;
using LightQueryProfiler.Highlight.Engines;
using LightQueryProfiler.Shared.Data;
using LightQueryProfiler.Shared.Extensions;
using LightQueryProfiler.Shared.Models;
using LightQueryProfiler.Shared.Repositories;
using LightQueryProfiler.Shared.Repositories.Interfaces;
using LightQueryProfiler.Shared.Services;
using LightQueryProfiler.Shared.Services.Interfaces;
using LightQueryProfiler.WinFormsApp.Data;
using LightQueryProfiler.WinFormsApp.Views;
using Microsoft.Data.SqlClient;

namespace LightQueryProfiler.WinFormsApp.Presenters
{
    public class MainPresenter
    {
        private const int EventsPollingIntervalMs = 900;

        private const string htmlDocument = @" <!DOCTYPE html>
                                                <html>

                                                <head> </head>

                                                <body>
                                                    <div>
                                                        <pre> <code>{0}</code> </pre>
                                                    </div>
                                                </body>

                                                </html> ";

        private readonly IMainView view;
        private IApplicationDbContext? _applicationDbContext;

        private IRepository<Connection>? _connectionRepository;
        private IProfilerService? _profilerService;
        private bool _shouldStop = true;
        private SqlHighlightService? _sqlHighlightService;
        private Thread? _thread;
        private CancellationTokenSource? _tokenSource;
        private IXEventRepository? _xEventRepository;
        private IXEventService? _xEventService;
        private Dictionary<string, ProfilerEvent> CurrentRows = new();
        private Dictionary<string, object>? Filters;
        private int currentIndex = 0;

        public MainPresenter(IMainView mainView)
        {
            view = mainView;
            SetAuthenticationModes();
            ShowButtonsByAction("default");
            SetProfilerColumns();
            view.OnStart += OnStart;
            view.OnStop += OnStop;
            view.RowEnter += RowEnter;
            view.OnPause += OnPause;
            view.OnResume += OnResume;
            view.OnClearEvents += OnClearEvents;
            view.OnFiltersClick += OnFiltersClick;
            view.OnClearFiltersClick += OnClearFiltersClick;
            view.OnClearSearch += OnClearSearch;
            view.OnFindNext += OnNextSearch;
            view.OnRecentConnectionsClick += OnRecentConnectionsClick;
            _connectionRepository = new ConnectionRepository(new SqliteContext());
            view.Show();
        }

        private EventFilter EventFilterModel { get; set; } = new EventFilter();

        private BaseProfilerViewTemplate ProfilerViewTemplate { get; set; } = new DefaultProfilerViewTemplate();

        public void SetAuthenticationModes()
        {
            IList<Models.AuthenticationMode> result = new List<Models.AuthenticationMode>();
            List<Shared.Enums.AuthenticationMode> authenticationModes = Enum.GetValues(typeof(Shared.Enums.AuthenticationMode)).Cast<Shared.Enums.AuthenticationMode>().ToList();
            foreach (var am in authenticationModes)
            {
                result.Add(new Models.AuthenticationMode(am.GetString(), (int)am));
            }

            view.AuthenticationModes = result;
        }

        public void SetProfilerColumns()
        {
            List<DataGridViewColumn> columns = new List<DataGridViewColumn>();
            foreach (BaseColumnViewTemplate c in ProfilerViewTemplate.Columns)
            {
                DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn
                {
                    HeaderText = c.Name,
                    ReadOnly = true,
                    Name = c.Name,
                    DataPropertyName = c.Name,
                };
                columns.Add(column);
            }

            view.ProfilerColumns = columns.ToArray();
        }

        private void ClearEvents()
        {
            view.ProfilerGridView.Rows.Clear();
        }

        private void ClearFilters()
        {
            DialogResult dialogResult = MessageBox.Show("Are you sure you want to clear the filters?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (dialogResult == DialogResult.Yes)
            {
                Filters = new Dictionary<string, object>();
                EventFilterModel = new Shared.Models.EventFilter();
            }
        }

        private void ClearResults()
        {
            ClearEvents();
            view.SqlTextArea = string.Empty;
            view.ProfilerDetails.Items.Clear();
            _shouldStop = false;
            CurrentRows = new();
        }

        private void Configure()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
            int selectedAuthenticationMode = Convert.ToInt32(view.SelectedAuthenticationMode);
            if ((Shared.Enums.AuthenticationMode)selectedAuthenticationMode == Shared.Enums.AuthenticationMode.WindowsAuth)
            {
                builder.IntegratedSecurity = true;
                view.User = string.Empty;
                view.Password = string.Empty;
            }

            builder.TrustServerCertificate = true;
            builder.DataSource = view.Server;
            builder.InitialCatalog = "master";
            builder.UserID = view.User;
            builder.Password = view.Password;
            builder.ApplicationName = "LightQueryProfiler";

            _applicationDbContext = new ApplicationDbContext(builder.ConnectionString);
            _xEventRepository = new XEventRepository(_applicationDbContext);
            _xEventService = new XEventService();
            _profilerService = new ProfilerService(_xEventRepository, _xEventService);
            // Linux OS or Mac OSX don't have the Arial Font so here we use a specific font for each OS
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Unix:
                    _sqlHighlightService = new SqlHighlightService(new HtmlEngine(), new LinuxConfiguration());
                    break;

                case PlatformID.MacOSX:
                    _sqlHighlightService = new SqlHighlightService(new HtmlEngine(), new LinuxConfiguration()); // check if this font works on Mac OS
                    break;

                default:
                    _sqlHighlightService = new SqlHighlightService(new HtmlEngine(), new DefaultConfiguration());
                    break;
            }
        }

        private void CreateFilters(Shared.Models.EventFilter eventFilter)
        {
            Filters = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(eventFilter.EventClass))
            {
                Filters.Add(nameof(eventFilter.EventClass), eventFilter.EventClass);
            }

            if (!string.IsNullOrEmpty(eventFilter.TextData))
            {
                Filters.Add(nameof(eventFilter.TextData), eventFilter.TextData);
            }

            if (!string.IsNullOrEmpty(eventFilter.ApplicationName))
            {
                Filters.Add(nameof(eventFilter.ApplicationName), eventFilter.ApplicationName);
            }

            if (!string.IsNullOrEmpty(eventFilter.NTUserName))
            {
                Filters.Add(nameof(eventFilter.NTUserName), eventFilter.NTUserName);
            }

            if (!string.IsNullOrEmpty(eventFilter.LoginName))
            {
                Filters.Add(nameof(eventFilter.LoginName), eventFilter.LoginName);
            }

            if (!string.IsNullOrEmpty(eventFilter.DatabaseName))
            {
                Filters.Add(nameof(eventFilter.DatabaseName), eventFilter.DatabaseName);
            }
        }

        private void CreateRowDetails(DataGridViewRow dataGridViewRow)
        {
            if (dataGridViewRow != null)
            {
                view.ProfilerDetails.Items.Clear();
                string[] row;
                List<ListViewItem> items = new();
                foreach (BaseColumnViewTemplate c in ProfilerViewTemplate.Columns)
                {
                    row = new string[] { c.Name, dataGridViewRow.Cells[c.Name].Value?.ToString() ?? string.Empty };
                    ListViewItem listViewItem = new ListViewItem(row);
                    items.Add(listViewItem);
                }

                view.ProfilerDetails.Items.AddRange(items.ToArray());
            }
        }

        /// <summary>
        /// Filters rows based on the active filters. All filters must match for a row to be included.
        /// </summary>
        private List<Dictionary<string, Event>> FilterRows(List<Dictionary<string, Event>> rows)
        {
            // If no filters or no rows, return the original list
            if (Filters?.Count == 0 || rows?.Count == 0)
            {
                return rows ?? new List<Dictionary<string, Event>>();
            }

            List<Dictionary<string, Event>> result = new List<Dictionary<string, Event>>();

            foreach (Dictionary<string, Event> row in rows)
            {
                bool matchesAllFilters = true;

                // Check if ALL filters match (AND logic)
                foreach (KeyValuePair<string, object> filter in Filters)
                {
                    if (!row.TryGetValue(filter.Key, out Event? eventData))
                    {
                        matchesAllFilters = false;
                        break;
                    }

                    string filterValue = (filter.Value?.ToString() ?? string.Empty).Trim();
                    string? eventValue = eventData.EventValue?.ToString();

                    if (string.IsNullOrEmpty(eventValue) ||
                        !eventValue.Contains(filterValue, StringComparison.OrdinalIgnoreCase))
                    {
                        matchesAllFilters = false;
                        break;
                    }
                }

                if (matchesAllFilters)
                {
                    result.Add(row);
                }
            }

            return result;
        }

        /// <summary>
        /// Retrieves and displays the latest profiler events
        /// </summary>
        private async Task GetLastEventsInternalAsync(CancellationToken cancellationToken = default)
        {
            if (_profilerService == null)
            {
                return;
            }

            try
            {
                await Task.Delay(EventsPollingIntervalMs, cancellationToken);

                List<ProfilerEvent>? events = await _profilerService.GetLastEventsAsync(view.SessionName);
                if (events?.Count == 0)
                {
                    return;
                }

                List<Dictionary<string, Event>> newRows = GetNewRows(events);
                if (newRows?.Count == 0)
                {
                    return;
                }

                List<Dictionary<string, Event>> filteredRows = FilterRows(newRows);

                // Update UI in a single invoke
                view.ProfilerGridView.Invoke(() =>
                {
                    int lastRowId = 0;

                    foreach (Dictionary<string, Event> row in filteredRows)
                    {
                        lastRowId = view.ProfilerGridView.Rows.Add();
                        DataGridViewRow gridRow = view.ProfilerGridView.Rows[lastRowId];

                        foreach (BaseColumnViewTemplate column in ProfilerViewTemplate.Columns)
                        {
                            gridRow.Cells[column.Name].Value = row[column.Name].EventValue;
                        }
                    }

                    // Update status bar
                    view.StatusBar.Items[0].Text = $"Events: {view.ProfilerGridView.Rows.Count}";

                    // Scroll to the last added row
                    if (lastRowId > 0)
                    {
                        view.ProfilerGridView.FirstDisplayedScrollingRowIndex = lastRowId;
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested, don't propagate
                throw;
            }
            catch (Exception ex)
            {
                // Log or handle unexpected errors
                throw new InvalidOperationException("Error retrieving profiler events", ex);
            }
        }

        private List<Dictionary<string, Event>> GetNewRows(List<ProfilerEvent> events)
        {
            List<Dictionary<string, Event>> newEvents = new List<Dictionary<string, Event>>();
            Dictionary<string, Event> data;
            foreach (var e in events)
            {
                if (!CurrentRows.ContainsKey(e.GetEventKey()))
                {
                    data = new Dictionary<string, Event>();

                    foreach (BaseColumnViewTemplate c in ProfilerViewTemplate.Columns)
                    {
                        if (c.Name == "EventClass")
                        {
                            data["EventClass"] = new Event() { EventValue = e.Name ?? string.Empty, Name = "EventClass" };
                            continue;
                        }

                        if (c.Name == "StartTime")
                        {
                            data["StartTime"] = new Event() { EventValue = e.Timestamp ?? string.Empty, Name = "StartTime" };
                            continue;
                        }

                        string columnName = c.Name;
                        object columnValue = string.Empty;

                        if (e.Actions?.Any(a => c.EventsMapped.Contains(a.Key)) ?? false)
                        {
                            columnValue = e.Actions.FirstOrDefault(a => c.EventsMapped.Contains(a.Key)).Value ?? string.Empty;
                        }
                        else
                        {
                            if (e.Fields?.Any(f => c.EventsMapped.Contains(f.Key)) ?? false)
                            {
                                columnValue = e.Fields.FirstOrDefault(f => c.EventsMapped.Contains(f.Key)).Value ?? string.Empty;
                            }
                        }

                        data[columnName] = new Event() { EventValue = columnValue, Name = columnName };
                    }

                    newEvents.Add(data);
                    SetCurrentRows(e);
                }
            }

            return newEvents;
        }

        private void HandleCancellationRequest()
        {
            if (_tokenSource != null && !_tokenSource.IsCancellationRequested)
            {
                _tokenSource?.Cancel(); // Request cancellation.
                _thread?.Join(); // If you want to wait for cancellation, `Join` blocks the calling thread until the thread represented by this instance terminates.
                _tokenSource?.Dispose(); // Dispose the token source.
            }
        }

        private void OnClearEvents(object? sender, EventArgs e)
        {
            ClearEvents();
        }

        private void OnClearFiltersClick(object? sender, EventArgs e)
        {
            ClearFilters();
        }

        private void OnFiltersClick(object? sender, EventArgs e)
        {
            using (var form = new FiltersView())
            {
                var presenter = new FiltersPresenter(form);
                presenter.SetEventFilter(EventFilterModel);
                var result = form.ShowDialog();
                if (result == DialogResult.OK)
                {
                    EventFilterModel = presenter.GetEventFilter();
                    CreateFilters(EventFilterModel);
                    ClearEvents();
                }
            }
        }

        private void OnPause(object? sender, EventArgs e)
        {
            _shouldStop = true;
            ShowButtonsByAction("pause");
            HandleCancellationRequest();
        }

        private void OnRecentConnectionsClick(object? sender, EventArgs e)
        {
            using (var form = new RecentConnectionsView())
            {
                var presenter = new RecentConnectionsPresenter(form, _connectionRepository);
                var result = form.ShowDialog();
                if (result == DialogResult.OK)
                {
                    var connection = presenter.GetConnection();
                    if (connection != null)
                    {
                        view.Server = connection.DataSource;
                        if (connection.IntegratedSecurity)
                        {
                            view.User = string.Empty;
                            view.Password = string.Empty;
                            view.SelectedAuthenticationMode = Shared.Enums.AuthenticationMode.WindowsAuth;
                            view.AuthenticationComboBox.SelectedIndex = 0;
                        }
                        else
                        {
                            view.User = connection.UserId;
                            view.Password = connection.Password;
                            view.SelectedAuthenticationMode = Shared.Enums.AuthenticationMode.SQLServerAuth;
                            view.AuthenticationComboBox.SelectedIndex = 1;
                        }
                    }
                }
            }
        }

        private void OnResume(object? sender, EventArgs e)
        {
            _shouldStop = false;
            ShowButtonsByAction("resume");
            _tokenSource = new CancellationTokenSource();
            _thread = new Thread(() => StartGetLastEvents(_tokenSource.Token)) { IsBackground = true };
            _thread.Start();
        }

        private void OnClearSearch(object? sender, EventArgs e)
        {
            view.SearchValue = string.Empty;
            currentIndex = 0;
            view.ProfilerGridView.ClearSelection();
        }

        private void OnNextSearch(object? sender, EventArgs e)
        {
            try
            {
                if (view.ProfilerGridView.Rows.Count > 0)
                {
                    if (string.IsNullOrEmpty(view.SearchValue))
                    {
                        MessageBox.Show("Please enter a search value.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    currentIndex = FindGridValue(view.SearchValue, currentIndex);
                    if (currentIndex != -1)
                    {
                        view.ProfilerGridView.ClearSelection();
                        view.ProfilerGridView.FirstDisplayedScrollingRowIndex = currentIndex;
                        view.ProfilerGridView.Rows[currentIndex].Selected = true;
                        RowEnter(sender, new DataGridViewCellEventArgs(0, currentIndex));
                        currentIndex++;
                    }
                    else
                    {
                        MessageBox.Show("No more results found.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        currentIndex = 0;
                    }
                }
            }
            catch (Exception exc)
            {
                currentIndex = 0;
                MessageBox.Show(exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ShowButtonsByAction("default");
            }
        }

        private void OnStart(object? sender, EventArgs e)
        {
            try
            {
                ClearResults();
                Configure();
                StartProfiling();
                ShowButtonsByAction("start");
                _tokenSource = new CancellationTokenSource();
                _thread = new Thread(() => StartGetLastEvents(_tokenSource.Token)) { IsBackground = true };
                _thread.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ShowButtonsByAction("default");
            }
        }

        private async void OnStop(object? sender, EventArgs e)
        {
            _shouldStop = true;
            ShowButtonsByAction("stop");
            HandleCancellationRequest();

            try
            {
                StopProfiling();
                await SaveRecentConnection();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ShowButtonsByAction("default");
            }
        }

        private void RowEnter(object? sender, EventArgs e)
        {
            DataGridViewCellEventArgs? dataGridViewCellEventArgs = e as DataGridViewCellEventArgs;
            if (dataGridViewCellEventArgs != null)
            {
                DataGridViewTextBoxCell cell = (DataGridViewTextBoxCell)view.ProfilerGridView.Rows[dataGridViewCellEventArgs.RowIndex].Cells["TextData"];
                if (cell != null && _sqlHighlightService != null)
                {
                    view.SqlTextArea = string.Format(htmlDocument, _sqlHighlightService.SyntaxHighlight(cell.Value?.ToString() ?? ""));
                    CreateRowDetails(view.ProfilerGridView.Rows[dataGridViewCellEventArgs.RowIndex]);
                }
            }
        }

        private async Task SaveRecentConnection()
        {
            if (_connectionRepository != null && view.Server != null)
            {
                var newConnection = new Connection(0, "master", DateTime.UtcNow, view.Server, view.User?.Length == 0, view.Password, view.User);
                var existingConnection = await _connectionRepository.Find(f => string.Equals(f.DataSource, newConnection.DataSource, StringComparison.InvariantCultureIgnoreCase)
                                                                && string.Equals(f.UserId, newConnection.UserId, StringComparison.InvariantCultureIgnoreCase));
                if (existingConnection == null)
                {
                    await _connectionRepository.AddAsync(newConnection);
                }
            }
        }

        private int FindGridValue(string searchValue, int startIndex)
        {
            for (int index = startIndex; index < view.ProfilerGridView.Rows.Count; index++)
            {
                DataGridViewRow row = view.ProfilerGridView.Rows[index];
                for (int i = 0; i < row.Cells.Count; i++)
                {
                    string celValue = row.Cells[i]?.Value?.ToString() ?? "";
                    if (celValue.Contains(searchValue, StringComparison.OrdinalIgnoreCase))
                    {
                        return row.Index;
                    }
                }
            }

            return -1;
        }

        private void SetCurrentRows(ProfilerEvent? _event)
        {
            if (_event != null)
            {
                CurrentRows.Add(_event.GetEventKey(), _event);
            }
        }

        private void ShowButtonsByAction(string action)
        {
            switch (action.ToUpper())
            {
                case "START":
                    view.AuthenticationComboBox.Enabled = false;
                    view.PauseButton.Enabled = true;
                    view.PauseButton.Visible = true;
                    view.ResumeButton.Enabled = false;
                    view.ResumeButton.Visible = false;
                    view.ServerTexBox.Enabled = false;
                    view.StartButton.Enabled = false;
                    view.StopButton.Enabled = !view.StartButton.Enabled;
                    break;

                case "STOP":
                    view.AuthenticationComboBox.Enabled = true;
                    view.PauseButton.Enabled = false;
                    view.PauseButton.Visible = true;
                    view.ResumeButton.Enabled = false;
                    view.ResumeButton.Visible = false;
                    view.ServerTexBox.Enabled = true;
                    view.StartButton.Enabled = true;
                    view.StopButton.Enabled = !view.StartButton.Enabled;
                    break;

                case "PAUSE":
                    view.AuthenticationComboBox.Enabled = false;
                    view.PauseButton.Enabled = false;
                    view.PauseButton.Visible = false;
                    view.ResumeButton.Enabled = true;
                    view.ResumeButton.Visible = true;
                    view.ServerTexBox.Enabled = false;
                    view.StartButton.Enabled = false;
                    view.StopButton.Enabled = !view.StartButton.Enabled;
                    break;

                case "RESUME":
                    view.AuthenticationComboBox.Enabled = false;
                    view.PauseButton.Enabled = true;
                    view.PauseButton.Visible = true;
                    view.ResumeButton.Enabled = false;
                    view.ResumeButton.Visible = false;
                    view.ServerTexBox.Enabled = false;
                    view.StartButton.Enabled = false;
                    view.StopButton.Enabled = !view.StartButton.Enabled;
                    break;

                default:
                    view.AuthenticationComboBox.Enabled = true;
                    view.PauseButton.Enabled = false;
                    view.PauseButton.Visible = false;
                    view.ResumeButton.Enabled = false;
                    view.ResumeButton.Visible = false;
                    view.ServerTexBox.Enabled = true;
                    view.StartButton.Enabled = true;
                    view.StopButton.Enabled = !view.StartButton.Enabled;
                    break;
            }
        }

        private async void StartGetLastEvents(CancellationToken token)
        {
            try
            {
                while (!_shouldStop && !token.IsCancellationRequested)
                {
                    await GetLastEventsInternalAsync(token);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ShowButtonsByAction("default");
            }
        }

        private void StartProfiling()
        {
            if (_profilerService != null)
            {
                _profilerService.StartProfiling(view.SessionName, new DefaultProfilerSessionTemplate());
            }
        }

        private void StopProfiling()
        {
            if (_profilerService != null)
            {
                _profilerService.StopProfiling(view.SessionName);
            }
        }
    }
}
