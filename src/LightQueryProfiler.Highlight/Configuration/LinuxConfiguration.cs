using System.Xml.Linq;

namespace LightQueryProfiler.Highlight.Configuration
{
    public class LinuxConfiguration : XmlConfiguration
    {
        public LinuxConfiguration()
        {
            XmlDocument = XDocument.Parse(Resources.LinuxDefinitions);
        }
    }
}