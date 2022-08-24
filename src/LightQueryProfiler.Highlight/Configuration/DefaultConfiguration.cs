using System.Xml.Linq;

namespace LightQueryProfiler.Highlight.Configuration
{
    public class DefaultConfiguration : XmlConfiguration
    {
        public DefaultConfiguration()
        {
            XmlDocument = XDocument.Parse(Resources.DefaultDefinitions);
        }
    }
}