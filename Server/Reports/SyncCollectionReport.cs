using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Threading.Tasks;
using System.Xml.Linq;
using Calendare.Server.Api.Models;
using Calendare.Server.Constants;
using Calendare.Server.Handlers;
using Calendare.Server.Models;
using Calendare.Server.Repository;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Calendare.Server.Reports;

/// <summary>
/// https://datatracker.ietf.org/doc/html/rfc6578#section-3.2
/// </summary>
public class SyncCollectionReport : ReportBase, IReport
{
    private ItemRepository? ItemRepository;
    private string PathBase = string.Empty;

    public async Task<ReportResponse> Report(XDocument xmlRequestDoc, DavResource resource, List<DavPropertyRef> properties, HttpContext httpContext)
    {
        if (xmlRequestDoc is null || xmlRequestDoc.Root is null)
        {
            return new(HttpStatusCode.BadRequest);
        }
        var ResourceRepository = httpContext.RequestServices.GetRequiredService<ResourceRepository>();
        ItemRepository = httpContext.RequestServices.GetRequiredService<ItemRepository>();
        var env = httpContext.RequestServices.GetRequiredService<DavEnvironmentRepository>();
        PathBase = env.PathBase;

        var isInfinite = GetIsInfiniteSync(xmlRequestDoc.Root);
        var pageSize = GetLimitResultset(xmlRequestDoc.Root);
        var requestToken = GetSyncToken(xmlRequestDoc.Root);
        // var rangeStart = 0;

        var propertyRegistry = httpContext.RequestServices.GetRequiredService<DavPropertyRepository>();
        if (resource.Current is not null)
        {
            return await SyncCollectionV1(xmlRequestDoc, resource, propertyRegistry, properties, httpContext, env);
        }
        else
        {
            var contexts = new List<DavResource>();
            switch (resource.ResourceType)
            {
                case DavResourceType.Root:
                    contexts.AddRange(await ResourceRepository.ListPrincipalsAsResourceAsync(resource, true, httpContext.RequestAborted));
                    break;
                case DavResourceType.Principal:
                case DavResourceType.Container:
                    contexts.AddRange(await ResourceRepository.ListChildrenAsResourcesAsync(resource, httpContext.RequestAborted));
                    break;
                default:
                    Log.Error("Resource type {resourceType} not supported in this report", resource.ResourceType);
                    break;
            }
            var (xmlDoc, xmlMultistatus) = HandlerExtensions.CreateMultistatusDocument();
            foreach (var ctx in contexts)
            {
                var exists = ctx.VerifyResourceType();
                switch (ctx.ResourceType)
                {
                    case DavResourceType.Principal:
                    case DavResourceType.Container:
                    case DavResourceType.Calendar:
                    case DavResourceType.Addressbook:
                        break;

                    default:
                    case DavResourceType.Unknown:
                    case DavResourceType.Root:
                    case DavResourceType.User:
                    case DavResourceType.CalendarItem:
                    case DavResourceType.AddressbookItem:
                        exists = false;
                        break;
                }
                if (exists)
                {
                    var xmlResponse = await HandlerExtensions.PropertyResponse(propertyRegistry, ctx, null, properties, httpContext);
                    xmlMultistatus.Add(xmlResponse);
                }
                else
                {
                    var xmlResponse = new XElement(XmlNs.Dav + "response",
                                        new XElement(XmlNs.Dav + "href", $"{PathBase}{ctx.DavName}"),
                                        new XElement(XmlNs.Dav + "status", "HTTP/1.1 404 Not Found")
                                    );
                    // TODO: DAV:error missing https://datatracker.ietf.org/doc/html/rfc6578#section-3.2
                    xmlMultistatus.Add(xmlResponse);
                }
            }
            var newToken = SyncToken.Sentinel;
            var xmlSyncToken = new XElement(XmlNs.Dav + "sync-token", newToken.Uri);
            xmlMultistatus.Add(xmlSyncToken);
            return new(xmlDoc);
        }
    }

    private async Task<ReportResponse> SyncCollectionV1(XDocument xmlRequestDoc, DavResource baseResource, DavPropertyRepository propertyRegistry, List<DavPropertyRef> properties, HttpContext httpContext, DavEnvironmentRepository env)
    {
        // List<XElement> filters = GetFilters(xmlRequestDoc) ?? [];
        if (xmlRequestDoc is null || xmlRequestDoc.Root is null)
        {
            return new(HttpStatusCode.BadRequest);
        }
        var isInfinite = GetIsInfiniteSync(xmlRequestDoc.Root);
        var pageSize = GetLimitResultset(xmlRequestDoc.Root);
        var requestToken = GetSyncToken(xmlRequestDoc.Root);
        Guid rangeStart = Guid.Empty;
        if (requestToken is not null)
        {
            var vt = await ItemRepository!.VerifySyncToken(baseResource.Current!.Id, requestToken.Value, httpContext.RequestAborted);
            if (vt is null)
            {
                if (!env.HasFeatures(CalendareFeatures.SyncCollectionSuppressTokenGone, httpContext))
                {
                    return new(HttpStatusCode.Gone, "Sync token is invalid");
                }
            }
            else
            {
                rangeStart = vt.Value;
            }
        }
        var journalItems = await ItemRepository!.ListCollectionObjectsAsync(baseResource.Current!.Id, rangeStart, httpContext.RequestAborted);
        var newToken = await ItemRepository!.GetCurrentSyncToken(baseResource.Current!.Id, httpContext.RequestAborted);
        var (xmlDoc, xmlMultistatus) = HandlerExtensions.CreateMultistatusDocument();
        foreach (var ji in journalItems)
        {
            if (ji.CollectionObject is not null)
            {
                var ci = ji.CollectionObject;
                DavResource? resource = null;
                if (ci.CalendarItem is not null)
                {
                    resource = baseResource.Graft(ci.CalendarItem);
                }
                if (ci.AddressItem is not null)
                {
                    resource = baseResource.Graft(ci.AddressItem);
                }
                if (resource is not null)
                {
                    var xmlResponse = await HandlerExtensions.PropertyResponse(propertyRegistry, resource, null, properties, httpContext);
                    xmlMultistatus.Add(xmlResponse);
                }
            }
            else
            {
                var xmlResponse = new XElement(XmlNs.Dav + "response",
                    new XElement(XmlNs.Dav + "href", $"{PathBase}{ji.Uri}"),
                    new XElement(XmlNs.Dav + "status", "HTTP/1.1 404 Not Found")
                );
                xmlMultistatus.Add(xmlResponse);
            }
        }
        var xmlSyncToken = new XElement(XmlNs.Dav + "sync-token", newToken.Uri);
        xmlMultistatus.Add(xmlSyncToken);
        return new(xmlDoc);
    }

    /// <summary>
    /// https://datatracker.ietf.org/doc/html/rfc6578#section-6.3
    /// </summary>
    /// <param name="xml"></param>
    /// <returns></returns>
    private static bool GetIsInfiniteSync(XElement xml)
    {
        var level = xml.Element(XmlNs.Dav + "sync-level");
        if (level is not null && !level.IsEmpty)
        {
            return string.Equals(level.Value, "infinite", StringComparison.Ordinal);
        }
        return false;
    }

    /// <summary>
    /// https://datatracker.ietf.org/doc/html/rfc6578#section-6.2
    /// </summary>
    /// <param name="xml"></param>
    /// <returns></returns>
    private Guid? GetSyncToken(XElement xml)
    {
        var token = xml.Element(XmlNs.Dav + "sync-token");
        if (token is null)
        {
            return null;
        }
        if (string.IsNullOrEmpty(token.Value))
        {
            return Guid.Empty;
        }
        return SyncToken.ParseUri(token.Value);
    }

    /// <summary>
    /// https://datatracker.ietf.org/doc/html/rfc5323#section-5.17
    /// </summary>
    /// <param name="xml"></param>
    /// <returns></returns>
    private static int? GetLimitResultset(XElement xml)
    {
        var limit = xml.Element(XmlNs.Dav + "limit");
        if (limit is not null && !limit.IsEmpty)
        {
            var nresults = xml.Element(XmlNs.Dav + "nresults");
            if (nresults is not null && !nresults.IsEmpty)
            {
                return Convert.ToInt32(nresults.Value, CultureInfo.InvariantCulture);
            }
        }
        return null;
    }
}
