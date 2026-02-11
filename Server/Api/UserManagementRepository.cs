using System.Collections.Generic;
using Calendare.Data.Models;
using Calendare.Server.Constants;
using Calendare.Server.Repository;

namespace Calendare.Server.Api;

public class UserManagementRepository
{
    private readonly StaticDataRepository StaticData;

    public UserManagementRepository(StaticDataRepository staticData)
    {
        StaticData = staticData;
    }

    public void CreateDefaultCollections(Usr user, PrincipalType principalType, string timezone, string? color, string? displayName, string? description, List<UserRegisterCollections> skipCollections)
    {
        var principal = new Collection
        {
            CollectionType = CollectionType.Principal,
            PrincipalTypeId = principalType.Id,
            ParentContainerUri = "/",
            Uri = $"/{user.Username}/",
            DisplayName = displayName,
            Timezone = timezone,
            Color = color,
            GlobalPermitSelf = PrivilegeMask.ReadCurrentUserPrivilegeSet | PrivilegeMask.ReadFreeBusy,
            Description = description,
        };
        user.Collections.Add(principal);
        if (!(skipCollections.Contains(UserRegisterCollections.Calendar) || skipCollections.Contains(UserRegisterCollections.Default)))
        {
            var colCalendar = new Collection
            {
                Parent = principal,
                Uri = $"/{user.Username}/{CollectionUris.DefaultCalendar}/",
                CollectionType = CollectionType.Calendar,
                CollectionSubType = CollectionSubType.Default,
                ParentContainerUri = $"/{user.Username}/",
                DisplayName = $"{displayName} calendar",
                ScheduleTransparency = ScheduleTransparency.Opaque,
                OwnerProhibit = PrivilegeMask.Bind | PrivilegeMask.Unbind,
                AuthorizedProhibit = PrivilegeMask.Bind | PrivilegeMask.Unbind,
                GlobalPermitSelf = PrivilegeMask.ReadFreeBusy,
                Timezone = timezone,
                Color = color,
            };
            user.Collections.Add(colCalendar);
        }
        if (!(skipCollections.Contains(UserRegisterCollections.Scheduling) || skipCollections.Contains(UserRegisterCollections.Default)))
        {
            var colCalendarOutbox = new Collection
            {
                Parent = principal,
                Uri = $"/{user.Username}/{CollectionUris.CalendarOutbox}/",
                CollectionType = CollectionType.Calendar,
                CollectionSubType = CollectionSubType.SchedulingOutbox,
                // TODO: Set DefaultPrivileges [Privileges on Scheduling Outbox Collections](https://datatracker.ietf.org/doc/html/rfc6638#section-6.2)
                //          These privileges determine which calendar users are allowed to send scheduling messages
                //          on behalf of the calendar user who "owns" the scheduling Outbox collection.
                GlobalPermitSelf = PrivilegeMask.ReadFreeBusy | PrivilegeMask.ScheduleSend,
                OwnerProhibit = PrivilegeMask.Bind | PrivilegeMask.Unbind,
                AuthorizedProhibit = PrivilegeMask.Bind | PrivilegeMask.Unbind,
                ParentContainerUri = $"/{user.Username}/",
                DisplayName = $"{displayName} Outbox",
                Timezone = timezone,
                ScheduleTransparency = ScheduleTransparency.Opaque,
            };
            user.Collections.Add(colCalendarOutbox);
            var colCalendarInbox = new Collection
            {
                Parent = principal,
                Uri = $"/{user.Username}/{CollectionUris.CalendarInbox}/",
                CollectionType = CollectionType.Calendar,
                CollectionSubType = CollectionSubType.SchedulingInbox,
                // TODO: Set DefaultPrivileges [Privileges on Scheduling Inbox Collections](https://datatracker.ietf.org/doc/html/rfc6638#section-6.1)
                //          These privileges determine whether delivery of scheduling messages from a calendar user
                //          is allowed by the calendar user who "owns" the scheduling Inbox collection.
                GlobalPermitSelf = PrivilegeMask.ReadFreeBusy | PrivilegeMask.ScheduleDeliver,
                OwnerProhibit = PrivilegeMask.Bind | PrivilegeMask.Unbind,
                AuthorizedProhibit = PrivilegeMask.Bind | PrivilegeMask.Unbind,
                ParentContainerUri = $"/{user.Username}/",
                DisplayName = $"{displayName} Inbox",
                Timezone = timezone,
                ScheduleTransparency = ScheduleTransparency.Opaque,
            };
            user.Collections.Add(colCalendarInbox);
        }
        if (!(skipCollections.Contains(UserRegisterCollections.WebPush) || skipCollections.Contains(UserRegisterCollections.Default)))
        {
            var colSubscriptions = new Collection
            {
                Parent = principal,
                Uri = $"/{user.Username}/{CollectionUris.PushSubscription}/",
                CollectionType = CollectionType.Collection,
                CollectionSubType = CollectionSubType.WebPushSubscription,
                //GlobalPermitSelf = PrivilegeMask.ReadFreeBusy | PrivilegeMask.ScheduleDeliver,
                OwnerProhibit = PrivilegeMask.Bind | PrivilegeMask.Unbind | PrivilegeMask.WriteAcl | PrivilegeMask.Share,
                AuthorizedProhibit = PrivilegeMask.Bind | PrivilegeMask.Unbind | PrivilegeMask.WriteAcl | PrivilegeMask.Share,
                ParentContainerUri = $"/{user.Username}/",
                DisplayName = $"{displayName} Push Subscriptions",
            };
            user.Collections.Add(colSubscriptions);
        }
        if (!(skipCollections.Contains(UserRegisterCollections.Address) || skipCollections.Contains(UserRegisterCollections.Default)))
        {
            var colAddressbook = new Collection
            {
                Parent = principal,
                Uri = $"/{user.Username}/{CollectionUris.DefaultAddressbook}/",
                CollectionType = CollectionType.Addressbook,
                OwnerProhibit = PrivilegeMask.Bind | PrivilegeMask.Unbind,
                AuthorizedProhibit = PrivilegeMask.Bind | PrivilegeMask.Unbind | PrivilegeMask.Read | PrivilegeMask.ReadAcl | PrivilegeMask.Write,
                ParentContainerUri = $"/{user.Username}/",
                DisplayName = $"{displayName} addressbook",
                Color = color,
            };
            user.Collections.Add(colAddressbook);
        }
        if (!(skipCollections.Contains(UserRegisterCollections.Proxy) || skipCollections.Contains(UserRegisterCollections.Default)))
        {
            var principalGroupType = StaticData.PrincipalTypeList[PrincipalTypes.Group];
            var colPrincipalProxyWrite = new Collection
            {
                Parent = principal,
                Uri = $"/{user.Username}/{CollectionUris.CalendarProxyWrite}/",
                CollectionType = CollectionType.Principal,
                CollectionSubType = CollectionSubType.CalendarProxyWrite,
                OwnerProhibit = PrivilegeMask.Bind | PrivilegeMask.Unbind,
                AuthorizedProhibit = PrivilegeMask.Bind | PrivilegeMask.Unbind,
                GlobalPermitSelf = PrivilegeMask.ReadCurrentUserPrivilegeSet,
                PrincipalTypeId = principalGroupType.Id,
                ParentContainerUri = $"/{user.Username}/",
                DisplayName = $"{displayName} [Read/Write]",
            };
            user.Collections.Add(colPrincipalProxyWrite);
            var colPrincipalProxyRead = new Collection
            {
                Parent = principal,
                Uri = $"/{user.Username}/{CollectionUris.CalendarProxyRead}/",
                CollectionType = CollectionType.Principal,
                CollectionSubType = CollectionSubType.CalendarProxyRead,
                OwnerProhibit = PrivilegeMask.Bind | PrivilegeMask.Unbind,
                AuthorizedProhibit = PrivilegeMask.Bind | PrivilegeMask.Unbind | PrivilegeMask.Write,
                GlobalPermitSelf = PrivilegeMask.ReadCurrentUserPrivilegeSet,
                PrincipalTypeId = principalGroupType.Id,
                ParentContainerUri = $"/{user.Username}/",
                DisplayName = $"{displayName} [Read only]",
            };
            user.Collections.Add(colPrincipalProxyRead);
        }
    }
}
