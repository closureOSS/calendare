using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Calendare.Server.Constants;
using Calendare.Server.Repository;
using Calendare.Server.Utils;

namespace Calendare.Server.Models.DavProperties;

public static partial class PropertiesDefinition
{
    public static DavPropertyRepository ObjectProperties(this DavPropertyRepository repo)
    {
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc3744#section-5.1
            Name = XmlNs.Dav + "owner",
            TypeRestrictions = [DavResourceType.AddressbookItem, DavResourceType.CalendarItem],
            GetValue = (prop, qry, resource, ctx) =>
            {
                if (resource.Object is not null)
                {
                    prop.Add(new XElement(XmlNs.Dav + "href", $"{resource.PathBase}/{resource.Object.Owner?.Username ?? resource.Owner.Username}/"));
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc3253#section-3.1.3
            Name = XmlNs.Dav + "supported-method-set",
            TypeRestrictions = [DavResourceType.AddressbookItem, DavResourceType.CalendarItem],
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                foreach (var method in new string[] {
                    "OPTIONS","PROPFIND","REPORT","DELETE",
                    // "LOCK","UNLOCK",
                    "MOVE","GET","HEAD","PUT",
                })
                {
                    prop.Add(new XElement(XmlNs.Dav + "supported-method", new XAttribute("name", method)));
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc4918#section-15.6
            Name = XmlNs.Dav + "getetag",
            TypeRestrictions = [DavResourceType.AddressbookItem, DavResourceType.CalendarItem],
            GetValue = (prop, qry, resource, ctx) =>
            {
                if (!string.IsNullOrEmpty(resource.DavEtag)) prop.Value = $"\"{resource.DavEtag}\"";
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc4918#section-15.4
            Name = XmlNs.Dav + "getcontentlength",
            TypeRestrictions = [DavResourceType.AddressbookItem, DavResourceType.CalendarItem],
            GetValue = (prop, qry, resource, ctx) =>
            {
                if (resource.Object is not null)
                {
                    prop.Value = $"{Encoding.UTF8.GetByteCount(resource.Object.RawData)}";
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc4918#section-15.7
            Name = XmlNs.Dav + "getlastmodified",
            TypeRestrictions = [DavResourceType.AddressbookItem, DavResourceType.CalendarItem],
            GetValue = (prop, qry, resource, ctx) =>
            {
                if (resource.Object is not null)
                {
                    prop.Value = resource.Object.Modified.ToRfc2616();
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc4918#section-15.1
            Name = XmlNs.Dav + "creationdate",
            TypeRestrictions = [DavResourceType.AddressbookItem, DavResourceType.CalendarItem],
            GetValue = (prop, qry, resource, ctx) =>
            {
                if (resource.Object is not null)
                {
                    prop.Value = resource.Object.Created.ToRfc3339();
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc4918#section-15.9
            Name = XmlNs.Dav + "resourcetype",
            TypeRestrictions = [DavResourceType.AddressbookItem, DavResourceType.CalendarItem],
            GetValue = (prop, qry, resource, ctx) =>
            {
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc3744#section-5.7
            Name = XmlNs.Dav + "inherited-acl-set",
            TypeRestrictions = [DavResourceType.AddressbookItem, DavResourceType.CalendarItem],
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                prop.Add(new XElement(XmlNs.Dav + "href", $"{resource.PathBase}{resource.Parent?.Uri ?? resource.DavName}"));
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });

        return repo;
    }
}
