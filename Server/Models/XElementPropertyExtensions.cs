using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Calendare.Server.Constants;

namespace Calendare.Server.Models;

public static class XElementPropertyExtensions
{
    public static List<DavPropertyRef> GetProperties(this XDocument xml) => xml.Root is not null ? xml.Root.GetProperties() : [];

    public static List<DavPropertyRef> GetProperties(this XElement xml)
    {
        var xmlProp = xml.Element(XmlNs.Dav + "prop");
        if (xmlProp is null)
        {
            return [];
        }
        var properties = new List<DavPropertyRef>();
        foreach (var subNode in xmlProp.Elements())
        {
            if (subNode.Name == XmlNs.Dav + "allprop")
            {
                return [];
            }
            // Log.Debug("Adding {prop}", subNode.Name);
            properties.Add(new DavPropertyRef
            {
                Name = subNode.Name,
                Element = subNode,
                IsExpensive = false,
            });
        }
        return properties;
    }

    public static List<DavPropertyStatic> GetPropertiesStatic(this XDocument xml) => xml.Root is not null ? xml.Root.GetPropertiesStatic() : [];

    public static List<DavPropertyStatic> GetPropertiesStatic(this XElement xml)
    {
        var properties = new List<DavPropertyStatic>();

        var xmlSet = xml.Element(XmlNs.Dav + "set");
        if (xmlSet is not null)
        {
            properties.AddRange(GetPropertyStaticList(xmlSet));
        }
        var xmlRemove = xml.Element(XmlNs.Dav + "remove");
        if (xmlRemove is not null)
        {
            properties.AddRange(GetPropertyStaticList(xmlRemove, true));
        }
        return properties;
    }

    private static List<DavPropertyStatic> GetPropertyStaticList(XElement xml, bool isDelete = false)
    {
        var xmlProp = xml.Element(XmlNs.Dav + "prop");
        if (xmlProp is null)
        {
            return [];
        }
        return [.. xmlProp.Elements().Select(subNode => new DavPropertyStatic { Name = subNode.Name, Value = subNode, ToDelete = isDelete, })];
    }

}
