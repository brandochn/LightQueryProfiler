namespace LightQueryProfiler.Shared.Models
{
    public class EventFilter
    {
        public string EventClass { get; set; } = string.Empty;
        public string TextData { get; set; } = string.Empty;
        public string ApplicationName { get; set; } = string.Empty;
        public string NTUserName { get; set; } = string.Empty;
        public string LoginName { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
    }
}