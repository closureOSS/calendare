using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Calendare.Data.Models;
using Calendare.Server.Constants;

namespace Calendare.Server.Models;

public static class XElementExtensions
{
    public static XDocument CreateDocument(this XElement root)
    {
        var xmlDoc = new XDocument(root);
        if (xmlDoc.Root is null) throw new InvalidOperationException("XDocument must contain a root object");
        xmlDoc.Root.Add(new XAttribute(XNamespace.Xmlns + XmlNs.CaldavPrefix, XmlNs.Caldav));
        xmlDoc.Root.Add(new XAttribute(XNamespace.Xmlns + XmlNs.CalenderServerPrefix, XmlNs.CalenderServer));
        xmlDoc.Root.Add(new XAttribute(XNamespace.Xmlns + XmlNs.AppleIcalPrefix, XmlNs.AppleIcal));
        xmlDoc.Root.Add(new XAttribute(XNamespace.Xmlns + XmlNs.CarddavPrefix, XmlNs.Carddav));
        return xmlDoc;
    }

    public static XElement AddMissingPrivileges(this XElement needPrivileges, string href, PrivilegeMask privileges)
    {
        var xmlResource = new XElement(XmlNs.Dav + "resource", new XElement(XmlNs.Dav + "href", href));
        var xmlPrivilege = new XElement(XmlNs.Dav + "privilege");
        xmlResource.Add(xmlPrivilege);
        foreach (var privilege in PrivilegesDefinitions.LoadList(privileges))
        {
            xmlPrivilege.Add(new XElement(privilege.Id));
        }
        needPrivileges.Add(xmlResource);
        return needPrivileges;
    }


    public static XElement AddSupportedReports(this XElement prop, List<XName> reports)
    {
        foreach (var rpt in reports)
        {
            var xmlReport = new XElement(XmlNs.Dav + "supported-report", new XElement(XmlNs.Dav + "report", new XElement(rpt)));
            prop.Add(xmlReport);
        }
        return prop;
    }

    public static XElement WritePrivilegeSet(this XElement target, PrivilegeItem priv)
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
        target.Add(xmlPriv);
        return xmlPriv;
    }
}
