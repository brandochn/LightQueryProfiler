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

        /// <summary>
        /// Generates a unique key for the event based on SQL Server Extended Events unique identifiers.
        /// Uses event_sequence (guaranteed unique per session) and attach_activity_id (GUID) when available,
        /// falling back to timestamp and event name combination.
        /// </summary>
        public string GetEventKey()
        {
            // Option 1: Use event_sequence (most reliable - unique per session)
            if (Actions?.TryGetValue("event_sequence", out var eventSequence) == true && eventSequence != null)
            {
                return $"seq:{eventSequence}";
            }

            // Option 2: Use attach_activity_id (GUID - unique per activity)
            if (Actions?.TryGetValue("attach_activity_id", out var activityId) == true && activityId != null)
            {
                return $"activity:{activityId}";
            }

            // Option 3: Fallback - combination of timestamp, name, and session_id
            var timestamp = Timestamp ?? string.Empty;
            var name = Name ?? string.Empty;
            var sessionId = Actions?.TryGetValue("session_id", out var sid) == true ? sid?.ToString() : string.Empty;

            return $"{timestamp}|{name}|{sessionId}";
        }
    }
}
