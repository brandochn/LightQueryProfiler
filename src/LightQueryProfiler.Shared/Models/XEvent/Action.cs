using System.Xml.Serialization;

namespace LightQueryProfiler.Shared.Models.XEvent
{
    [XmlRoot(ElementName = "action")]
    public class Action
    {
        [XmlElement(ElementName = "type")]
        public Type? Type { get; set; }

        [XmlElement(ElementName = "value")]
        public string? Value { get; set; }

        [XmlAttribute(AttributeName = "name")]
        public string? Name { get; set; }

        [XmlAttribute(AttributeName = "package")]
        public string? Package { get; set; }
    }
}