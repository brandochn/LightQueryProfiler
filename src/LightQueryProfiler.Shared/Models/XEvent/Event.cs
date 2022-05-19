using System.Xml.Serialization;

namespace LightQueryProfiler.Shared.Models.XEvent
{
    [XmlRoot(ElementName = "event")]
    public class Event
    {
        [XmlElement(ElementName = "data")]
        public List<Data>? Data { get; set; }

        [XmlAttribute(AttributeName = "name")]
        public string? Name { get; set; }

        [XmlAttribute(AttributeName = "package")]
        public string? Package { get; set; }

        [XmlAttribute(AttributeName = "timestamp")]
        public DateTime Timestamp { get; set; }

        [XmlElement(ElementName = "action")]
        public List<Action>? Action { get; set; }
    }
}