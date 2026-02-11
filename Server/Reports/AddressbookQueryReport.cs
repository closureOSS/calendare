using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Calendare.Server.Addressbook;
using Calendare.Server.Handlers;
using Calendare.Server.Models;
using Calendare.Server.Repository;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Calendare.Server.Reports;


/// <summary>
/// https://datatracker.ietf.org/doc/html/rfc6352#section-8.6
/// </summary>
public class AddressbookQueryReport : ReportBase, IReport
{
    public async Task<ReportResponse> Report(XDocument xmlRequestDoc, DavResource resource, List<DavPropertyRef> properties, HttpContext httpContext)
    {
        var ct = httpContext.RequestAborted;
        var filterEvaluator = new FilterEvaluator();
        filterEvaluator.Compile(AddressbookFilter.Parse(xmlRequestDoc?.Root));

        var itemRepository = httpContext.RequestServices.GetRequiredService<ItemRepository>();
        var collectionObjects = await itemRepository.ListCollectionObjectsAsync(resource.Current!, ct);
        var propertyRegistry = httpContext.RequestServices.GetRequiredService<DavPropertyRepository>();

        var matchedObjects = collectionObjects.Where(filterEvaluator.Matches).OrderBy(x => x.Uri, StringComparer.OrdinalIgnoreCase);

        var (xmlDoc, xmlMultistatus) = HandlerExtensions.CreateMultistatusDocument();
        foreach (var ci in matchedObjects)
        {
            if (ci.AddressItem is not null)
            {
                var addressItemResource = resource.Graft(ci.AddressItem) ?? throw new ArgumentOutOfRangeException(nameof(resource));
                var xmlResponse = await HandlerExtensions.PropertyResponse(propertyRegistry, addressItemResource, ci.Uri, properties, httpContext);
                xmlMultistatus.Add(xmlResponse);
            }
        }
        return new(xmlDoc);
    }
}
