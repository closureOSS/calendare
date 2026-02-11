using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Calendare.Data.Models;
using Calendare.Server.Calendar;
using Calendare.Server.Constants;
using Calendare.Server.Models;
using Calendare.Server.Repository;
using Calendare.VSyntaxReader.Components;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace Calendare.Server.Handlers;


public partial class GetHandler : HandlerBase, IMethodHandler
{
    private async Task GetCalendarItem(HttpContext httpContext, DavResource resource, bool isHeadRequest)
    {
        var response = httpContext.Response;
        var confidentialMode = resource.CurrentUser.Id != resource.Owner.Id;
        if (resource.Exists == true && resource.Object is not null && resource.Object.RawData is not null)
        {
            SetEtagHeader(response, resource.Object.Etag);
            response.ContentType = $"{MimeContentTypes.VCalendar}; component={resource.Object.VObjectType.ToLowerInvariant()}; {MimeContentTypes.Utf8}";
            response.StatusCode = (int)HttpStatusCode.OK;
            if (isHeadRequest == false)
            {
                if (confidentialMode)
                {
                    var vcalendarConfidential = CreateCombinedCalendar([resource.Object], confidentialMode, resource.CurrentUser.Id);
                    var serializedCalendar = vcalendarConfidential.Serialize();
                    response.ContentLength = Encoding.UTF8.GetByteCount(serializedCalendar);
                    await response.WriteAsync(serializedCalendar, httpContext.RequestAborted);
                    Recorder.SetResponseBody(serializedCalendar);
                }
                else
                {
                    response.ContentLength = Encoding.UTF8.GetByteCount(resource.Object.RawData);
                    await response.WriteAsync(resource.Object.RawData, httpContext.RequestAborted);
                    Recorder.SetResponseBody(resource.Object.RawData);
                }
            }
        }
        else
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.NotFound);
        }
    }

    private async Task GetCalendar(HttpContext httpContext, DavResource resource, bool isHeadRequest)
    {
        var response = httpContext.Response;
        var confidentialMode = resource.CurrentUser.Id != resource.Owner.Id;
        if (resource.Exists == true && resource.Current is not null)
        {
            var collectionObjects = await ItemRepository.ListCollectionObjectsAsync(resource.Current, httpContext.RequestAborted);
            var calendar = CreateCombinedCalendar(collectionObjects, confidentialMode, resource.CurrentUser.Id);
            if (resource.Current.DisplayName is not null)
            {
                calendar.CalendarName = resource.Current.DisplayName;
            }
            var serializedCalendar = calendar.Serialize();
            SetEtagHeader(response, resource.Object?.Etag);
            response.ContentType = $"{MimeContentTypes.VCalendar}; {MimeContentTypes.Utf8}";
            response.StatusCode = (int)HttpStatusCode.OK;
            if (isHeadRequest == false)
            {
                response.ContentLength = Encoding.UTF8.GetByteCount(serializedCalendar);
                await response.WriteAsync(serializedCalendar, httpContext.RequestAborted);
                Recorder.SetResponseBody(serializedCalendar);
            }
        }
        else
        {
            await WriteStatusAsync(httpContext, HttpStatusCode.BadRequest); // Or NotFound??
        }
    }


    private VCalendar CreateCombinedCalendar(List<CollectionObject> collectionObjects, bool makeConfidential, int currentUserId)
    {
        var calendar = CalendarBuilder.CreateCalendar();
        foreach (var co in collectionObjects)
        {
            if (co.IsPrivate && makeConfidential)
            {
                continue;
            }
            var parseResult = CalendarBuilder.Parser.TryParse(co.RawData, out var calendarItem, $"{co.Id}");
            if (parseResult && calendarItem is not null)
            {
                foreach (var child in calendarItem.Children)
                {
                    switch (child)
                    {
                        case VTimezone vtimezone:
                            var hasTZ = calendar.Children.Where(c => c is VTimezone).FirstOrDefault(t => t is VTimezone tz && string.Equals(tz.TzId, vtimezone.TzId, System.StringComparison.Ordinal));
                            if (hasTZ is null)
                            {
                                calendar.AddChild(vtimezone);
                            }
                            break;
                        case VEvent vEvent:
                            if (co.IsConfidential && makeConfidential && currentUserId != co.ActualUserId)
                            {
                                calendar.AddChild(vEvent.ToConfidential());
                            }
                            else
                            {
                                calendar.AddChild(vEvent);
                            }
                            break;
                        case VAvailability vAvailability:
                            calendar.AddChild(vAvailability);
                            break;
                        case VPoll vPoll:
                            calendar.AddChild(vPoll);
                            break;
                        case VTodo vTodo:
                        case VJournal vJournal:
                        default:
                            if (makeConfidential == false)
                            {
                                calendar.AddChild(child);
                            }
                            break;
                    }
                }
            }
            else
            {
                Log.Error("Failed to parse {id} {errMsg}", co.Id, parseResult.ErrorMessage);
            }

        }
        return calendar;
    }
}
