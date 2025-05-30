﻿using System.Xml.Linq;

namespace LightQueryProfiler.Highlight.Extensions
{
    internal static class XmlExtensions
    {
        public static string GetAttributeValue(this XElement element, XName name)
        {
            if (element == null)
            {
                throw new ArgumentNullException("element");
            }

            var attribute = element.Attribute(name);
            if (attribute == null)
            {
#pragma warning disable CS8603 // Possible null reference return.
                return null;
#pragma warning restore CS8603 // Possible null reference return.
            }

            return attribute.Value;
        }
    }
}