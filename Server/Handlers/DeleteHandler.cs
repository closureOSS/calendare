using System;
using System.Net;
using System.Threading.Tasks;
using Calendare.Server.Calendar;
using Calendare.Server.Calendar.Scheduling;
using Calendare.Server.Constants;
using Calendare.Server.Models;
using Calendare.Server.Recorder;
using Calendare.Server.Repository;
using Calendare.Server.Utils;
using Calendare.VSyntaxReader.Components;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace Calendare.Server.Handlers;

/// <summary>
/// Implementation of the DELETE method.
/// </summary>
/// <remarks>
/// The specification of the DELETE method can be found in the
/// <see href="https://datatracker.ietf.org/doc/html/rfc4918#section-9.6">
/// CalDav specification
/// </see>.
/// </remarks>
public class DeleteHandler : HandlerBase, IMethodHandler
{
    private readonly CollectionRepository CollectionRepository;
    private readonly ItemRepository ItemRepository;
    private readonly SchedulingRepository SchedulingRepository;
    private readonly PushSubscriptionRepository PushSubscriptionRepository;
    private readonly ICalendarBuilder CalendarBuilder;

    public DeleteHandler(DavEnvironmentRepository env, CollectionRepository collectionRepository, ItemRepository itemRepository, SchedulingRepository schedulingRepository, PushSubscriptionRepository pushSubscriptionRepository, ICalendarBuilder calendarBuilder, RecorderSession recorder) : base(env, recorder)
    {
        CollectionRepository = collectionRepository;
        ItemRepository = itemRepository;
        SchedulingRepository = schedulingRepository;
        PushSubscriptionRepository = pushSubscriptionRepository;
        CalendarBuilder = calendarBuilder;
    }

    public async Task HandleRequestAsync(HttpContext httpContext, DavResource resource)
    {
        var request = httpContext.Request;
        var response = httpContext.Response;
        if (resource.ResourceType == DavResourceType.Unknown || resource.Exists == false)
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.NotFound);
            return;
        }
        var ifmatch = request.GetIfMatch();
        var ifmatchSchedule = request.GetIfScheduleTagMatch();
        switch (resource.ResourceType)
        {
            case DavResourceType.AddressbookItem:
                if (ifmatch is not null && !string.Equals(ifmatch, resource.Object?.Etag, StringComparison.Ordinal))
                {
                    await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, XmlNs.Dav + "if-match", $"Existing resource Etag of \"{ifmatch}\" does not match \"{resource.Object?.Etag}\"");
                    return;
                }
                await ItemRepository.DeleteAsync(resource.Object!.Uri, httpContext.RequestAborted);
                await WriteStatusAsync(httpContext, HttpStatusCode.NoContent);
                break;

            case DavResourceType.CalendarItem:
                if (resource.Object is null || resource.Parent is null || resource.Object.CalendarItem is null)
                {
                    throw new System.Exception("Object and parent collection are expected due to previous checks to be not null");
                }
                if (ifmatch is not null && !string.Equals(ifmatch, resource.Object?.Etag, StringComparison.Ordinal))
                {
                    await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, XmlNs.Dav + "if-match", $"Existing resource Etag of \"{ifmatch}\" does not match \"{resource.Object?.Etag}\"");
                    return;
                }
                if (ifmatchSchedule is not null && !string.Equals(ifmatchSchedule, resource.Object?.ScheduleTag, StringComparison.Ordinal))
                {
                    await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, XmlNs.Dav + "if-match", $"Existing resource schedule tag of \"{ifmatchSchedule}\" does not match \"{resource.Object?.ScheduleTag}\"");
                    return;
                }

                if (resource.Parent.CollectionSubType == Calendare.Data.Models.CollectionSubType.SchedulingInbox)
                {
                    await ItemRepository.DeleteAsync(resource.Object!.Uri, httpContext.RequestAborted);
                }
                else
                {
                    var parseResult = CalendarBuilder.Parser.TryParse(resource.Object!.RawData, out var vCalendar, $"{httpContext.Request.GetFullPath()}");
                    if (!parseResult || vCalendar is null)
                    {
                        // TODO: Just delete and ignore error?
                        Log.Error("Failed to parse {errMsg}", parseResult.ErrorMessage);
                        await WriteStatusAsync(httpContext, HttpStatusCode.UnsupportedMediaType);
                        return;
                    }
                    var vCalendarUnique = new VCalendarUnique(vCalendar);
                    if (!vCalendarUnique.IsValid)
                    {
                        // TODO: Just delete and ignore error?
                        Log.Error("Calendar contains multiple unrelated components");
                        await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, XmlNs.Caldav + "valid-calendar-object-resource", "Calendar contains multiple unrelated components");
                        return;
                    }
                    var schedulingRequest = await SchedulingRepository.Schedule(httpContext, resource, DbOperationCode.Delete, resource.Object, vCalendarUnique, null);
                    await ItemRepository.AmendAsync(schedulingRequest, httpContext.RequestAborted);
                }
                await WriteStatusAsync(httpContext, HttpStatusCode.NoContent);
                return;

            case DavResourceType.Container:
            case DavResourceType.Addressbook:
            case DavResourceType.Calendar:
                if (resource.Current is null)
                {
                    await WriteStatusAsync(httpContext, HttpStatusCode.NotFound);
                    // TODO: Check if this is trigged, or is it dead code?
                    throw new NotSupportedException($"Collection at {resource.Uri.Path} not set?");
                }
                await CollectionRepository.DeleteAsync(resource.Current.Id, httpContext.RequestAborted);
                await WriteStatusAsync(httpContext, HttpStatusCode.NoContent);
                return;

            case DavResourceType.WebSubscriptionItem:
                {
                    if (resource.Object is null || string.IsNullOrEmpty(resource.Object.Uid))
                    {
                        await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, XmlNs.Bitfire + "subscription-id", "Subscription Id mandatory");
                        return;
                    }
                    var subscription = await PushSubscriptionRepository.Delete(resource.CurrentUser.UserId, resource.Object.Uid, httpContext.RequestAborted);
                    if (subscription is null)
                    {
                        await WriteStatusAsync(httpContext, HttpStatusCode.NotFound);
                        return;
                    }
                    await WriteStatusAsync(httpContext, HttpStatusCode.NoContent);
                }
                break;

            default:
                await WriteStatusAsync(httpContext, HttpStatusCode.Forbidden);
                break;
        }
    }
}
