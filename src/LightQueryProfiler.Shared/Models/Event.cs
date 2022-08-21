namespace LightQueryProfiler.Shared.Models
{
    public class Event
    {
        public string Name { get; set; } = string.Empty;
        public object? EventValue { get; set; }
        public Action OnClickAction { get; set; } = () => { };
        public Action OnDoubleClickAction { get; set; } = () => { };
    }
}