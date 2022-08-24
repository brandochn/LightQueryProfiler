using LightQueryProfiler.Highlight.Patterns;

namespace LightQueryProfiler.Highlight.Configuration
{
    public interface IConfiguration
    {
        IDictionary<string, Definition> Definitions { get; }
    }
}