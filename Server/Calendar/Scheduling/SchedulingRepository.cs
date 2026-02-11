using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Calendare.Server.Api.Models;
using Calendare.Server.Handlers;
using Calendare.Server.Models;
using Calendare.Server.Repository;
using Calendare.VSyntaxReader.Components;
using Microsoft.AspNetCore.Http;

namespace Calendare.Server.Calendar.Scheduling;

public partial class SchedulingRepository
{
    private readonly DavEnvironmentRepository Env;
    private readonly UserRepository UserRepository;
    private readonly ResourceRepository ResourceRepository;
    private readonly ItemRepository ItemRepository;
    public Dictionary<string, Principal> Addressbook { get; } = [];

    public SchedulingRepository(DavEnvironmentRepository env, UserRepository userRepository, ResourceRepository resourceRepository, ItemRepository itemRepository)
    {
        Env = env;
        ItemRepository = itemRepository;
        UserRepository = userRepository;
        ResourceRepository = resourceRepository;
    }

    public async Task Put(HandlerBase handler, HttpContext httpContext, DbOperationCode opCode, DavResource resource, CollectionObject collectionObject, VCalendarUnique vCalendarUnique, VCalendarUnique? vPreviousCalendar)
    {
        SchedulingRequest? schedulingRequest;
        if (resource.Parent?.CollectionSubType == CollectionSubType.SchedulingInbox)
        {
            schedulingRequest = await ProcessInbox(httpContext, resource, collectionObject, vCalendarUnique.Calendar);
        }
        else
        {
            schedulingRequest = await Schedule(httpContext, resource, opCode, collectionObject, vCalendarUnique, vPreviousCalendar);
        }
        if (schedulingRequest is null || schedulingRequest.Origin.CalendarItem is null)
        {
            await handler.WriteStatusAsync(httpContext, HttpStatusCode.InternalServerError);
            return;
        }
        if (schedulingRequest.Origin.CalendarItem.IsScheduling == false)
        {
            schedulingRequest.Origin.ScheduleTag = schedulingRequest.Origin.Etag;
        }
        await ItemRepository.AmendAsync(schedulingRequest, httpContext.RequestAborted);
        if (schedulingRequest.Origin.CalendarItem.IsScheduling == true)
        {
            handler.SetScheduleHeader(httpContext.Response, collectionObject.ScheduleTag);
        }
        handler.SetEtagHeader(httpContext.Response, collectionObject.Etag);
        await handler.WriteStatusAsync(httpContext, opCode == DbOperationCode.Update ? HttpStatusCode.NoContent : HttpStatusCode.Created);
        return;
    }

    public async Task<SchedulingRequest> Schedule(HttpContext httpContext, DavResource resource, DbOperationCode operationCode, CollectionObject collectionObject, VCalendarUnique currentCalendar, VCalendarUnique? beforeCalendar)
    {
        if (!Env.HasFeatures(CalendareFeatures.AutoScheduling, httpContext) || currentCalendar.Organizer is null || currentCalendar.HasAttendees == false)
        {
            if (collectionObject.CalendarItem is not null)
            {
                collectionObject.CalendarItem.IsScheduling = false;
            }
            return CreateSchedulingRequest(operationCode, collectionObject, []);
        }
        var scheduleItems = await OrganizerScheduling(httpContext, resource, operationCode, collectionObject, currentCalendar, beforeCalendar);
        if (scheduleItems is null)
        {
            var attendeeRequest = await AttendeeScheduling(httpContext, resource, operationCode, collectionObject, currentCalendar, beforeCalendar);
            if (attendeeRequest is not null)
            {
                scheduleItems ??= [];
                scheduleItems.Add(attendeeRequest);
            }
        }
        scheduleItems?.AddRange(await ApplyInboxMsg(httpContext, scheduleItems));
        var schedulingRequest = CreateSchedulingRequest(operationCode, collectionObject, scheduleItems);
        return schedulingRequest;
    }

    public async Task<SchedulingRequest> ProcessInbox(HttpContext httpContext, DavResource resource, CollectionObject collectionObject, VCalendar currentCalendar)
    {
        List<SchedulingItem> scheduleItems = [];
        SchedulingItem trigger = new SchedulingItem { Resource = resource, Calendar = currentCalendar, Email = "*" };
        scheduleItems.Add(trigger);
        scheduleItems.AddRange(await ApplyInboxMsg(httpContext, [trigger]));
        return CreateSchedulingRequest(DbOperationCode.Insert, collectionObject, scheduleItems);
    }

    // public bool IsSchedulingRequired(DbOperationCode opCode, ObjectCalendar objectCalendar)
    // {
    //     if (!IsSchedulingEnabled)
    //     {
    //         return false;
    //     }
    //     var cntAttendees = objectCalendar.Attendees?.Count ?? 0;
    //     return cntAttendees != 0;
    // }

    private static SchedulingRequest CreateSchedulingRequest(DbOperationCode opCode, CollectionObject collectionObject, List<SchedulingItem>? schedulingItems)
    {
        var schedulingRequest = new SchedulingRequest { OpCode = opCode, Origin = collectionObject, };
        foreach (var item in schedulingItems ?? [])
        {
            if (item.Resource is not null)
            {
                if (item.IsDelete == false)
                {
                    var inboxCollection = item.Calendar.CreateCollectionObject(item.Resource);
                    // var existing = inboxCollection?.Id is not null && inboxCollection.Id != 0 && schedulingRequest.SchedulingObjects.FirstOrDefault(s => s.Id == inboxCollection.Id) is not null;
                    // if (!existing && inboxCollection is not null) schedulingRequest.SchedulingObjects.Add(inboxCollection);
                    if (inboxCollection is not null) schedulingRequest.SchedulingObjects.Add(inboxCollection);
                }
                else
                {
                    if (item.Resource.Object is not null) schedulingRequest.TrashcanObjects.Add(item.Resource.Object);
                }
            }
            else
            {
                VCalendarUnique unique = new(item.Calendar);
                if (unique is not null && unique.Uid is not null && unique.IsValid)
                {
                    schedulingRequest.ExternalObjects.Add(new SchedulingEMailItem
                    {
                        Uid = unique.Uid,
                        EmailFrom = item.EmailFrom ?? unique.Organizer!.Value,
                        EmailTo = item.Email,
                        Body = item.Calendar.Serialize(),
                        Sequence = 222,   // TODO: introduce sequence on VCalendarUnique??
                    });
                }
            }
        }
        return schedulingRequest;
    }
}
