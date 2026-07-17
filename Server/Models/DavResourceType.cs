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
    BlobItem,
}


public static class DavResourceTypeExtensions
{
    extension(Data.Models.CollectionType collectionType)
    {
        public DavResourceType ToResourceType()
        {
            return collectionType switch
            {
                Data.Models.CollectionType.Collection => DavResourceType.Container,
                Data.Models.CollectionType.Principal => DavResourceType.Principal,
                Data.Models.CollectionType.Calendar => DavResourceType.Calendar,
                Data.Models.CollectionType.Addressbook => DavResourceType.Addressbook,
                // Data.Models.CollectionType.SchedulingInbox => DavResourceType.Calendar,
                // Data.Models.CollectionType.SchedulingOutbox => DavResourceType.Calendar,
                _ => throw new Exception(),
            };
        }
    }
}
