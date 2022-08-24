using LightQueryProfiler.Highlight.Patterns;

namespace LightQueryProfiler.Highlight.Engines
{
    public interface IEngine
    {
        string Highlight(Definition definition, string input);
    }
}