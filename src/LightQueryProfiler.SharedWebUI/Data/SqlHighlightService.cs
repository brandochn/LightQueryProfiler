using LightQueryProfiler.Highlight.Configuration;
using LightQueryProfiler.Highlight.Engines;

namespace LightQueryProfiler.SharedWebUI.Data
{
    public class SqlHighlightService
    {
        private readonly IEngine _engine;
        private readonly IConfiguration _configuration;

        public SqlHighlightService(IEngine engine, IConfiguration configuration)
        {
            _engine = engine;
            _configuration = configuration;
        }

        public string SyntaxHighlight(string sqlString)
        {
            var definition = _configuration.Definitions["SQL"];
            string output = _engine.Highlight(definition, sqlString);

            return output;
        }
    }
}