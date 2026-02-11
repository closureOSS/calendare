using System;

namespace Calendare.Server.Models;

public enum DavResourceType
{
    Unknown,
    Root,
    User,   // ???
    Principal,
    Container,
    Calendar,
    Addressbook,
    CalendarItem,
    AddressbookItem,
    WebSubscriptionItem,
}


public static class DavResourceTypeExtensions
{
    public static DavResourceType ToResourceType(this Calendare.Data.Models.CollectionType collectionType)
    {
        return collectionType switch
        {
            Calendare.Data.Models.CollectionType.Collection => DavResourceType.Container,
            Calendare.Data.Models.CollectionType.Principal => DavResourceType.Principal,
            Calendare.Data.Models.CollectionType.Calendar => DavResourceType.Calendar,
            Calendare.Data.Models.CollectionType.Addressbook => DavResourceType.Addressbook,
            // Calendare.Data.Models.CollectionType.SchedulingInbox => DavResourceType.Calendar,
            // Calendare.Data.Models.CollectionType.SchedulingOutbox => DavResourceType.Calendar,
            _ => throw new Exception(),
        };
    }
}
