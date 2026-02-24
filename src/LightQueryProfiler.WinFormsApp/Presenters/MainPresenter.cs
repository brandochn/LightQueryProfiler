using LightQueryProfiler.Highlight.Configuration;
using LightQueryProfiler.Highlight.Engines;
using LightQueryProfiler.Shared.Data;
using LightQueryProfiler.Shared.Enums;
using LightQueryProfiler.Shared.Extensions;
using LightQueryProfiler.Shared.Factories;
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
        private DatabaseEngineType _currentEngineType = DatabaseEngineType.SqlServer;

        private IRepository<Connection>? _connectionRepository;
        private IProfilerService? _profilerService;
        private bool _shouldStop = true;
        private SqlHighlightService? _sqlHighlightService;
        private Thread? _thread;
        private CancellationTokenSource? _tokenSource;
        private IXEventRepository? _xEventRepository;
        private IXEventService? _xEventService;
        private Dictionary<string, ProfilerEvent> CurrentRows = [];
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
            IList<Models.AuthenticationMode> result = [];
            List<Shared.Enums.AuthenticationMode> authenticationModes = Enum.GetValues(typeof(Shared.Enums.AuthenticationMode)).Cast<Shared.Enums.AuthenticationMode>().ToList();
            foreach (var am in authenticationModes)
            {
                result.Add(new Models.AuthenticationMode(am.GetString(), (int)am));
            }

            view.AuthenticationModes = result;
        }

        public void SetProfilerColumns()
        {
            List<DataGridViewColumn> columns = [];
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
                Filters = [];
                EventFilterModel = new Shared.Models.EventFilter();
            }
        }

        private void ClearResults()
        {
            ClearEvents();
            view.SqlTextArea = string.Empty;
            view.ProfilerDetails.Items.Clear();
            _shouldStop = false;
            CurrentRows = [];
        }

        private async Task ConfigureAsync()
        {
            SqlConnectionStringBuilder builder = [];
            int selectedAuthenticationMode = Convert.ToInt32(view.SelectedAuthenticationMode);
            var authMode = (Shared.Enums.AuthenticationMode)selectedAuthenticationMode;

            if (authMode == Shared.Enums.AuthenticationMode.WindowsAuth)
            {
                builder.IntegratedSecurity = true;
                view.User = string.Empty;
                view.Password = string.Empty;
            }
            else if (authMode == Shared.Enums.AuthenticationMode.AzureSQLDatabase)
            {
                // Azure SQL Database requires explicit database name
                if (string.IsNullOrWhiteSpace(view.Database))
                {
                    throw new InvalidOperationException(Resources.DatabaseRequiredForAzureSql);
                }
            }

            builder.TrustServerCertificate = true;
            builder.DataSource = view.Server;

            // Initialize Database with default value if not set (except for Azure SQL Database)
            if (string.IsNullOrWhiteSpace(view.Database))
            {
                view.Database = "master";
            }

            builder.InitialCatalog = view.Database;
            builder.UserID = view.User;
            builder.Password = view.Password;
            builder.ApplicationName = "LightQueryProfiler";

            _applicationDbContext = new ApplicationDbContext(builder.ConnectionString);

            // Determine database engine type
            // For Azure SQL Database auth mode, we can directly infer the engine type without detection.
            // For other auth modes (Windows Auth, SQL Server Auth), we need to query the server.
            _currentEngineType = await GetDatabaseEngineTypeAsync(authMode, _applicationDbContext);

            _xEventRepository = new XEventRepository(_applicationDbContext);
            _xEventRepository.SetEngineType(_currentEngineType);

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
            Filters = [];
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
                List<ListViewItem> items = [];
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
            if (rows == null)
            {
                return [];
            }

            // If no filters or no rows, return the original list
            if (Filters == null || Filters.Count == 0 || rows.Count == 0)
            {
                return rows;
            }

            List<Dictionary<string, Event>> result = [];

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
                if (events == null || events.Count == 0)
                {
                    return;
                }

                List<Dictionary<string, Event>> newRows = GetNewRows(events);
                if (newRows == null || newRows.Count == 0)
                {
                    return;
                }

                List<Dictionary<string, Event>> filteredRows = FilterRows(newRows);

                // Update UI in a single invoke
                view.ProfilerGridView.Invoke(() =>
                {
                    int lastRowId = 0;
                    bool wasEmpty = view.ProfilerGridView.Rows.Count == 0;

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

                    // If grid was empty, select first row and manually trigger row details display
                    if (wasEmpty && view.ProfilerGridView.Rows.Count > 0)
                    {
                        view.ProfilerGridView.Rows[0].Selected = true;
                        view.ProfilerGridView.CurrentCell = view.ProfilerGridView.Rows[0].Cells[0];

                        // Manually display the first row's details
                        DataGridViewTextBoxCell firstCell = (DataGridViewTextBoxCell)view.ProfilerGridView.Rows[0].Cells["TextData"];
                        if (firstCell != null && _sqlHighlightService != null)
                        {
                            view.SqlTextArea = string.Format(htmlDocument, _sqlHighlightService.SyntaxHighlight(firstCell.Value?.ToString() ?? ""));
                            CreateRowDetails(view.ProfilerGridView.Rows[0]);
                        }
                    }
                    // Otherwise scroll to the last added row
                    else if (lastRowId > 0)
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
            List<Dictionary<string, Event>> newEvents = [];
            Dictionary<string, Event> data;
            foreach (var e in events)
            {
                if (!CurrentRows.ContainsKey(e.GetEventKey()))
                {
                    data = [];

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
                        view.Database = connection.InitialCatalog;

                        // Use stored engine type if available, otherwise infer from authentication mode
                        _currentEngineType = connection.EngineType ?? InferEngineTypeFromAuthenticationMode(connection.AuthenticationMode);

                        // Use stored AuthenticationMode from connection
                        view.SelectedAuthenticationMode = connection.AuthenticationMode;
                        view.AuthenticationComboBox.SelectedIndex = (int)connection.AuthenticationMode;

                        if (connection.AuthenticationMode == Shared.Enums.AuthenticationMode.WindowsAuth)
                        {
                            view.User = string.Empty;
                            view.Password = string.Empty;
                        }
                        else if (connection.AuthenticationMode == Shared.Enums.AuthenticationMode.AzureSQLDatabase)
                        {
                            view.User = connection.UserId;
                            view.Password = connection.Password;
                            view.Database = connection.InitialCatalog;
                        }
                        else
                        {
                            view.User = connection.UserId;
                            view.Password = connection.Password;
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

            // Optionally reset scroll position to top
            if (view.ProfilerGridView.Rows.Count > 0)
            {
                view.ProfilerGridView.FirstDisplayedScrollingRowIndex = 0;
            }
        }

        private void OnNextSearch(object? sender, EventArgs e)
        {
            try
            {
                if (view.ProfilerGridView.Rows.Count == 0)
                {
                    MessageBox.Show("No data available to search.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (string.IsNullOrWhiteSpace(view.SearchValue))
                {
                    MessageBox.Show("Please enter a search value.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Ensure currentIndex is within valid range
                if (currentIndex < 0 || currentIndex >= view.ProfilerGridView.Rows.Count)
                {
                    currentIndex = 0;
                }

                int foundIndex = FindGridValue(view.SearchValue, currentIndex);

                if (foundIndex != -1)
                {
                    view.ProfilerGridView.ClearSelection();

                    // Safely set scroll position
                    if (foundIndex < view.ProfilerGridView.Rows.Count)
                    {
                        view.ProfilerGridView.FirstDisplayedScrollingRowIndex = foundIndex;
                        view.ProfilerGridView.Rows[foundIndex].Selected = true;
                        RowEnter(sender, new DataGridViewCellEventArgs(0, foundIndex));
                        currentIndex = foundIndex + 1;
                    }
                }
                else
                {
                    // No more results found, offer to wrap around
                    if (currentIndex > 0)
                    {
                        DialogResult result = MessageBox.Show(
                            "No more results found. Search from the beginning?",
                            "Search",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question);

                        if (result == DialogResult.Yes)
                        {
                            currentIndex = 0;
                            OnNextSearch(sender, e); // Recursively search from start
                        }
                        else
                        {
                            currentIndex = 0;
                        }
                    }
                    else
                    {
                        MessageBox.Show("No results found.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

        private async void OnStart(object? sender, EventArgs e)
        {
            try
            {
                ClearResults();
                await ConfigureAsync();
                StartProfiling();
                ShowButtonsByAction("start");
                _tokenSource = new CancellationTokenSource();
                _thread = new Thread(() => StartGetLastEvents(_tokenSource.Token)) { IsBackground = true };
                _thread.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Resources.ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                int selectedAuthenticationMode = Convert.ToInt32(view.SelectedAuthenticationMode);
                var authMode = (Shared.Enums.AuthenticationMode)selectedAuthenticationMode;
                var newConnection = new Connection(0, view.Database ?? "master", DateTime.UtcNow, view.Server, view.User?.Length == 0, view.Password, view.User, _currentEngineType, authMode);
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
            if (string.IsNullOrWhiteSpace(searchValue))
            {
                return -1;
            }

            if (startIndex < 0 || startIndex >= view.ProfilerGridView.Rows.Count)
            {
                return -1;
            }

            for (int index = startIndex; index < view.ProfilerGridView.Rows.Count; index++)
            {
                DataGridViewRow row = view.ProfilerGridView.Rows[index];
                for (int i = 0; i < row.Cells.Count; i++)
                {
                    string cellValue = row.Cells[i]?.Value?.ToString() ?? string.Empty;
                    if (cellValue.Contains(searchValue, StringComparison.OrdinalIgnoreCase))
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
            // Ensure UI updates happen on the UI thread
            if (view.ProfilerGridView.InvokeRequired)
            {
                view.ProfilerGridView.Invoke(() => ShowButtonsByAction(action));
                return;
            }

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
                var template = ProfilerSessionTemplateFactory.CreateTemplate(_currentEngineType);
                _profilerService.StartProfiling(view.SessionName, template);
            }
        }

        private void StopProfiling()
        {
            if (_profilerService != null)
            {
                _profilerService.StopProfiling(view.SessionName);
            }
        }

        /// <summary>
        /// Infers the database engine type from the authentication mode.
        /// Azure SQL Database authentication mode directly implies Azure SQL Database engine type.
        /// Other modes default to SQL Server.
        /// </summary>
        /// <param name="authenticationMode">The authentication mode</param>
        /// <returns>The inferred database engine type</returns>
        private static DatabaseEngineType InferEngineTypeFromAuthenticationMode(Shared.Enums.AuthenticationMode authenticationMode)
        {
            return authenticationMode == Shared.Enums.AuthenticationMode.AzureSQLDatabase
                ? DatabaseEngineType.AzureSqlDatabase
                : DatabaseEngineType.SqlServer;
        }

        /// <summary>
        /// Gets the database engine type by inferring from authentication mode when possible,
        /// or detecting it by querying the server for other authentication modes.
        /// </summary>
        /// <param name="authenticationMode">The authentication mode</param>
        /// <param name="dbContext">The database context to use for detection</param>
        /// <returns>The database engine type</returns>
        private static async Task<DatabaseEngineType> GetDatabaseEngineTypeAsync(
            Shared.Enums.AuthenticationMode authenticationMode,
            IApplicationDbContext dbContext)
        {
            if (authenticationMode == Shared.Enums.AuthenticationMode.AzureSQLDatabase)
            {
                // Azure SQL Database authentication mode directly implies the engine type
                return DatabaseEngineType.AzureSqlDatabase;
            }

            // For other authentication modes, detect the engine type by querying the server
            var engineDetector = new DatabaseEngineDetector();
            return await engineDetector.DetectEngineTypeAsync(dbContext);
        }
    }
}
