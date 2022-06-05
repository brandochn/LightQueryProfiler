namespace LightQueryProfiler.Shared.Models
{
    public abstract class BaseProfilerViewTemplate
    {
        public abstract string Name { get; set; }
        public abstract IList<BaseColumnViewTemplate> Columns { get; set; }
    }
}