using System.Xml.Serialization;

namespace LightQueryProfiler.Shared.Helpers
{
    public static class XmlHelper
    {
        public static T? Deserialize<T>(string xml)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            T? result;
            using (TextReader reader = new StringReader(xml))
            {
                result = (T?)serializer.Deserialize(reader);
            }

            return result == null ? default : result;
        }
    }
}