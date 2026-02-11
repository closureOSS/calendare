using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Calendare.Data.Models;
using Calendare.Server.Constants;
using Calendare.Server.Middleware;
using Calendare.Server.Models;
using Calendare.Server.Recorder;
using Calendare.Server.Repository;
using Calendare.Server.Utils;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace Calendare.Server.Handlers;

/// <summary>
/// Implementation of the ACL method.
/// </summary>
/// <remarks>
/// The specification of the ACL method can be found in the
/// https://datatracker.ietf.org/doc/html/rfc3744#section-8.1
/// </see>.
/// </remarks>
public class AclHandler : HandlerBase, IMethodHandler
{
    private readonly ResourceRepository ResourceRepository;
    private readonly UserRepository UserRepository;

    public AclHandler(DavEnvironmentRepository env, ResourceRepository resourceRepository, RecorderSession recorder, UserRepository userRepository) : base(env, recorder)
    {
        ResourceRepository = resourceRepository;
        UserRepository = userRepository;
    }

    public async Task HandleRequestAsync(HttpContext httpContext, DavResource resource)
    {
        var request = httpContext.Request;
        var response = httpContext.Response;
        var (xmlRequest, _) = await request.BodyAsXmlAsync(httpContext.RequestAborted);
        if (xmlRequest is null || xmlRequest?.Root is null)
        {
            SetEtagHeader(response, resource.Current?.Etag);
            await WriteErrorXmlAsync(httpContext, HttpStatusCode.BadRequest, XmlNs.Dav + "invalid-xml");
            return;
        }
        if (xmlRequest.Root.Name != XmlNs.Dav + "acl")
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.UnsupportedMediaType);
            return;
        }
        Recorder.SetRequestBody(xmlRequest);

        var grantor = resource.Current ?? await UserRepository.GetPrincipalAsCollectionAsync(resource.Owner.Id, httpContext.RequestAborted);
        if (!resource.Privileges.HasAnyOf(PrivilegeMask.WriteAcl) || grantor is null)
        {
            await WriteErrorNeedPrivilegeAsync(httpContext, resource.DavName, PrivilegeMask.WriteAcl);
            return;
        }
        var aces = await ParseAccessControlEntities(xmlRequest.Root, httpContext, resource.Owner, httpContext.RequestAborted);
        if (aces is null)
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.Forbidden);
            return;
        }
        await UserRepository.AmendRelationshipAsync(grantor, aces, httpContext.RequestAborted);
        await WriteStatusAsync(httpContext, HttpStatusCode.OK);
    }

    private async Task<List<AccessControlEntity>?> ParseAccessControlEntities(XElement xml, HttpContext httpContext, Principal owner, CancellationToken ct)
    {
        var privileges = PrivilegesDefinitions.LoadList(PrivilegeMask.All);
        var result = new List<AccessControlEntity>();
        foreach (var xmlAce in xml.Elements(XmlNs.Dav + "ace"))
        {
            var ace = new AccessControlEntity();
            var xmlPrincipal = xmlAce.Element(XmlNs.Dav + "principal");
            if (xmlPrincipal is null || xmlPrincipal.FirstNode is null)
            {
                Log.Warning("ACE without principal");
                return default;
            }
            var xmlHref = xmlPrincipal.Element(XmlNs.Dav + "href");
            if (xmlHref is not null)
            {
                var principalUri = new CaldavUri(xmlHref.Value, PathBase);
                var principalContext = await ResourceRepository.GetResourceAsync(principalUri, httpContext, ct);
                if (principalContext is not null && principalContext.Exists && principalContext.Owner is not null && principalContext.ResourceType == DavResourceType.Principal)
                {
                    if (principalContext.Current is not null)
                    {
                        ace.Grantee = principalContext.Current.ToPrincipal();
                    }
                    else
                    {
                        ace.Grantee = principalContext.Owner;
                    }
                    ace.Grantee = principalContext.Current is not null ? principalContext.Current.ToPrincipal() : principalContext.Owner;
                }
                else
                {
                    Log.Warning("Href {principal} is not a principal", xmlHref.Value);
                    return default;
                }
            }
            else
            {
                var xmlProperty = xmlPrincipal.Element(XmlNs.Dav + "property");
                if (xmlProperty is not null)
                {
                    if (xmlProperty.FirstNode is not null)
                    {
                        var property = xmlProperty.Elements().First().Name.LocalName;
                        switch (property)
                        {
                            case "owner":
                                ace.Grantee = owner;
                                break;

                            default:
                                Log.Warning("Principal property {property} not supported", property);
                                return default;
                        }
                    }
                    else
                    {
                        Log.Warning("Principal property not set");
                        return default;
                    }
                }
                else if (xmlPrincipal.Element(XmlNs.Dav + "authenticated") is not null)
                {
                    ace.Grantee = null;   // defines default privileges
                }
                else if (xmlPrincipal.Element(XmlNs.Dav + "unauthenticated") is not null)
                {
                    Log.Information("Ignoring ACL for unauthenticated users (not supported)");
                    continue;
                }
                else
                {
                    Log.Warning("Principal not defined");
                    return default;
                }
            }
            var xmlGrant = xmlAce.Element(XmlNs.Dav + "grant");
            var xmlDeny = xmlAce.Element(XmlNs.Dav + "deny");
            if (xmlGrant is not null && xmlDeny is not null)
            {
                Log.Warning("Grant and deny isn't allowed in the same request");
                return default;
            }
            if (xmlGrant is null && xmlDeny is null)
            {
                Log.Warning("Neither grant or deny is defined");
                return default;
            }
            if (xmlDeny is not null)
            {
                Log.Warning("Deny isn't allowed (not supported)");
                return default;
            }
            if (xmlGrant is null)
            {
                Log.Warning("Grants are not defined");
                return default;
            }
            var grants = new List<PrivilegeItem>();
            foreach (var gp in xmlGrant.Elements(XmlNs.Dav + "privilege"))
            {
                var gp1 = gp.Elements().FirstOrDefault();
                if (gp1 is null)
                {
                    break;  // no privilege
                }
                var ggp = privileges.FirstOrDefault(x => x.Id == gp1.Name);
                if (ggp is not null)
                {
                    grants.Add(ggp);
                }
                else
                {
                    Log.Warning("Privilege {privilege} unknown", gp1.Name);
                    // return default;
                }
            }
            ace.Privileges = grants.CalculateMask();
            result.Add(ace);
        }
        return result;
    }
}
