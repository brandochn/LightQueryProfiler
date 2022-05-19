using System.Xml.Serialization;

namespace LightQueryProfiler.Shared.Models.XEvent
{
    [XmlRoot(ElementName = "RingBufferTarget")]
    public class RingBufferTarget
    {
        [XmlElement(ElementName = "event")]
        public List<Event>? Event { get; set; }

        [XmlAttribute(AttributeName = "truncated")]
        public int? Truncated { get; set; }

        [XmlAttribute(AttributeName = "processingTime")]
        public int? ProcessingTime { get; set; }

        [XmlAttribute(AttributeName = "totalEventsProcessed")]
        public int? TotalEventsProcessed { get; set; }

        [XmlAttribute(AttributeName = "eventCount")]
        public int? EventCount { get; set; }

        [XmlAttribute(AttributeName = "droppedCount")]
        public int? DroppedCount { get; set; }

        [XmlAttribute(AttributeName = "memoryUsed")]
        public int? MemoryUsed { get; set; }
    }
}