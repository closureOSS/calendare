using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Linq;
using Calendare.Server.Constants;
using Calendare.Server.Repository;
using Calendare.Server.Utils;

namespace Calendare.Server.Models.DavProperties;

public static partial class PropertiesDefinition
{
    public static DavPropertyRepository UserProperties(this DavPropertyRepository repo)
    {
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc4918#section-15.5
            Name = XmlNs.Dav + "getcontenttype",
            TypeRestrictions = [DavResourceType.Principal, DavResourceType.User, DavResourceType.Root],
            GetValue = (prop, qry, resource, ctx) =>
            {
                prop.Value = "httpd/unix-directory";
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        // TODO: Investigate if non-standard ctag is used (or just use etag anyway) by clients
        repo.Register(new DavProperty
        {
            Name = XmlNs.CalenderServer + "getctag",
            TypeRestrictions = [DavResourceType.Principal, DavResourceType.User, DavResourceType.Root],
            IsExpensive = true,
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc6638#section-2.4.2
            Name = XmlNs.Caldav + "calendar-user-type",
            TypeRestrictions = [DavResourceType.Principal, DavResourceType.User, DavResourceType.Root],
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                if (resource.Owner.PrincipalType is not null)
                {
                    prop.Value = resource.Owner.PrincipalType.Label;
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            Update = (prop, resource, collection, ctx) =>
            {
                // TODO: Implement updating principal type (i.e. ROOM, RESOURCE, INDIVIDUAL)
                //       Some constraints may apply
                return Task.FromResult(PropertyUpdateResult.Ignore);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc3253#section-3.1.3
            Name = XmlNs.Dav + "supported-report-set",
            TypeRestrictions = [DavResourceType.Principal, DavResourceType.User, DavResourceType.Root],
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                prop.AddSupportedReports(CommonReports);
                prop.AddSupportedReports([
                    XmlNs.Dav + "acl-principal-prop-set"
                ]);
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            Name = XmlNs.Dav + "description",
            TypeRestrictions = [DavResourceType.Principal, DavResourceType.User, DavResourceType.Root],
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                if (!string.IsNullOrEmpty(resource.Owner.Description))
                {
                    prop.Value = resource.Owner.Description;
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            Update = (prop, resource, collection, ctx) =>
            {
                collection.Description = prop.InnerXMLToString();
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            Remove = (prop, resource, collection, ctx) =>
            {
                collection.Description = null;
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });

        return repo;
    }

    public static readonly List<XName> CommonReports = [
        XmlNs.Dav + "principal-property-search",
        XmlNs.Dav + "principal-search-property-set",
        XmlNs.Dav + "expand-property",
        XmlNs.Dav + "principal-match",
        XmlNs.Dav + "sync-collection",
    ];
}
