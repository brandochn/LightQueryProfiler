using Microsoft.JSInterop;

namespace LightQueryProfiler.SharedWebUI.Data
{
    public class SqlHighLightService : IAsyncDisposable
    {
        private readonly Lazy<Task<IJSObjectReference>> moduleTask;

        public SqlHighLightService(IJSRuntime jsRuntime)
        {
            moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/LightQueryProfiler.SharedWebUI/js/sqlHighLightService.js").AsTask());
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