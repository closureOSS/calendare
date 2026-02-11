using System;
using System.Threading.Tasks;
using System.Xml.Linq;
using Calendare.Server.Constants;
using Calendare.Server.Repository;

namespace Calendare.Server.Models.DavProperties;

public static partial class PropertiesDefinition
{
    public static DavPropertyRepository PrincipalProperties(this DavPropertyRepository repo)
    {
        // TODO: principal was a call parameter and not (always) resource.Owner

        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc4918#section-15.2
            Name = XmlNs.Dav + "displayname",
            TypeRestrictions = [DavResourceType.Principal],
            GetValue = (prop, qry, resource, ctx) =>
            {
                var principal = resource.Owner;// TODO: HACK REPLACE
                if (!string.IsNullOrEmpty(principal.DisplayName))
                {
                    prop.Value = principal.DisplayName;
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            Update = (prop, resource, collection, ctx) =>
            {
                var principal = resource.Owner;// TODO: HACK REPLACE
                if (principal is not null)
                {
                    principal.DisplayName = prop.Value;
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            Matches = (resource, searchTerm) =>
            {
                var principal = resource.Owner; // TODO: HACK REPLACE
                return (principal?.DisplayName ?? "").Contains(searchTerm ?? "", StringComparison.InvariantCultureIgnoreCase);
            },
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc4918#section-15.9
            Name = XmlNs.Dav + "resourcetype",
            TypeRestrictions = [DavResourceType.Principal],
            GetValue = (prop, qry, resource, ctx) =>
            {
                prop.Add(new XElement(CollectionNames.Collection));
                prop.Add(new XElement(CollectionNames.Principal));
                if (resource.IsProxyRead())
                {
                    prop.Add(new XElement(CollectionNames.ProxyRead));
                }
                if (resource.IsProxyWrite())
                {
                    prop.Add(new XElement(CollectionNames.ProxyWrite));
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc3253#section-3.1.3
            Name = XmlNs.Dav + "supported-method-set",
            TypeRestrictions = [DavResourceType.Principal],
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                foreach (var method in new string[] {
                    "OPTIONS", "PROPFIND", "REPORT", "DELETE", // "LOCK", "UNLOCK",
                    "MOVE", "GET", "HEAD", "MKCOL", "MKCALENDAR", "PROPPATCH",
                    "BIND", "ACL",
                })
                {
                    prop.Add(new XElement(XmlNs.Dav + "supported-method", new XAttribute("name", method)));
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc3744#section-5.1
            Name = XmlNs.Dav + "owner",
            TypeRestrictions = [DavResourceType.Principal],
            GetValue = (prop, qry, resource, ctx) =>
            {
                var principal = resource.Owner;// TODO: HACK REPLACE
                prop.Add(new XElement(XmlNs.Dav + "href", $"{resource.PathBase}{principal.Uri}"));
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // caldav-notifications.txt
            //
            // The notification collection referenced by the CS:notification-URL
            // (Section 4.1.1) property MUST have a DAV:resourcetype property with
            // DAV:collection and CS:notifications (Section 4.3.1) child elements.
            Name = XmlNs.Dav + "notification-URL",
            TypeRestrictions = [DavResourceType.Principal],
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                prop.Add(new XElement(XmlNs.Dav + "href", $"{resource.PathBase}{resource.CurrentUser.Uri}{CollectionUris.Notifications}/"));
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        }, XmlNs.CalenderServer + "notification-URL");
        repo.Register(new DavProperty
        {
            Name = XmlNs.IceWarp + "default-calendar-URL",
            TypeRestrictions = [DavResourceType.Principal],
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                prop.Add(new XElement(XmlNs.Dav + "href", $"{resource.PathBase}{resource.CurrentUser.Uri}{CollectionUris.DefaultCalendar}/"));
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            Name = XmlNs.IceWarp + "default-contacts-URL",
            TypeRestrictions = [DavResourceType.Principal],
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                prop.Add(new XElement(XmlNs.Dav + "href", $"{resource.PathBase}{resource.CurrentUser.Uri}{CollectionUris.DefaultAddressbook}/"));
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            Name = XmlNs.IceWarp + "default-notes-URL",
            TypeRestrictions = [DavResourceType.Principal],
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                prop.Add(new XElement(XmlNs.Dav + "href", $"{resource.PathBase}{resource.CurrentUser.Uri}{CollectionUris.DefaultCalendar}/"));
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            Name = XmlNs.IceWarp + "default-tasks-URL",
            TypeRestrictions = [DavResourceType.Principal],
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                prop.Add(new XElement(XmlNs.Dav + "href", $"{resource.PathBase}{resource.CurrentUser.Uri}{CollectionUris.DefaultCalendar}/"));
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });

        return repo;
    }
}
