using System.Threading.Tasks;
using Calendare.Server.Constants;
using Calendare.Server.Repository;

namespace Calendare.Server.Models.DavProperties;

public static partial class PropertiesDefinition
{
    public static DavPropertyRepository ObjectAddressbookProperties(this DavPropertyRepository repo)
    {
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc4918#section-15.2
            Name = XmlNs.Dav + "displayname",
            TypeRestrictions = [DavResourceType.AddressbookItem],
            GetValue = (prop, qry, resource, ctx) =>
            {
                prop.Value = resource.Object?.AddressItem?.FormattedName ?? "";
                return Task.FromResult(PropertyUpdateResult.Success);
            },
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc4918#section-15.5
            Name = XmlNs.Dav + "getcontenttype",
            TypeRestrictions = [DavResourceType.AddressbookItem],
            GetValue = (prop, qry, resource, ctx) =>
            {
                prop.Value = MimeContentTypes.VCard;
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc6352#section-10.4
            // TODO: Not supported CARDDAV:prop https://datatracker.ietf.org/doc/html/rfc6352#section-10.4.2
            Name = XmlNs.Carddav + "address-data",
            TypeRestrictions = [DavResourceType.AddressbookItem],
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                if (resource.Object is not null && resource.Object.RawData is not null) prop.Value = resource.Object.RawData;
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc3253#section-3.1.5
            Name = XmlNs.Dav + "supported-report-set",
            TypeRestrictions = [DavResourceType.AddressbookItem],
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                prop.AddSupportedReports(CommonReports);
                prop.AddSupportedReports([
                    XmlNs.Caldav + "addressbook-query",
                    XmlNs.Caldav + "addressbook-multiget"
                ]);
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        return repo;
    }
}
