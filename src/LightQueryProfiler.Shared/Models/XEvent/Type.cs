using System.Xml.Serialization;

namespace LightQueryProfiler.Shared.Models.XEvent
{
    [XmlRoot(ElementName = "type")]
    public class Type
    {
        [XmlAttribute(AttributeName = "name")]
        public string? Name { get; set; }

        [XmlAttribute(AttributeName = "package")]
        public string? Package { get; set; }
    }
}