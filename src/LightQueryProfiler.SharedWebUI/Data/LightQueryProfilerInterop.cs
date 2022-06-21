using Microsoft.JSInterop;

namespace LightQueryProfiler.SharedWebUI.Data
{
    public class LightQueryProfilerInterop : IAsyncDisposable
    {
        private readonly Lazy<Task<IJSObjectReference>> moduleTask;

        public LightQueryProfilerInterop(IJSRuntime jsRuntime)
        {
            moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
           "import", "./_content/LightQueryProfiler.SharedWebUI/js/lightqueryprofiler.js").AsTask());
        }

        public async Task InitializeResizableTableColumns(string tableName)
        {
            var module = await moduleTask.Value;
            await module.InvokeAsync<string>("initializeResizableTableColumns", tableName);
        }

        public async ValueTask<string> SyntaxHighlight(string sqlString)
        {
            var module = await moduleTask.Value;
            return await module.InvokeAsync<string>("syntaxHighlight", sqlString);
        }

        public async ValueTask DisposeAsync()
        {
            if (moduleTask.IsValueCreated)
            {
                var module = await moduleTask.Value;
                await module.DisposeAsync();
            }
        }
    }
}