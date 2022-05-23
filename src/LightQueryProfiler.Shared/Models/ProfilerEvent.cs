namespace LightQueryProfiler.Shared.Models
{
    public class ProfilerEvent
    {
        public string? Name { get; set; }
        public string? Timestamp { get; set; }
        public Dictionary<string, object?>? Fields { get; set; }
        public Dictionary<string, object?>? Actions { get; set; }

        public override string ToString()
        {
            return string.Format("Event:{0}, Timestamp:{1}, Fields:{2}, Actions:{3}", Name, Timestamp?.ToString() ?? string.Empty, string.Join(", ", Fields ?? new Dictionary<string, object?>()), string.Join(", ", Actions ?? new Dictionary<string, object?>()));
        }

        public string GetEventKey()
        {
            return string.Join(Environment.NewLine, (Actions ?? new Dictionary<string, object?>()).Union(Fields ?? new Dictionary<string, object?>()).Select(v => v.Value)).Trim();
        }
    }
}