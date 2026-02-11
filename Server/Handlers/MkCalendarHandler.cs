using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Xml.Linq;
using Calendare.Data.Models;
using Calendare.Server.Constants;
using Calendare.Server.Models;
using Calendare.Server.Recorder;
using Calendare.Server.Repository;
using Calendare.Server.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Calendare.Server.Handlers;

/// <summary>
/// Implementation of the MKCALENDAR method.
/// </summary>
/// <remarks>
/// For MKCALENDAR see https://datatracker.ietf.org/doc/html/rfc4791#section-5.3.1
/// and for MKCOL see https://datatracker.ietf.org/doc/html/rfc5689#section-4.1
/// </see>.
/// </remarks>
public class MkCalendarHandler : HandlerBase, IMethodHandler
{
    private readonly ResourceRepository ResourceRepository;
    private readonly CollectionRepository CollectionRepository;

    public MkCalendarHandler(DavEnvironmentRepository env, ResourceRepository resourceRepository, CollectionRepository collectionRepository, RecorderSession recorder) : base(env, recorder)
    {
        ResourceRepository = resourceRepository;
        CollectionRepository = collectionRepository;
    }

    /// <summary>
    /// Handle a MKCALENDAR or MKCOL request.
    /// </summary>
    /// <param name="httpContext">
    /// The HTTP context of the request.
    /// </param>
    public async Task HandleRequestAsync(HttpContext httpContext, DavResource resource)
    {
        var request = httpContext.Request;
        var response = httpContext.Response;
        var isMkCalendar = string.Equals(request.Method, "MKCALENDAR", StringComparison.Ordinal);
        response.Headers.CacheControl = "no-cache";
        if (!resource.Privileges.HasAnyOf(PrivilegeMask.Bind))
        {
            //  (DAV:needs-privilege): The DAV:bind privilege MUST be granted to the current user on the parent collection of the Request-URI.
            await WriteErrorNeedPrivilegeAsync(httpContext, resource.DavName, PrivilegeMask.Bind);
            return;
        }
        // https://datatracker.ietf.org/doc/html/rfc4918#section-9.3.1
        // MKCOL can only be executed on an unmapped URL.
        if (resource.Exists && resource.ResourceType != DavResourceType.Principal)
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.MethodNotAllowed, "A collection already exists at that location.");
            return;
        }

        string parentCollectionPath = resource.Uri.ParentCollectionPath!;
        string collectionPath = resource.Uri.Path!;
        string? displayName = resource.Uri.ItemName;

        var (xmlRequestDoc, xmlSuccess) = await request.BodyAsXmlAsync(httpContext.RequestAborted);
        if (xmlSuccess == false)
        {
            SetContentLocation(response, resource.Uri.Path);
            await WriteErrorXmlAsync(httpContext, HttpStatusCode.BadRequest, XmlNs.Dav + "invalid-xml");
            return;
        }
        List<DavPropertyStatic> properties = [];
        if (xmlRequestDoc is not null)
        {
            Recorder.SetRequestBody(xmlRequestDoc);
            if (xmlRequestDoc?.Root is null || (
                   xmlRequestDoc.Root.Name != XmlNs.Caldav + "mkcalendar"
                && xmlRequestDoc.Root.Name != XmlNs.Dav + "mkcol"
                ))
            {
                // https://datatracker.ietf.org/doc/html/rfc4791#section-5.3.1.1
                await WriteStatusAsync(httpContext, HttpStatusCode.UnsupportedMediaType);
                return;
            }
            properties = xmlRequestDoc.GetPropertiesStatic();

            var propDisplayName = properties.FirstOrDefault(x => x.Name == XmlNs.Dav + "displayname");
            if (propDisplayName is not null && propDisplayName.Value is not null)
            {
                displayName = propDisplayName.Value.Value;
            }
            if (resource.ResourceType == DavResourceType.Principal)
            {
                if (!string.IsNullOrEmpty(displayName))
                {
                    collectionPath += $"{displayName}/";
                    parentCollectionPath = resource.Uri.Path!;
                }
                else
                {
                    SetContentLocation(response, resource.Uri.Path);
                    await WriteErrorXmlAsync(httpContext, HttpStatusCode.BadRequest, XmlNs.Dav + "invalid-xml");
                    return;
                }
            }
        }
        if (!(resource.ParentResourceType == Models.DavResourceType.Principal || resource.ParentResourceType == Models.DavResourceType.Container))
        {
            if (resource.ParentResourceType != DavResourceType.Root || (resource.ParentResourceType == DavResourceType.Root && resource.Uri.Collection is not null && resource.Uri.Collection?.Count > 1))
            {

                // https://datatracker.ietf.org/doc/html/rfc4918#section-9.3.1
                // The collection must always be created inside another collection
                await WriteStatusAsync(httpContext, HttpStatusCode.Forbidden);
                return;
            }
        }
        var collection = new Collection
        {
            ParentContainerUri = parentCollectionPath,
            ParentId = resource.Parent?.Id ?? resource.Owner.Id,
            Uri = collectionPath,
            DisplayName = displayName,
            CollectionType = string.Equals(request.Method, "MKCALENDAR", StringComparison.Ordinal) ? CollectionType.Calendar : CollectionType.Collection,
            Etag = $"{resource.Owner!.Id}{resource.Uri.Path}".PrettyMD5Hash(),
            OwnerId = resource.Owner.UserId,
        };
        var newResource = resource.Graft(collection);
        var propertyRegistry = httpContext.RequestServices.GetRequiredService<DavPropertyRepository>();
        var status = await PropPatchHandler.UpdateProperties(collection, propertyRegistry, newResource, properties, httpContext);
        if (status.Failure == false)
        {
            try
            {
                var resultCollection = await CollectionRepository.CreateAsync(collection, httpContext.RequestAborted);
                if (resultCollection is null)
                {
                    response.StatusCode = (int)HttpStatusCode.Forbidden;
                    return;
                }
                SetLocation(response, resultCollection.Uri);
                response.StatusCode = (int)HttpStatusCode.Created;
                return;
            }
            catch (DbUpdateException e)
            {
                Log.Error("Failed to create collection {collection}: {error}", resource.Uri.Path, e.InnerException?.Message ?? e.Message);
                await WriteStatusAsync(httpContext, HttpStatusCode.Forbidden);
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to create collection {collection}", resource.Uri.Path);
                await WriteStatusAsync(httpContext, HttpStatusCode.Forbidden);
            }
        }
        else
        {
            var xmlMakeResponse = new XElement(XmlNs.Dav + $"{(isMkCalendar ? "mkcalendar" : "mkcol")}-response");
            var xmlDoc = xmlMakeResponse.CreateDocument();
            PropPatchHandler.PropStatResponse(xmlMakeResponse, status);
            await response.BodyXmlAsync(xmlDoc, HttpStatusCode.Conflict, httpContext.RequestAborted);
            Recorder.SetResponseBody(xmlDoc);
        }
    }

}
