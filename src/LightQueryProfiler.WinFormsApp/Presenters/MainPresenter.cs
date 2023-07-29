using LightQueryProfiler.Highlight.Configuration;
using LightQueryProfiler.Highlight.Engines;
using LightQueryProfiler.Shared.Data;
using LightQueryProfiler.Shared.Extensions;
using LightQueryProfiler.Shared.Models;
using LightQueryProfiler.Shared.Repositories;
using LightQueryProfiler.Shared.Repositories.Interfaces;
using LightQueryProfiler.Shared.Services;
using LightQueryProfiler.Shared.Services.Interfaces;
using LightQueryProfiler.SharedWebUI.Data;
using LightQueryProfiler.WinFormsApp.Views;
using System.Data.SqlClient;

namespace LightQueryProfiler.WinFormsApp.Presenters
{
    public class MainPresenter
    {
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

        private IProfilerService? _profilerService;
        private bool _shouldStop = true;
        private SqlHighlightService? _sqlHighlightService;
        private Thread? _thread;
        private CancellationTokenSource? _tokenSource;
        private IXEventRepository? _xEventRepository;
        private IXEventService? _xEventService;
        private Dictionary<string, ProfilerEvent> CurrentRows = new();
        private Dictionary<string, object>? Filters;

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
            CurrentRows = new();
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

        private List<Dictionary<string, Event>> FilterRows(List<Dictionary<string, Event>> rows)
        {
            List<Dictionary<string, Event>> result = new List<Dictionary<string, Event>>();
            string filterValue;
            string? eventValue;

            if (Filters?.Count > 0 && rows?.Count > 0)
            {
                foreach (Dictionary<string, Event> r in rows)
                {
                    foreach (KeyValuePair<string, object> f in Filters)
                    {
                        if (r.ContainsKey(f.Key))
                        {
                            filterValue = (f.Value?.ToString() ?? string.Empty).Trim();
                            eventValue = r[f.Key].EventValue?.ToString();
                            if (eventValue?.Contains(filterValue, StringComparison.OrdinalIgnoreCase) == true)
                            {
                                result.Add(r);
                            }
                        }
                    }
                }
            }
            else
            {
                return rows ?? new List<Dictionary<string, Event>>();
            }

            return result;
        }

        private async Task GetLastEventsInternalAsync()
        {
            if (_profilerService != null)
            {
                await Task.Delay(900);
                List<ProfilerEvent>? _events = await _profilerService.GetLastEventsAsync(view.SessionName);
                if (_events?.Count > 0)
                {
                    List<Dictionary<string, Event>> newRows = GetNewRows(_events);
                    if (newRows?.Count > 0)
                    {
                        List<Dictionary<string, Event>> _rows = FilterRows(newRows);
                        view.ProfilerGridView.Invoke(() =>
                        {
                            int rowId = 0;
                            foreach (Dictionary<string, Event> r in _rows)
                            {
                                rowId = view.ProfilerGridView.Rows.Add();
                                DataGridViewRow row = view.ProfilerGridView.Rows[rowId];
                                foreach (BaseColumnViewTemplate c in ProfilerViewTemplate.Columns)
                                {
                                    row.Cells[c.Name].Value = r[c.Name].EventValue;
                                }
                            }
                            view.StatusBar.Invoke(() => view.StatusBar.Items[0].Text = $"Events: {view.ProfilerGridView.Rows.Count}");
                            if (rowId > 0)
                            {
                                view.ProfilerGridView.FirstDisplayedScrollingRowIndex = rowId;
                            }
                        });
                    }
                }
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

        private void OnResume(object? sender, EventArgs e)
        {
            _shouldStop = false;
            ShowButtonsByAction("resume");
            _tokenSource = new CancellationTokenSource();
            _thread = new Thread(() => StartGetLastEvents(_tokenSource.Token)) { IsBackground = true };
            _thread.Start();
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

        private void OnStop(object? sender, EventArgs e)
        {
            _shouldStop = true;
            ShowButtonsByAction("stop");
            HandleCancellationRequest();

            try
            {
                StopProfiling();
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
                    if (view.AuthenticationComboBox.InvokeRequired) view.AuthenticationComboBox.Invoke(() => view.AuthenticationComboBox.Enabled = false); else view.AuthenticationComboBox.Enabled = false;
                    if (view.PauseButton.InvokeRequired) view.PauseButton.Invoke(() => view.PauseButton.Enabled = true); else view.PauseButton.Enabled = true;
                    if (view.PauseButton.InvokeRequired) view.PauseButton.Invoke(() => view.PauseButton.Visible = true); else view.PauseButton.Visible = true;
                    if (view.ResumeButton.InvokeRequired) view.ResumeButton.Invoke(() => view.ResumeButton.Enabled = false); else view.ResumeButton.Enabled = false;
                    if (view.ResumeButton.InvokeRequired) view.ResumeButton.Invoke(() => view.ResumeButton.Visible = false); else view.ResumeButton.Visible = false;
                    if (view.ServerTexBox.InvokeRequired) view.ServerTexBox.Invoke(() => view.ServerTexBox.Enabled = false); else view.ServerTexBox.Enabled = false;
                    if (view.StartButton.InvokeRequired) view.StartButton.Invoke(() => view.StartButton.Enabled = false); else view.StartButton.Enabled = false;
                    if (view.StopButton.InvokeRequired) view.StopButton.Invoke(() => view.StopButton.Enabled = !view.StartButton.Enabled); else view.StopButton.Enabled = !view.StartButton.Enabled;
                    break;

                case "STOP":
                    if (view.AuthenticationComboBox.InvokeRequired) view.AuthenticationComboBox.Invoke(() => view.AuthenticationComboBox.Enabled = true); else view.AuthenticationComboBox.Enabled = true;
                    if (view.PauseButton.InvokeRequired) view.PauseButton.Invoke(() => view.PauseButton.Enabled = false); else view.PauseButton.Enabled = false;
                    if (view.PauseButton.InvokeRequired) view.PauseButton.Invoke(() => view.PauseButton.Visible = true); else view.PauseButton.Visible = true;
                    if (view.ResumeButton.InvokeRequired) view.ResumeButton.Invoke(() => view.ResumeButton.Enabled = false); else view.ResumeButton.Enabled = false;
                    if (view.ResumeButton.InvokeRequired) view.ResumeButton.Invoke(() => view.ResumeButton.Visible = false); else view.ResumeButton.Visible = false;
                    if (view.ServerTexBox.InvokeRequired) view.ServerTexBox.Invoke(() => view.ServerTexBox.Enabled = true); else view.ServerTexBox.Enabled = true;
                    if (view.StartButton.InvokeRequired) view.StartButton.Invoke(() => view.StartButton.Enabled = true); else view.StartButton.Enabled = true;
                    if (view.StopButton.InvokeRequired) view.StopButton.Invoke(() => view.StopButton.Enabled = !view.StartButton.Enabled); else view.StopButton.Enabled = !view.StartButton.Enabled;
                    break;

                case "PAUSE":
                    if (view.AuthenticationComboBox.InvokeRequired) view.AuthenticationComboBox.Invoke(() => view.AuthenticationComboBox.Enabled = false); else view.AuthenticationComboBox.Enabled = false;
                    if (view.PauseButton.InvokeRequired) view.PauseButton.Invoke(() => view.PauseButton.Enabled = false); else view.PauseButton.Enabled = false;
                    if (view.PauseButton.InvokeRequired) view.PauseButton.Invoke(() => view.PauseButton.Visible = false); else view.PauseButton.Visible = false;
                    if (view.ResumeButton.InvokeRequired) view.ResumeButton.Invoke(() => view.ResumeButton.Enabled = true); else view.ResumeButton.Enabled = true;
                    if (view.ResumeButton.InvokeRequired) view.ResumeButton.Invoke(() => view.ResumeButton.Visible = true); else view.ResumeButton.Visible = true;
                    if (view.ServerTexBox.InvokeRequired) view.ServerTexBox.Invoke(() => view.ServerTexBox.Enabled = false); else view.ServerTexBox.Enabled = false;
                    if (view.StartButton.InvokeRequired) view.StartButton.Invoke(() => view.StartButton.Enabled = false); else view.StartButton.Enabled = false;
                    if (view.StopButton.InvokeRequired) view.StopButton.Invoke(() => view.StopButton.Enabled = !view.StartButton.Enabled); else view.StopButton.Enabled = !view.StartButton.Enabled;
                    break;

                case "RESUME":
                    if (view.AuthenticationComboBox.InvokeRequired) view.AuthenticationComboBox.Invoke(() => view.AuthenticationComboBox.Enabled = false); else view.AuthenticationComboBox.Enabled = false;
                    if (view.PauseButton.InvokeRequired) view.PauseButton.Invoke(() => view.PauseButton.Enabled = true); else view.PauseButton.Enabled = true;
                    if (view.PauseButton.InvokeRequired) view.PauseButton.Invoke(() => view.PauseButton.Visible = true); else view.PauseButton.Visible = true;
                    if (view.ResumeButton.InvokeRequired) view.ResumeButton.Invoke(() => view.ResumeButton.Enabled = false); else view.ResumeButton.Enabled = false;
                    if (view.ResumeButton.InvokeRequired) view.ResumeButton.Invoke(() => view.ResumeButton.Visible = false); else view.ResumeButton.Visible = false;
                    if (view.ServerTexBox.InvokeRequired) view.ServerTexBox.Invoke(() => view.ServerTexBox.Enabled = false); else view.ServerTexBox.Enabled = false;
                    if (view.StartButton.InvokeRequired) view.StartButton.Invoke(() => view.StartButton.Enabled = false); else view.StartButton.Enabled = false;
                    if (view.StopButton.InvokeRequired) view.StopButton.Invoke(() => view.StopButton.Enabled = !view.StartButton.Enabled); else view.StopButton.Enabled = !view.StartButton.Enabled;
                    break;

                default:
                    if (view.AuthenticationComboBox.InvokeRequired) view.AuthenticationComboBox.Invoke(() => view.AuthenticationComboBox.Enabled = true); else view.AuthenticationComboBox.Enabled = true;
                    if (view.PauseButton.InvokeRequired) view.PauseButton.Invoke(() => view.PauseButton.Enabled = false); else view.PauseButton.Enabled = false;
                    if (view.PauseButton.InvokeRequired) view.PauseButton.Invoke(() => view.PauseButton.Visible = true); else view.PauseButton.Visible = true;
                    if (view.ResumeButton.InvokeRequired) view.ResumeButton.Invoke(() => view.ResumeButton.Enabled = false); else view.ResumeButton.Enabled = false;
                    if (view.ResumeButton.InvokeRequired) view.ResumeButton.Invoke(() => view.ResumeButton.Visible = false); else view.ResumeButton.Visible = false;
                    if (view.ServerTexBox.InvokeRequired) view.ServerTexBox.Invoke(() => view.ServerTexBox.Enabled = true); else view.ServerTexBox.Enabled = true;
                    if (view.StartButton.InvokeRequired) view.StartButton.Invoke(() => view.StartButton.Enabled = true); else view.StartButton.Enabled = true;
                    if (view.StopButton.InvokeRequired) view.StopButton.Invoke(() => view.StopButton.Enabled = !view.StartButton.Enabled); else view.StopButton.Enabled = !view.StartButton.Enabled;
                    break;
            }
        }

        private async void StartGetLastEvents(CancellationToken token)
        {
            try
            {
                while (!_shouldStop && !token.IsCancellationRequested)
                {
                    await GetLastEventsInternalAsync();
                }
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