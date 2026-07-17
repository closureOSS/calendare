using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Calendare.Data.Models;
using Calendare.Server.Constants;
using Calendare.Server.Utils;

namespace Calendare.Server.Models;

public static class XElementExtensions
{
    extension(XElement element)
    {
        public XDocument CreateDocument()
        {
            var xmlDoc = new XDocument(element);
            if (xmlDoc.Root is null) throw new InvalidOperationException("XDocument must contain a root object");
            // xmlDoc.Root.Add(new XAttribute(XNamespace.Xmlns + XmlNs.DavPrefix, XmlNs.Dav));
            xmlDoc.Root.Add(new XAttribute(XNamespace.Xmlns + XmlNs.CaldavPrefix, XmlNs.Caldav));
            xmlDoc.Root.Add(new XAttribute(XNamespace.Xmlns + XmlNs.CalenderServerPrefix, XmlNs.CalenderServer));
            xmlDoc.Root.Add(new XAttribute(XNamespace.Xmlns + XmlNs.AppleIcalPrefix, XmlNs.AppleIcal));
            xmlDoc.Root.Add(new XAttribute(XNamespace.Xmlns + XmlNs.CarddavPrefix, XmlNs.Carddav));
            return xmlDoc;
        }

        public XElement AddMissingPrivileges(string href, PrivilegeMask privileges)
        {
            var xmlResource = new XElement(XmlNs.Dav + "resource", new XElement(XmlNs.Dav + "href", UriUtils.ToEscapedUri(href)));
            var xmlPrivilege = new XElement(XmlNs.Dav + "privilege");
            xmlResource.Add(xmlPrivilege);
            foreach (var privilege in PrivilegesDefinitions.LoadList(privileges))
            {
                xmlPrivilege.Add(new XElement(privilege.Id));
            }
            element.Add(xmlResource);
            return element;
        }

        public XElement AddSupportedReports(List<XName> reports)
        {
            foreach (var rpt in reports)
            {
                var xmlReport = new XElement(XmlNs.Dav + "supported-report", new XElement(XmlNs.Dav + "report", new XElement(rpt)));
                element.Add(xmlReport);
            }
            return element;
        }

        public XElement WritePrivilegeSet(PrivilegeItem priv)
        {
            var xmlPriv = new XElement(XmlNs.Dav + "supported-privilege", new XElement(XmlNs.Dav + "privilege", new XElement(priv.Id)));
            if (!string.IsNullOrEmpty(priv.Description))
            {
                xmlPriv.Add(new XElement(XmlNs.Dav + "description", priv.Description));
            }
            if (priv.Items is not null && priv.Items.Count > 0)
            {
                foreach (var subPriv in priv.Items)
                {
                    xmlPriv.WritePrivilegeSet(subPriv);
                }
            }
            element.Add(xmlPriv);
            return xmlPriv;
        }
    }
}
