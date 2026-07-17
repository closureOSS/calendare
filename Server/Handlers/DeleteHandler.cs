using System;
using System.Net;
using System.Threading.Tasks;
using Calendare.Server.Calendar;
using Calendare.Server.Calendar.Scheduling;
using Calendare.Server.Constants;
using Calendare.Server.Models;
using Calendare.Server.Recorder;
using Calendare.Server.Repository;
using Calendare.Server.Storage;
using Calendare.Server.Utils;
using Calendare.VSyntaxReader.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly MoveCopyRepository MoveRepository;

    public DeleteHandler(DavEnvironmentRepository env, CollectionRepository collectionRepository,
        ItemRepository itemRepository, MoveCopyRepository moveRepository,
        SchedulingRepository schedulingRepository, PushSubscriptionRepository pushSubscriptionRepository,
        ICalendarBuilder calendarBuilder, RecorderSession recorder) : base(env, recorder)
    {
        CollectionRepository = collectionRepository;
        ItemRepository = itemRepository;
        SchedulingRepository = schedulingRepository;
        PushSubscriptionRepository = pushSubscriptionRepository;
        MoveRepository = moveRepository;
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
            case DavResourceType.BlobItem:
            case DavResourceType.AddressbookItem:
                if (ifmatch is not null && !string.Equals(ifmatch, resource.Object?.Etag, StringComparison.Ordinal))
                {
                    await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, Precondition.IfMatch, $"Existing resource Etag of \"{ifmatch}\" does not match \"{resource.Object?.Etag}\"");
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
                    await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, Precondition.IfMatch, $"Existing resource Etag of \"{ifmatch}\" does not match \"{resource.Object?.Etag}\"");
                    return;
                }
                if (ifmatchSchedule is not null && !string.Equals(ifmatchSchedule, resource.Object?.ScheduleTag, StringComparison.Ordinal))
                {
                    await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, Precondition.IfMatch, $"Existing resource schedule tag of \"{ifmatchSchedule}\" does not match \"{resource.Object?.ScheduleTag}\"");
                    return;
                }

                if (resource.Parent.CollectionSubType == Calendare.Data.Models.CollectionSubType.SchedulingInbox)
                {
                    await ItemRepository.DeleteAsync(resource.Object!.Uri, httpContext.RequestAborted);
                }
                else
                {
                    var parseResult = CalendarBuilder.Parser.TryParse(resource.Object!.RawData, out var vCalendar, $"{httpContext.Request.GetFullPath(PathBase)}");
                    if (!parseResult || vCalendar is null)
                    {
                        // TODO: Just delete and ignore error?
                        Log.Error("Parsing of request body text/calendar failed {errMsg}", parseResult.ErrorMessage);
                        switch (parseResult.ErrorCategory)
                        {
                            case VSyntaxReader.Properties.DeserializeErrorCategory.Syntax:
                                await WriteErrorXmlAsync(httpContext, HttpStatusCode.Conflict, Precondition.ValidCalendarData, parseResult.ErrorMessage);
                                break;

                            case VSyntaxReader.Properties.DeserializeErrorCategory.NoContent:
                            case VSyntaxReader.Properties.DeserializeErrorCategory.WrongFormat:
                                await WriteStatusAsync(httpContext, HttpStatusCode.UnsupportedMediaType);
                                break;
                        }
                        return;
                    }
                    var vCalendarUnique = new VCalendarUnique(vCalendar);
                    if (!vCalendarUnique.IsValid)
                    {
                        // TODO: Just delete and ignore error?
                        Log.Error("Calendar contains multiple unrelated components");
                        await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, Precondition.ValidCalendarObjectResource, "Calendar contains multiple unrelated components");
                        return;
                    }
                    var schedulingRequest = await SchedulingRepository.Schedule(httpContext, resource, DbOperationCode.Delete, resource.Object, vCalendarUnique, beforeCalendar: null);
                    await ItemRepository.AmendAsync(schedulingRequest, httpContext.RequestAborted);
                }
                await WriteStatusAsync(httpContext, HttpStatusCode.NoContent);
                return;

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

            case DavResourceType.Container:
                if (resource.Current is null)
                {
                    await WriteStatusAsync(httpContext, HttpStatusCode.NotFound);
                    // TODO: Check if this is trigged, or is it dead code?
                    throw new NotSupportedException($"Collection at {resource.Uri.Path} not set?");
                }
                var storage = httpContext.RequestServices.GetService<IDavStorage>();
                if (storage is not null)
                {
                    await MoveRepository.TrackBlobDeletionAsync(resource.Current, storage, httpContext.RequestAborted);
                }
                await CollectionRepository.DeleteAsync(resource.Current.Id, httpContext.RequestAborted);
                await WriteStatusAsync(httpContext, HttpStatusCode.NoContent);
                if (storage is not null)
                {
                    try
                    {
                        await storage.CommitImmediately(httpContext.RequestAborted);
                    }
                    catch
                    {
                        Log.Error("Storage cleanup failed");
                    }
                }
                return;

            case DavResourceType.WebSubscriptionItem:
                {
                    if (resource.Object is null || string.IsNullOrEmpty(resource.Object.Uid))
                    {
                        await WriteErrorXmlAsync(httpContext, HttpStatusCode.PreconditionFailed, Precondition.SubscriptionId, "Subscription Id mandatory");
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
