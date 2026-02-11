using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Xml.Linq;
using Calendare.Data.Models;
using Calendare.Server.Calendar;
using Calendare.Server.Handlers;
using Calendare.Server.Models;
using Calendare.Server.Repository;
using Calendare.Server.Utils;
using Calendare.VSyntaxReader.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Calendare.Server.Reports;

/// <summary>
/// https://datatracker.ietf.org/doc/html/rfc4791#section-7.8
/// </summary>
public class CalendarQueryReport : ReportBase, IReport
{
    public async Task<ReportResponse> Report(XDocument xmlRequestDoc, DavResource resource, List<DavPropertyRef> properties, HttpContext httpContext)
    {
        var ct = httpContext.RequestAborted;
        var calendarBuilder = httpContext.RequestServices.GetRequiredService<ICalendarBuilder>();
        var filterEvaluator = new FilterEvaluator(calendarBuilder);
        filterEvaluator.Compile(CalendarFilter.Parse(xmlRequestDoc.Root));

        // TODO: Implement checks for a valid starting resource
        if (resource.ResourceType == DavResourceType.Addressbook || resource.ResourceType == DavResourceType.AddressbookItem || resource.ResourceType == DavResourceType.Unknown)
        {
            Log.Error("Current resource {uri}/{resourcetype} not supported", resource.DavName, resource.ResourceType);
            return new(HttpStatusCode.BadRequest);
        }
        var request = httpContext.Request;
        var depth = request.GetDepth(0);
        if ((depth == 0 && resource.Current is null) || resource.Exists == false)
        {
            Log.Error("Current resource {uri} is not a collection (with depth={depth}) or doesn't exist", resource.DavName, depth);
            return new(HttpStatusCode.Forbidden);
        }
        var env = httpContext.RequestServices.GetRequiredService<DavEnvironmentRepository>();
        var PathBase = env.PathBase;
        var ItemRepository = httpContext.RequestServices.GetRequiredService<ItemRepository>();

        List<CollectionObject> collectionObjects = [];
        if (depth == 0 && resource.Current is not null)
        {
            collectionObjects = await ItemRepository.ListCollectionObjectsAsync(resource.Current, ct);
        }
        else
        {
            List<Collection> collections = [];
            var collectionRepository = httpContext.RequestServices.GetRequiredService<CollectionRepository>();
            var ownedCollections = await collectionRepository.ListByOwnerUserIdAsync(resource.Owner.UserId, ct);
            int? parentCollectionId = resource.Current?.Id;
            // TODO: Trim list of owned collection to adapt to depth != infinite
            // This implementation supports only depth == 1 and depth == infinite
            foreach (var oc in ownedCollections)
            {
                if (depth != int.MaxValue && parentCollectionId is not null)
                {
                    if (oc.Id != parentCollectionId)
                    {
                        if (oc.ParentId != parentCollectionId)
                        {
                            continue;
                        }
                    }
                }
                if (oc.CollectionType == CollectionType.Calendar && (oc.CollectionSubType == CollectionSubType.Default || oc.CollectionSubType == CollectionSubType.SchedulingInbox))
                {
                    collections.Add(oc);
                }
            }
            var query = new CalendarObjectQuery
            {
                CurrentUser = resource.CurrentUser,
                CollectionIds = [.. collections.Select(c => c.Id)],
            };
            collectionObjects = await ItemRepository.ListCalendarObjectsAsync(query, ct);
        }


        var matchedObjects = collectionObjects.Where(filterEvaluator.Matches).OrderBy(x => x.Uri, StringComparer.InvariantCultureIgnoreCase);
        // TODO: Remove private items if not owner (see workaround below or integrate into query here)

        var propertyRegistry = httpContext.RequestServices.GetRequiredService<DavPropertyRepository>();
        var (xmlDoc, xmlMultistatus) = HandlerExtensions.CreateMultistatusDocument();
        foreach (var ci in matchedObjects)
        {
            if (ci.CalendarItem is not null)
            {
                var calendarItemResource = resource.Graft(ci.CalendarItem) ?? throw new ArgumentOutOfRangeException(nameof(resource));
                // Remove private items if not owner (workaround)
                if (ci.IsPrivate && !(ci.ActualUserId == resource.CurrentUser.Id || ci.OwnerId == resource.CurrentUser.Id))
                {
                    continue;
                }
                var xmlResponse = await HandlerExtensions.PropertyResponse(propertyRegistry, calendarItemResource, ci.Uri, properties, httpContext);
                xmlMultistatus.Add(xmlResponse);
            }
        }
        return new(xmlDoc);
    }
}
