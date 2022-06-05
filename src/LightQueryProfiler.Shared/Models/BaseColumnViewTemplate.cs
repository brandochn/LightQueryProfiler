namespace LightQueryProfiler.Shared.Models
{
    public abstract class BaseColumnViewTemplate
    {
        public string Name { get; set; } = string.Empty;
        public List<string> EventsMapped { get; set; } = new List<string>();
    }
}