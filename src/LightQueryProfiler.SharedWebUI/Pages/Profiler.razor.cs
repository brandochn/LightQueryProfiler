using LightQueryProfiler.Shared;
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
using System.Data.SqlClient;

namespace LightQueryProfiler.SharedWebUI.Pages
{
    public partial class Profiler
    {
        private IApplicationDbContext? _applicationDbContext;

        //for cancaletation
        private CancellationTokenSource? _cancelationTokenSource;

        //for implementing pause in processing
        private PauseTokenSource? _pauseTokeSource;

        private IProfilerService? _profilerService;

        private IXEventRepository? _xEventRepository;

        private IXEventService? _xEventService;

        [CascadingParameter(Name = "MessageComponent")]
        protected IMessageComponent? MessageComponent { get; set; }

        [Inject]
        protected SqlHighLightService? SqlHighLightService { get; set; }

        private AuthenticationMode AuthenticationMode { get; set; }
        private string? Password { get; set; }
        private BaseProfilerViewTemplate ProfilerViewTemplate { get; set; } = new DefaultProfilerViewTemplate();
        private MarkupString RawSqlTextAreaHtml { get; set; }
        private RenderFragment? RowRender { get; set; }
        private List<Dictionary<string, object>> Rows { get; set; } = new List<Dictionary<string, object>>();
        private string? Server { get; set; }
        private string SessionName { get; set; } = "lqpSession";
        private string? SqlTextArea { get; set; }
        private string? User { get; set; }
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

        private void ClearTableResults()
        {
            Rows = new List<Dictionary<string, object>>();
            RowRender = null;
            SqlTextArea = string.Empty;
            StateHasChanged();
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

        private RenderFragment CreateRowComponent(List<Dictionary<string, object>> rows) => builder =>
        {
            foreach (var r in rows)
            {
                builder.OpenComponent(0, typeof(RowTemplate));
                builder.AddAttribute(1, "Row", r);
                builder.AddAttribute(2, "onClickRowCallBack", OnClickRowHandler);
                builder.CloseComponent();
            }
        };

        private async Task GetLastEventsAsync(PauseToken pauseToken, CancellationToken cancelToken)
        {
            while (true)
            {
                //if the pause is active the code will wait here but not block UI thread
                await pauseToken.WaitWhilePausedAsync();

                await GetLastEventsAsync(cancelToken);
            }
        }

        private async Task GetLastEventsAsync(CancellationToken cancelToken)
        {
            if (_profilerService != null)
            {
                await Task.Delay(1000, cancelToken);
                List<ProfilerEvent>? _events = await _profilerService.GetLastEventsAsync(SessionName);
                if (_events != null)
                {
                    Rows.AddRange(AddRows(_events));
                    RowRender = CreateRowComponent(Rows);
                    StateHasChanged();
                }
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
                }
            });
        }

        private async void OnPause()
        {
            await Task.Run(() =>
            {
                if (_pauseTokeSource != null)
                {
                    _pauseTokeSource.IsPaused = !_pauseTokeSource.IsPaused;
                }
            });
        }

        private async void OnResume()
        {
            await Task.Run(() =>
            {
                if (_pauseTokeSource != null)
                {
                    _pauseTokeSource.IsPaused = !_pauseTokeSource.IsPaused;
                }
            });
        }

        private async void OnStart()
        {
            try
            {
                ClearTableResults();
                Configure();
                StartProfiling();
                //creating cancel and pause token sources
                _pauseTokeSource = new PauseTokenSource();
                _cancelationTokenSource = new CancellationTokenSource();

                await GetLastEventsAsync(_pauseTokeSource.Token, _cancelationTokenSource.Token);
            }
            catch (TaskCanceledException)
            {
                if (_cancelationTokenSource != null && _cancelationTokenSource.IsCancellationRequested)
                {
                    StopProfiling();
                }
            }
            catch (Exception e)
            {
                if (MessageComponent != null)
                {
                    MessageComponent.ShowMessage("An error has occurred", e.Message, MessageType.Error);
                }
            }
        }

        private void OnStop()
        {
            if (_cancelationTokenSource != null)
            {
                _cancelationTokenSource.Cancel();
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
                if (SqlHighLightService != null)
                {
                    string? html = await SqlHighLightService.SyntaxHighlight(sqlText ?? string.Empty);
                    RawSqlTextAreaHtml = new MarkupString(html);
                }
            }
            else
            {
                RawSqlTextAreaHtml = (MarkupString)string.Empty;
            }

            await InvokeAsync(StateHasChanged);
        }

        private void SeverHandler(string server)
        {
            Server = server;
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

        private void UserHandler(string user)
        {
            User = user;
        }
    }
}