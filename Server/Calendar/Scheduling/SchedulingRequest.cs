using System.Collections.Generic;
using Calendare.Data.Models;
using Calendare.Server.Handlers;

namespace Calendare.Server.Calendar.Scheduling;


public class SchedulingRequest
{
    public required DbOperationCode OpCode { get; set; } = DbOperationCode.Skip;
    public required CollectionObject Origin { get; set; }
    public List<CollectionObject> SchedulingObjects { get; } = [];
    public List<CollectionObject> TrashcanObjects { get; } = [];

    public List<SchedulingEMailItem> ExternalObjects { get; } = [];
}
