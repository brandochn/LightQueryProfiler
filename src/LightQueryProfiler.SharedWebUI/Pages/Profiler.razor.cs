using Blazored.Modal;
using Blazored.Modal.Services;
using LightQueryProfiler.Highlight.Configuration;
using LightQueryProfiler.Highlight.Engines;
using LightQueryProfiler.Shared.Data;
using LightQueryProfiler.Shared.Enums;
using LightQueryProfiler.Shared.Models;
using LightQueryProfiler.Shared.Repositories;
using LightQueryProfiler.Shared.Repositories.Interfaces;
using LightQueryProfiler.Shared.Services;
using LightQueryProfiler.Shared.Services.Interfaces;
using LightQueryProfiler.SharedWebUI.Components;
using LightQueryProfiler.SharedWebUI.Data;
using LightQueryProfiler.SharedWebUI.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Data.SqlClient;

namespace LightQueryProfiler.SharedWebUI.Pages
{
    public partial class Profiler
    {
        private IApplicationDbContext? _applicationDbContext;

        private IProfilerService? _profilerService;
        private bool _shouldStop = true;
        private SqlHighlightService? _sqlHighlightService;
        private IXEventRepository? _xEventRepository;
        private IXEventService? _xEventService;
        private Dictionary<string, object>? Filters;

        [CascadingParameter] public IModalService Modal { get; set; } = default!;

        [Inject]
        protected LightQueryProfilerInterop? LightQueryProfilerInterop { get; set; }

        [CascadingParameter(Name = "MessageComponent")]
        protected IMessageComponent? MessageComponent { get; set; }

        private AuthenticationMode AuthenticationMode { get; set; }
        private LightQueryProfiler.Shared.Models.EventFilter EventFilterModel { get; set; } = new LightQueryProfiler.Shared.Models.EventFilter();
        [Inject] private IJSRuntime? JSRuntime { get; set; }
        private string? Password { get; set; }
        private BaseProfilerViewTemplate ProfilerViewTemplate { get; set; } = new DefaultProfilerViewTemplate();
        private MarkupString RawSqlTextAreaHtml { get; set; }
        private RenderFragment? RowDetailRender { get; set; }
        private List<Dictionary<string, Event>> Rows { get; set; } = new List<Dictionary<string, Event>>();
        private string? Server { get; set; }
        private string SessionName { get; set; } = "lqpSession";
        private string? SqlTextArea { get; set; }
        private string? User { get; set; }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                await ResizableRableColumnsInitAsync("mainTable");
                await ResizableRableColumnsInitAsync("detailsTable");
                await InitializeNavTab();
                await TableKeydownHandler("mainTable");
                await SortTableAsync("mainTable");
            }
        }

        private void AuthenticationModeHandler(AuthenticationMode authenticationMode)
        {
            AuthenticationMode = authenticationMode;
        }

        private void ClearEvents()
        {
            Rows = new List<Dictionary<string, Event>>();
        }

        private async Task ClearFiltersAsync()
        {
            var parameters = new ModalParameters()
               .Add("Message", "Are you sure you want to clear the filters?");

            var messageDialog = Modal.Show<MessageDialog>("Warning", parameters);
            var result = await messageDialog.Result;

            if (result.Confirmed)
            {
                Filters = new Dictionary<string, object>();
                EventFilterModel = new LightQueryProfiler.Shared.Models.EventFilter();
            }
        }

        private void ClearResults()
        {
            ClearEvents();
            RowDetailRender = null;
            SqlTextArea = string.Empty;
            RawSqlTextAreaHtml = (MarkupString)string.Empty;
            _shouldStop = false;
        }

        private void Configure()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
            if (AuthenticationMode == AuthenticationMode.WindowsAuth)
            {
                builder.IntegratedSecurity = true;
                User = string.Empty;
                Password = string.Empty;
            }

            builder.TrustServerCertificate = true;
            builder.DataSource = Server;
            builder.InitialCatalog = "master";
            builder.UserID = User;
            builder.Password = Password;
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

        private void CreateFilters(LightQueryProfiler.Shared.Models.EventFilter eventFilter)
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

        private RenderFragment CreateRowDetailComponent(Dictionary<string, Event> row) => builder =>
        {
            builder.OpenComponent(0, typeof(RowDetailTemplate));
            builder.AddAttribute(1, "Row", row);
            builder.CloseComponent();
        };

        private List<Dictionary<string, Event>> FilterRowsOut(List<Dictionary<string, Event>> rows)
        {
            List<Dictionary<string, Event>> result = new List<Dictionary<string, Event>>();
            string filterValue;
            string? eventValue;

            if (Filters != null && Filters.Count > 0)
            {
                if (rows != null && rows.Count > 0)
                {
                    foreach (KeyValuePair<string, object> f in Filters)
                    {
                        foreach (Dictionary<string, Event> r in rows)
                        {
                            if (r.ContainsKey(f.Key))
                            {
                                filterValue = (f.Value?.ToString() ?? string.Empty).Trim();
                                eventValue = r[f.Key].EventValue?.ToString();
                                if (eventValue != null &&
                                    eventValue.Contains(filterValue, StringComparison.OrdinalIgnoreCase))
                                {
                                    result.Add(r);
                                }
                            }
                        }
                    }
                }
            }

            return result;
        }

        private async Task GetLastEventsAsync()
        {
            try
            {
                while (!_shouldStop)
                {
                    await GetLastEventsInternalAsync();
                }
            }
            catch (Exception e)
            {
                await ShowButtonsByAction();

                if (MessageComponent != null)
                {
                    MessageComponent.ShowMessage("An error has occurred", e.Message, MessageType.Error);
                }
            }
        }

        private async Task GetLastEventsInternalAsync()
        {
            if (_profilerService != null)
            {
                await Task.Delay(900);
                List<ProfilerEvent>? _events = await _profilerService.GetLastEventsAsync(SessionName);
                if (_events != null)
                {
                    List<Dictionary<string, Event>> newRows = GetNewRows(_events);
                    if (newRows != null && newRows.Count > 0)
                    {
                        foreach (Dictionary<string, Event> row in newRows)
                        {
                            row.Values.First().OnClickAction = () => OnClickRow(row);
                            row.Values.First().OnDoubleClickAction = () => OnDoubleClickRow(row);
                        }

                        if (Filters != null && Filters.Count > 0)
                        {
                            List<Dictionary<string, Event>> filterRows = FilterRowsOut(newRows);
                            if (filterRows != null && filterRows.Count > 0)
                            {
                                Rows.AddRange(filterRows);
                                StateHasChanged();
                            }
                        }
                        else
                        {
                            Rows.AddRange(newRows);
                            StateHasChanged();
                        }
                    }
                }
            }
        }

        private List<Dictionary<string, Event>> GetNewRows(List<ProfilerEvent> events)
        {
            if (events == null)
            {
                return new List<Dictionary<string, Event>>();
            }

            List<Dictionary<string, Event>> newEvents = new List<Dictionary<string, Event>>();
            Dictionary<string, Event> data;
            foreach (var e in events)
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

                    string columName = c.Name;
                    object columValue = string.Empty;

                    if (e.Actions?.Any(a => c.EventsMapped.Contains(a.Key)) ?? false)
                    {
                        columValue = e.Actions.FirstOrDefault(a => c.EventsMapped.Contains(a.Key)).Value ?? string.Empty;
                    }
                    else
                    {
                        if (e.Fields?.Any(f => c.EventsMapped.Contains(f.Key)) ?? false)
                        {
                            columValue = e.Fields.FirstOrDefault(f => c.EventsMapped.Contains(f.Key)).Value ?? string.Empty;
                        }
                    }

                    data[columName] = new Event() { EventValue = columValue, Name = columName };
                }

                newEvents.Add(data);
            }

            return newEvents;
        }

        private async Task InitializeNavTab()
        {
            if (LightQueryProfilerInterop != null)
            {
                await LightQueryProfilerInterop.InitializeNavTab("mainNavTab");
            }
        }

        private async Task OnClickRowHandler(Dictionary<string, Event> row)
        {
            await Task.Run(async () =>
            {
                if (row != null && row.Count > 0)
                {
                    SqlTextArea = row["TextData"]?.EventValue?.ToString() ?? string.Empty;
                    await RenderSqlTextAreaHtml(SqlTextArea);
                    RowDetailRender = CreateRowDetailComponent(row);
                }
            });
        }

        private void OnPause()
        {
            _shouldStop = true;
        }

        private async void OnResume()
        {
            _shouldStop = false;
            await GetLastEventsAsync();
        }

        private async void OnShowFilterModal()
        {
            try
            {
                await ShowModal();
            }
            catch (Exception e)
            {
                if (MessageComponent != null)
                {
                    MessageComponent.ShowMessage("An error has occurred", e.Message, MessageType.Error);
                }
            }
        }

        private async void OnStart()
        {
            try
            {
                ClearResults();
                Configure();
                StartProfiling();

                await GetLastEventsAsync();
            }
            catch (Exception e)
            {
                await ShowButtonsByAction();

                if (MessageComponent != null)
                {
                    MessageComponent.ShowMessage("An error has occurred", e.Message, MessageType.Error);
                }
            }
        }

        private async void OnStop()
        {
            _shouldStop = true;
            try
            {
                await Task.Delay(100);
                StopProfiling();
            }
            catch (Exception e)
            {
                await ShowButtonsByAction();

                if (MessageComponent != null)
                {
                    MessageComponent.ShowMessage("An error has occurred", e.Message, MessageType.Error);
                }
            }
        }

        private void PasswordHandler(string pwd)
        {
            Password = pwd;
        }

        private async Task RenderSqlTextAreaHtml(string sqlText)
        {
            if (!string.IsNullOrEmpty(sqlText))
            {
                if (_sqlHighlightService != null)
                {
                    string? html = _sqlHighlightService.SyntaxHighlight(sqlText);
                    RawSqlTextAreaHtml = new MarkupString(html);
                }
            }
            else
            {
                RawSqlTextAreaHtml = (MarkupString)string.Empty;
            }

            await InvokeAsync(StateHasChanged);
        }

        private async Task ResizableRableColumnsInitAsync(string tableName)
        {
            if (LightQueryProfilerInterop != null)
            {
                await LightQueryProfilerInterop.InitializeResizableTableColumns(tableName);
            }
        }

        private void SeverHandler(string server)
        {
            Server = server;
        }

        private async Task ShowButtonsByAction()
        {
            if (LightQueryProfilerInterop != null)
            {
                await LightQueryProfilerInterop.ShowButtonsByAction("default");
            }
        }

        private async Task ShowModal()
        {
            ModalOptions options = new ModalOptions
            {
                DisableBackgroundCancel = true
            };

            ModalParameters parameters = new ModalParameters()
                .Add(nameof(EventFilterModel), EventFilterModel);

            var moviesModal = Modal.Show<Components.EventFilter>("Filters", parameters, options);
            var result = await moviesModal.Result;

            if (result.Confirmed)
            {
                if (result.Data != null)
                {
                    EventFilterModel = (LightQueryProfiler.Shared.Models.EventFilter)result.Data;
                    CreateFilters(EventFilterModel);
                    ClearEvents();
                }
            }
        }

        private async Task SortTableAsync(string tableName)
        {
            if (LightQueryProfilerInterop != null)
            {
                await LightQueryProfilerInterop.SortTable(tableName);
            }
        }

        private void StartProfiling()
        {
            if (_profilerService == null)
            {
                return;
            }
            _profilerService.StartProfiling(SessionName, new DefaultProfilerSessionTemplate());
        }

        private void StopProfiling()
        {
            if (_profilerService == null)
            {
                return;
            }
            _profilerService.StopProfiling(SessionName);
        }

        private async Task TableKeydownHandler(string table)
        {
            if (JSRuntime == null)
            {
                throw new Exception("JSRuntime cannot be null.");
            }

            Task<IJSObjectReference> moduleTask = JSRuntime.InvokeAsync<IJSObjectReference>("import",
                           "./_content/LightQueryProfiler.SharedWebUI/Pages/Profiler.razor.js").AsTask();
            var module = await moduleTask;

            await module.InvokeAsync<string>("tableKeydownHandler", table);
        }

        private void UserHandler(string user)
        {
            User = user;
        }
    }
}