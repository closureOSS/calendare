using System.Threading.Tasks;
using System.Xml.Linq;
using Calendare.Server.Constants;
using Calendare.Server.Repository;

namespace Calendare.Server.Models.DavProperties;

public static partial class PropertiesDefinition
{
    public static DavPropertyRepository RootProperties(this DavPropertyRepository repo)
    {
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc4918#section-15.2
            Name = XmlNs.Dav + "displayname",
            TypeRestrictions = [DavResourceType.Root],
            GetValue = (prop, qry, resource, ctx) =>
            {
                prop.Value = "Calendare Server";
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc4918#section-15.9
            Name = XmlNs.Dav + "resourcetype",
            TypeRestrictions = [DavResourceType.Root],
            GetValue = (prop, qry, resource, ctx) =>
            {
                prop.Add(new XElement(XmlNs.Dav + "collection"));
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        // repo.Register(new DavProperty
        // {
        //     // https://datatracker.ietf.org/doc/html/rfc3744#section-5.4
        //     Name = XmlNamespaces.DavNs + "current-user-privilege-set",
        //     IsExpensive = true,
        //     GetValue = (prop, qry, resource, ctx) =>
        //     {
        //         if (resource is null)
        //         {
        //             return Task.FromResult(PropertyUpdateResult.Success);
        //         }
        //         var grantList = PrivilegeStatic.LoadList(PrivilegeMask.ReadCurrentUserPrivilegeSet | PrivilegeMask.ReadAcl);
        //         foreach (var grant in grantList)
        //         {
        //             prop.Add(new XElement(XmlNamespaces.DavNs + "privilege", new XElement(grant.Id)));
        //         }
        //         return Task.FromResult(PropertyUpdateResult.Success);
        //     }
        // });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc3253#section-3.1.3
            Name = XmlNs.Dav + "supported-method-set",
            TypeRestrictions = [DavResourceType.Root],
            GetValue = (prop, qry, resource, ctx) =>
            {
                foreach (var method in new string[] {
                    "OPTIONS","PROPFIND","REPORT"
                })
                {
                    prop.Add(new XElement(XmlNs.Dav + "supported-method", new XAttribute("name", method)));
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc3744#section-5.7
            Name = XmlNs.Dav + "inherited-acl-set",
            TypeRestrictions = [DavResourceType.Root],
            GetValue = (prop, qry, resource, ctx) =>
            {
                return Task.FromResult(PropertyUpdateResult.Success);
            }
        });
        return repo;
    }
}
