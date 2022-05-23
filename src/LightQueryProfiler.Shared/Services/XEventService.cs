using LightQueryProfiler.Shared.Models;
using LightQueryProfiler.Shared.Services.Interfaces;
using System.Xml;

namespace LightQueryProfiler.Shared.Services
{
    public class XEventService : IXEventService
    {
        public XEventService()
        {

        }

        public List<ProfilerEvent> Parser(string xml)
        {
            if (string.IsNullOrEmpty(xml))
            {
                throw new Exception("xml cannot be null or empty");
            }

            var settings = new XmlReaderSettings { ConformanceLevel = ConformanceLevel.Fragment, IgnoreWhitespace = true, IgnoreComments = true };
            XmlReader reader = XmlReader.Create(new StringReader(xml), settings);
            XmlDocument xmlDocument = new XmlDocument();

            //Read the XML File  
            xmlDocument.Load(reader);

            //Create a XML Node List with XPath Expression  
            XmlNodeList? eventNodeList = xmlDocument.SelectNodes("/RingBufferTarget/event");

            if (eventNodeList == null)
            {
                return new List<ProfilerEvent>();
            }

            List<ProfilerEvent> events = new List<ProfilerEvent>();
            string key;
            string? value;

            foreach (XmlNode eventNode in eventNodeList)
            {
                ProfilerEvent profilerEvent = new ProfilerEvent();
                profilerEvent.Fields = new Dictionary<string, object?>();
                profilerEvent.Actions = new Dictionary<string, object?>();

                profilerEvent.Name = eventNode.Attributes?["name"]?.Value;
                profilerEvent.Timestamp = eventNode.Attributes?["timestamp"]?.Value;

                XmlNodeList? fieldsNodeList = eventNode.SelectNodes("data");
                if (fieldsNodeList != null)
                {
                    foreach (XmlNode fieldNode in fieldsNodeList)
                    {
                        key = fieldNode.Attributes?["name"]?.Value ?? "";
                        value = fieldNode["value"]?.InnerText;
                        if (profilerEvent.Fields.ContainsKey(key))
                        {
                            profilerEvent.Fields[key] = value;
                        }
                        else
                        {
                            profilerEvent.Fields.Add(key: key, value: value);
                        }
                    }
                }

                XmlNodeList? actionNodeList = eventNode.SelectNodes("action");
                if (actionNodeList != null)
                {
                    foreach (XmlNode actionNode in actionNodeList)
                    {
                        key = actionNode.Attributes?["name"]?.Value ?? "";
                        value = actionNode["value"]?.InnerText;
                        if (profilerEvent.Actions.ContainsKey(key))
                        {
                            profilerEvent.Actions[key] = value;
                        }
                        else
                        {
                            profilerEvent.Actions.Add(key: key, value: value);
                        }
                    }
                }

                events.Add(profilerEvent);
            }
            return events;
        }
    }
}