using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Xml.Linq;
using Calendare.Server.Constants;
using Calendare.Server.Handlers;
using Calendare.Server.Models;
using Calendare.Server.Repository;
using Calendare.Server.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Calendare.Server.Reports;

/// <summary>
/// https://datatracker.ietf.org/doc/html/rfc3744#section-9.4
///
/// TODO: SEARCHING IS NOT YET FULLY IMPLEMENTED, check also the actual usage of this report ...
///
/// TODO: This is not restricted, enhance with some kind of privilege to know which principals
/// are browseable at all.
///
/// </summary>
public class PrincipalPropertySearchReport : ReportBase, IReport
{
    public async Task<ReportResponse> Report(XDocument xmlRequestDoc, DavResource resource, List<DavPropertyRef> properties, HttpContext httpContext)
    {
        var depth = httpContext.Request.GetDepth(0);
        if (xmlRequestDoc is null || xmlRequestDoc.Root is null || !resource.Exists || depth != 0)
        {
            return new(HttpStatusCode.BadRequest);
        }
        // TODO: Implement restrictions ...
        var env = httpContext.RequestServices.GetRequiredService<DavEnvironmentRepository>();
        var PathBase = env.PathBase;
        var resourceRepository = httpContext.RequestServices.GetRequiredService<ResourceRepository>();
        var xmlType = xmlRequestDoc.Root.Attribute("type");
        var xmlTest = xmlRequestDoc.Root.Attribute("test");
        var logicalAnd = xmlTest is not null && "allof".Equals(xmlTest.Value, System.StringComparison.InvariantCultureIgnoreCase);
        var searchTerms = ParsePropertySearch(xmlRequestDoc.Root);
        var query = new PrincipalListQuery
        {
            CurrentUser = resource.CurrentUser,
            Unrestricted = true,
        };
        if (xmlType is not null && !string.IsNullOrEmpty(xmlType.Value))
        {
            var userRepository = httpContext.RequestServices.GetRequiredService<UserRepository>();
            var principalType = await userRepository.GetPrincipalTypeAsync(xmlType.Value, httpContext.RequestAborted);
            if (principalType is not null)
            {
                query.PrincipalTypes = [principalType];
            }
        }
        var principals = await resourceRepository.ListPrincipalsAsResourceAsync(query, httpContext.RequestAborted);
        var propertyRegistry = httpContext.RequestServices.GetRequiredService<DavPropertyRepository>();
        var (xmlDoc, xmlMultistatus) = HandlerExtensions.CreateMultistatusDocument();
        foreach (var principal in principals)
        {
            if (searchTerms is not null && searchTerms.Count > 0)
            {
                bool anyMatch = logicalAnd;
                foreach (var st in searchTerms)
                {
                    bool matchSuccess;
                    var prop = propertyRegistry.Property(st.Name, principal.ResourceType);
                    if (prop is not null && prop.Matches is not null)
                    {
                        matchSuccess = prop.Matches(principal, st.MatchValue);
                    }
                    else
                    {
                        matchSuccess = false;   // missing property equals failure
                    }
                    if (logicalAnd)
                    {
                        if (!matchSuccess)
                        {
                            anyMatch = false;
                            break;
                        }
                    }
                    else
                    {
                        if (matchSuccess)
                        {
                            anyMatch = true;
                            break;
                        }
                    }
                }
                if (anyMatch == false)
                {
                    continue;
                }
            }
            var xmlResponse = await HandlerExtensions.PropertyResponse(propertyRegistry, principal, null, properties, httpContext);
            xmlMultistatus.Add(xmlResponse);
        }
        return new(xmlDoc);
    }

    private static List<SearchProperty>? ParsePropertySearch(XElement xml)
    {
        var result = new List<SearchProperty>();
        var xmlPropertySearchList = xml.Elements(XmlNs.Dav + "property-search");
        foreach (var xmlPropertySearch in xmlPropertySearchList)
        {
            var xmlProp = xmlPropertySearch.Element(XmlNs.Dav + "prop");
            if (xmlProp is null || xmlProp.FirstNode is null)
            {
                Log.Warning("property-search without prop or empty prop element");
                return null;
            }
            var st = new SearchProperty { Name = xmlProp.Elements().First().Name, };
            var xmlMatch = xmlPropertySearch.Element(XmlNs.Dav + "match");
            if (xmlMatch is not null)
            {
                st.MatchValue = xmlMatch.Value;
            }
            result.Add(st);
        }
        return result;
    }
}

public class SearchProperty
{
    public required XName Name { get; set; }
    public string MatchType { get; set; } = "Contains";
    public string? MatchValue { get; set; }
}
