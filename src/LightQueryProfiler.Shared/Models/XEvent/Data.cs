using System.Xml.Serialization;

namespace LightQueryProfiler.Shared.Models.XEvent
{
    [XmlRoot(ElementName = "data")]
    public class Data
    {
        [XmlElement(ElementName = "type")]
        public Type? Type { get; set; }

        [XmlElement(ElementName = "value")]
        public string? Value { get; set; }

        [XmlAttribute(AttributeName = "name")]
        public string? Name { get; set; }
    }
}