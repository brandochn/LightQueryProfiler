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
        private IXEventRepository? _xEventRepository;
        private IXEventService? _xEventService;

        [Inject]
        protected LightQueryProfilerInterop? LightQueryProfilerInterop { get; set; }

        [CascadingParameter(Name = "MessageComponent")]
        protected IMessageComponent? MessageComponent { get; set; }

        private AuthenticationMode AuthenticationMode { get; set; }
        [Inject] private IJSRuntime? JSRuntime { get; set; }
        private string? Password { get; set; }
        private BaseProfilerViewTemplate ProfilerViewTemplate { get; set; } = new DefaultProfilerViewTemplate();
        private MarkupString RawSqlTextAreaHtml { get; set; }
        private RenderFragment? RowDetailRender { get; set; }
        private RenderFragment? RowRender { get; set; }
        private List<Dictionary<string, object>> Rows { get; set; } = new List<Dictionary<string, object>>();
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

        private List<Dictionary<string, object>> AddRows(List<ProfilerEvent> events)
        {
            if (events == null)
            {
                return new List<Dictionary<string, object>>();
            }

            List<Dictionary<string, object>> newEvents = new List<Dictionary<string, object>>();
            Dictionary<string, object> data;
            foreach (var e in events)
            {
                data = new Dictionary<string, object>();

                foreach (BaseColumnViewTemplate c in ProfilerViewTemplate.Columns)
                {
                    if (c.Name == "EventClass")
                    {
                        data["EventClass"] = e.Name ?? string.Empty;
                        continue;
                    }

                    if (c.Name == "StartTime")
                    {
                        data["StartTime"] = e.Timestamp ?? string.Empty;
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

                    data[columName] = columValue;
                }

                newEvents.Add(data);
            }

            return newEvents;
        }

        private void AuthenticationModeHandler(AuthenticationMode authenticationMode)
        {
            AuthenticationMode = authenticationMode;
        }

        private void ClearResults()
        {
            Rows = new List<Dictionary<string, object>>();
            RowRender = null;
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
        }

        private RenderFragment CreateRowComponent(List<Dictionary<string, object>> rows, MulticastDelegate? callBack) => builder =>
        {
            foreach (var r in rows)
            {
                builder.OpenComponent(0, typeof(RowTemplate));
                builder.AddAttribute(1, "Row", r);
                builder.AddAttribute(2, "onClickRowCallBack", callBack);
                builder.CloseComponent();
            }
        };

        private RenderFragment CreateRowDetailComponent(Dictionary<string, object> row) => builder =>
        {
            builder.OpenComponent(0, typeof(RowDetailTemplate));
            builder.AddAttribute(1, "Row", row);
            builder.CloseComponent();
        };

        private async Task GetLastEventsAsync()
        {
            while (!_shouldStop)
            {
                await GetLastEventsInternalAsync();
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
                    Rows.AddRange(AddRows(_events));
                    RowRender = CreateRowComponent(Rows, OnClickRowHandler);
                    StateHasChanged();
                }
            }
        }

        private async Task InitializeNavTab()
        {
            if (LightQueryProfilerInterop != null)
            {
                await LightQueryProfilerInterop.InitializeNavTab("mainNavTab");
            }
        }

        private async void OnClickRowHandler(Dictionary<string, object> row)
        {
            await Task.Run(async () =>
            {
                if (row != null && row.Count > 0)
                {
                    SqlTextArea = row["TextData"]?.ToString() ?? string.Empty;
                    await RenderSqlTextAreaHtml(SqlTextArea);
                    RowDetailRender = CreateRowDetailComponent(row);
                    await InvokeAsync(StateHasChanged);
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
            try
            {
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
                await Task.Delay(1000);
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
                if (LightQueryProfilerInterop != null)
                {
                    string? html = await LightQueryProfilerInterop.SyntaxHighlight(sqlText ?? string.Empty);
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

            Lazy<Task<IJSObjectReference>> moduleTask = new(() => JSRuntime.InvokeAsync<IJSObjectReference>("import",
                           "./_content/LightQueryProfiler.SharedWebUI/Pages/Profiler.razor.js").AsTask());
            var module = await moduleTask.Value;

            await module.InvokeAsync<string>("tableKeydownHandler", table);
        }
        private void UserHandler(string user)
        {
            User = user;
        }
    }
}