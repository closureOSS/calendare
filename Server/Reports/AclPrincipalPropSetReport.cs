using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Xml.Linq;
using Calendare.Data.Models;
using Calendare.Server.Handlers;
using Calendare.Server.Middleware;
using Calendare.Server.Models;
using Calendare.Server.Repository;
using Calendare.Server.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Calendare.Server.Reports;

/// <summary>
/// https://datatracker.ietf.org/doc/html/rfc3744#section-9.2
/// </summary>
public class AclPrincipalPropSetReport : ReportBase, IReport
{
    public async Task<ReportResponse> Report(XDocument xmlRequestDoc, DavResource resource, List<DavPropertyRef> properties, HttpContext httpContext)
    {
        var depth = httpContext.Request.GetDepth(0);
        if (xmlRequestDoc is null || xmlRequestDoc.Root is null || !resource.Exists || depth != 0)
        {
            return new(HttpStatusCode.BadRequest);
        }
        if (!(resource.Privileges.HasFlag(PrivilegeMask.ReadAcl) || resource.Privileges.HasFlag(PrivilegeMask.Write)))
        {
            return new(PrivilegeMask.ReadAcl);
        }
        var principals = new HashSet<Models.Principal>
        {
            resource.CurrentUser
        };
        var userRepository = httpContext.RequestServices.GetRequiredService<UserRepository>();
        var propertyRegistry = httpContext.RequestServices.GetRequiredService<DavPropertyRepository>();
        var resourceRepository = httpContext.RequestServices.GetRequiredService<ResourceRepository>();
        var aceList = await userRepository.GetPrivilegesGrantedToAsync(resource, true, httpContext.RequestAborted);
        var relationships = await resourceRepository.ListPrincipalsAsResourceAsync(resource, onlySelf: false, httpContext.RequestAborted);
        foreach (var principal in aceList.Where(ace => ace.Grantee is not null).Select(ace => ace.Grantee!))
        {
            principals.Add(principal);
        }
        var (xmlDoc, xmlMultistatus) = HandlerExtensions.CreateMultistatusDocument();
        foreach (var principal in principals.OrderBy(x => x.Uri, StringComparer.OrdinalIgnoreCase))
        {
            var principalResource = ToContext(principal, resource);
            var xmlResponse = await HandlerExtensions.PropertyResponse(propertyRegistry, principalResource, principal.Uri, properties, httpContext);
            xmlMultistatus.Add(xmlResponse);
        }
        return new(xmlDoc);
    }

    private static DavResource ToContext(Principal principal, DavResource parent)
    {
        var uri = new CaldavUri(principal.Uri);
        var resource = new DavResource(uri)
        {
            CurrentUser = parent.CurrentUser,
            Owner = principal,
            DavName = uri.Path!,
            PathBase = parent.PathBase,
            // DavEtag = child.Etag,
            Exists = true,
            Parent = parent.Current,
            ParentResourceType = DavResourceType.Root,// CollectionType(parent.Current),
            ResourceType = DavResourceType.Principal,
            //Privilege = child.Id == parent.CurrentUser.Id ? PrivilegeMask.All : parent.Privilege,
            Privileges = PrivilegeMask.All,
        };
        return resource;
    }
}
