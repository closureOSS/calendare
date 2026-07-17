using System.Threading.Tasks;
using Calendare.Server.Constants;
using Calendare.Server.Repository;
using Calendare.Server.Utils;
using NodaTime;

namespace Calendare.Server.Models.DavProperties;

public static partial class PropertiesDefinition
{
    public static DavPropertyRepository ObjectBlobProperties(this DavPropertyRepository repo)
    {
        // Compare with https://docs.nextcloud.com/server/stable/developer_manual/client_apis/WebDAV/basic.html
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc4918#section-15.2
            Name = XmlNs.Dav + "displayname",
            TypeRestrictions = [DavResourceType.BlobItem],
            GetValue = (prop, qry, resource, ctx) =>
            {
                if (!string.IsNullOrEmpty(resource.Object?.BlobItem?.DisplayName))
                {
                    prop.Value = resource.Object.BlobItem.DisplayName;
                }
                else
                {
                    if (!string.IsNullOrEmpty(resource.Uri.ItemName))
                    {
                        prop.Value = resource.Uri.ItemName;
                    }
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            Update = (prop, resource, collection, ctx) =>
            {
                if (resource.Object?.BlobItem is null)
                {
                    return Task.FromResult(PropertyUpdateResult.BadRequest);
                }
                resource.Object.BlobItem.DisplayName = prop.Value;
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            Matches = (resource, searchTerm) =>
            {
                if (resource.Object?.BlobItem is null)
                {
                    return false;
                }
                return (resource.Object.BlobItem.DisplayName ?? "").Contains(searchTerm ?? "", System.StringComparison.InvariantCultureIgnoreCase);
            },
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc4918#section-15.7
            Name = XmlNs.Dav + "getlastmodified",
            TypeRestrictions = [DavResourceType.BlobItem],
            GetValue = (prop, qry, resource, ctx) =>
            {
                if (resource.Object is not null && resource.Object.BlobItem is not null)
                {
                    prop.Value = resource.Object.BlobItem.Modified.ToRfc2616();
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            Update = (prop, resource, collection, ctx) =>
            {
                if (resource.Object is null || resource.Object.BlobItem is null)
                {
                    return Task.FromResult(PropertyUpdateResult.BadRequest);
                }
                if (!HttpDateParser.TryParseRfc2616(prop.Value, out var modDate))
                {
                    return Task.FromResult(PropertyUpdateResult.BadRequest);
                }
                resource.Object.BlobItem.Modified = Instant.FromDateTimeOffset(modDate);
                return Task.FromResult(PropertyUpdateResult.Success);
            },
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc4918#section-15.1
            Name = XmlNs.Dav + "creationdate",
            TypeRestrictions = [DavResourceType.BlobItem],
            GetValue = (prop, qry, resource, ctx) =>
            {
                if (resource.Object is not null && resource.Object.BlobItem is not null)
                {
                    prop.Value = resource.Object.BlobItem.Created.ToRfc3339();
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            Update = (prop, resource, collection, ctx) =>
            {
                if (resource.Object is null || resource.Object.BlobItem is null)
                {
                    return Task.FromResult(PropertyUpdateResult.BadRequest);
                }
                if (!HttpDateParser.TryParseRfc2616(prop.Value, out var modDate))
                {
                    return Task.FromResult(PropertyUpdateResult.BadRequest);
                }
                resource.Object.BlobItem.Created = Instant.FromDateTimeOffset(modDate);
                return Task.FromResult(PropertyUpdateResult.Success);
            },
        });
        repo.Register(new DavProperty
        {
            Name = XmlNs.Dav + "getcontentlanguage",
            TypeRestrictions = [DavResourceType.BlobItem],
            GetValue = (prop, qry, resource, ctx) =>
            {
                if (resource.Object is not null && resource.Object.BlobItem is not null && !string.IsNullOrEmpty(resource.Object.BlobItem.LanguageCode))
                {
                    prop.Value = resource.Object.BlobItem.LanguageCode;
                }
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            Update = (prop, resource, collection, ctx) =>
            {
                if (resource.Object is null || resource.Object.BlobItem is null)
                {
                    return Task.FromResult(PropertyUpdateResult.BadRequest);
                }
                resource.Object.BlobItem.LanguageCode = prop.Value;
                return Task.FromResult(PropertyUpdateResult.Success);
            },
            Remove = (prop, resource, collection, ctx) =>
            {
                if (resource.Object is null || resource.Object.BlobItem is null)
                {
                    return Task.FromResult(PropertyUpdateResult.BadRequest);
                }
                resource.Object.BlobItem.LanguageCode = null;
                return Task.FromResult(PropertyUpdateResult.Success);
            },
        });
        //  xmlns:Z="urn:schemas-microsoft-com:"
        // TODO: <Z:Win32LastModifiedTime>Wed, 03 Jun 2026 14:50:38 GMT</Z:Win32LastModifiedTime>
        // TODO: <Z:Win32FileAttributes>00002020</Z:Win32FileAttributes>
        // TODO: <Z:Win32CreationTime>Wed, 03 Jun 2026 14:50:38 GMT</Z:Win32CreationTime>
        // TODO: <Z:Win32LastAccessTime>Thu, 11 Jun 2026 09:26:44 GMT</Z:Win32LastAccessTime>
        // repo.Register(new DavProperty
        // {
        //      MS Windows NS not XmlNs.Dav
        //     Name = XmlNs.Dav + "lastaccesstime",
        //     TypeRestrictions = [DavResourceType.BlobItem],
        //     GetValue = (prop, qry, resource, ctx) =>
        //     {
        //         if (resource.Object is not null && resource.Object.BlobItem is not null)
        //         {
        //             prop.Value = resource.Object.BlobItem.LastAccess.ToRfc2616();
        //         }
        //         return Task.FromResult(PropertyUpdateResult.Success);
        //     },
        // });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc4918#section-15.5
            Name = XmlNs.Dav + "getcontenttype",
            TypeRestrictions = [DavResourceType.BlobItem],
            GetValue = (prop, qry, resource, ctx) =>
            {
                prop.Value = resource.Object?.BlobItem?.ContentType ?? "application/octet-stream";
                return Task.FromResult(PropertyUpdateResult.Success);
            },
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc4918#section-15.4
            Name = XmlNs.Dav + "getcontentlength",
            TypeRestrictions = [DavResourceType.BlobItem],
            // IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                prop.Value = $"{resource.Object?.BlobItem?.ContentLength ?? 0}";
                return Task.FromResult(PropertyUpdateResult.Success);
            },
        });
        repo.Register(new DavProperty
        {
            // https://datatracker.ietf.org/doc/html/rfc3253#section-3.1.5
            Name = XmlNs.Dav + "supported-report-set",
            TypeRestrictions = [DavResourceType.BlobItem],
            IsExpensive = true,
            GetValue = (prop, qry, resource, ctx) =>
            {
                prop.AddSupportedReports(CommonReports);
                return Task.FromResult(PropertyUpdateResult.Success);
            },
        });
        // TODO: <executable xmlns="http://apache.org/dav/props/" />
        /* RFC 3253
        <D:checked-in>
          <D:href>/repo/history/v/1.2</D:href>
        </D:checked-in>
          <D:checked-out>
                    <D:href>/repo/history/1/W3</D:href>
                </D:checked-out>
        */
        return repo;
    }
}
