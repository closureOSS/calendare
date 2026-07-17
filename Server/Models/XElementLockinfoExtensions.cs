using System.Xml.Linq;
using Calendare.Server.Constants;
using Calendare.Server.Utils;

namespace Calendare.Server.Models;

public enum DavLockScope
{
    Unknown,
    Exclusive,
    Shared,
}

public enum DavLockType
{
    Unknown,
    Write,
}

public class DavLock
{
    public DavLockScope Scope { get; set; } = DavLockScope.Unknown;
    public DavLockType Type { get; set; } = DavLockType.Unknown;
    public XElement? Owner { get; set; }
    public int Timeout { get; set; } = int.MaxValue;
    public bool InfiniteDepth { get; set; }
    public string? Token { get; set; }
}

public static class XElementLockinfoExtensions
{
    public static DavLock? GetLockinfo(this XDocument xml, DavLock davLock) => xml.Root?.GetLockinfo(davLock);

    public static DavLock? GetLockinfo(this XElement xml, DavLock davLock)
    {
        // <!ELEMENT lockinfo (lockscope, locktype, owner?)  >
        // <!ELEMENT lockscope (exclusive | shared) >
        var xmlLockscope = xml.Element(XmlNs.Dav + "lockscope");
        if (xmlLockscope is not null)
        {
            if (!xmlLockscope.IsEmpty)
            {
                var exclusive = xmlLockscope.Element(XmlNs.Dav + "exclusive");
                if (exclusive is not null)
                {
                    davLock.Scope = DavLockScope.Exclusive;
                }
                var shared = xmlLockscope.Element(XmlNs.Dav + "shared");
                if (shared is not null)
                {
                    davLock.Scope = DavLockScope.Shared;
                }
            }
        }
        // <!ELEMENT locktype (write) >
        var xmlLockType = xml.Element(XmlNs.Dav + "locktype");
        if (xmlLockType is not null)
        {
            if (!xmlLockType.IsEmpty)
            {
                var write = xmlLockType.Element(XmlNs.Dav + "write");
                if (write is not null)
                {
                    davLock.Type = DavLockType.Write;
                }
            }
        }
        // <!ELEMENT owner ANY >
        var xmlOwner = xml.Element(XmlNs.Dav + "owner");
        if (xmlOwner is not null)
        {
            davLock.Owner = xmlOwner;
        }
        return davLock;
    }

    public static XDocument LockResponse(DavLock davLock, DavResource resource)
    {
        var xmlProp = new XElement(XmlNs.Dav + "prop");
        var xmlLockDiscovery = new XElement(XmlNs.Dav + "lockdiscovery");
        AddActiveLocks(xmlLockDiscovery, davLock, resource);
        xmlProp.Add(xmlLockDiscovery);
        var xmlResponse = xmlProp.CreateDocument();
        return xmlResponse;
    }

    public static XElement AddActiveLocks(XElement xmlLockDiscovery, DavLock? davLock, DavResource resource)
    {
        if (davLock is not null)
        {
            var xmlActiveLock = new XElement(XmlNs.Dav + "activelock");
            xmlLockDiscovery.Add(xmlActiveLock);
            if (davLock.Type == DavLockType.Write)
            {
                var xmlLockType = new XElement(XmlNs.Dav + "locktype");
                xmlLockType.Add(new XElement(XmlNs.Dav + "write"));
                xmlActiveLock.Add(xmlLockType);
            }
            if (davLock.Scope != DavLockScope.Unknown)
            {
                var xmlLockScope = new XElement(XmlNs.Dav + "lockscope");
                xmlLockScope.Add(new XElement(XmlNs.Dav + (davLock.Scope == DavLockScope.Exclusive ? "exclusive" : "shared")));
                xmlActiveLock.Add(xmlLockScope);
            }
            var xmlDepth = new XElement(XmlNs.Dav + "depth")
            {
                Value = davLock.InfiniteDepth ? "infinity" : "0",
            };
            xmlActiveLock.Add(xmlDepth);

            if (davLock.Owner is not null)
            {
                var xmlOwner = new XElement(XmlNs.Dav + "owner");
                xmlOwner.Add(davLock.Owner);
                xmlActiveLock.Add(xmlOwner);
            }

            var xmlTimeout = new XElement(XmlNs.Dav + "timeout")
            {
                Value = $"Second-{davLock.Timeout}",
            };
            xmlActiveLock.Add(xmlTimeout);

            var xmlLockToken = new XElement(XmlNs.Dav + "locktoken");
            xmlLockToken.Add(new XElement(XmlNs.Dav + "href", davLock.Token));
            xmlActiveLock.Add(xmlLockToken);

            var xmlLockRoot = new XElement(XmlNs.Dav + "lockroot");
            xmlLockRoot.Add(new XElement(XmlNs.Dav + "href", resource.DavName));
            xmlActiveLock.Add(xmlLockRoot);
        }
        return xmlLockDiscovery;
    }

    public static XElement SupportedLock()
    {
        var xmlLockEntry = new XElement(XmlNs.Dav + "lockentry");
        xmlLockEntry.Add(new XElement(XmlNs.Dav + "lockscope", new XElement(XmlNs.Dav + "exclusive")), new XElement(XmlNs.Dav + "locktype", new XElement(XmlNs.Dav + "write")));
        return xmlLockEntry;
    }
}
