using System;
using System.Collections.Generic;
using Calendare.Data.Models;
using Calendare.Server.Constants;

namespace Calendare.Server.Models;

public static class PrivilegesDefinitions
{
    public static PrivilegeItem LoadTree()
    {
        var all = new PrivilegeItem { Id = XmlNs.Dav + "all", Privileges = PrivilegeMask.All, Description = "Full rights", Items = [], };
        all.Items.Add(new() { Id = XmlNs.Dav + "read", Privileges = PrivilegeMask.Read, Description = "Read the content of a resource or collection", });   // emclient->translated

        var write = new PrivilegeItem { Id = XmlNs.Dav + "write", Privileges = PrivilegeMask.Write, Description = "Write", Items = [], }; // emclient->translated
        write.Items.Add(new() { Id = XmlNs.Dav + "bind", Privileges = PrivilegeMask.Bind, Description = "Create a resource or collection", }); // emclient->translated
        write.Items.Add(new() { Id = XmlNs.Dav + "unbind", Privileges = PrivilegeMask.Unbind, Description = "Delete a resource or collection", }); // emclient->translated
        write.Items.Add(new() { Id = XmlNs.Dav + "write-content", Privileges = PrivilegeMask.WriteContent, Description = "Write content", }); // emclient->translated
        write.Items.Add(new() { Id = XmlNs.Dav + "write-properties", Privileges = PrivilegeMask.WriteProperties, Description = "Write properties", });// emclient->translated
        all.Items.Add(write);

        all.Items.Add(new() { Id = XmlNs.Caldav + "read-free-busy", Privileges = PrivilegeMask.ReadFreeBusy, Description = "Read the free/busy information for a calendar collection", });// emclient->translated
        all.Items.Add(new() { Id = XmlNs.Dav + "read-acl", Privileges = PrivilegeMask.ReadAcl, Description = "Read ACLs for a resource or collection", });  // emclient->translated
        all.Items.Add(new() { Id = XmlNs.Dav + "read-current-user-privilege-set", Privileges = PrivilegeMask.ReadCurrentUserPrivilegeSet, Description = "Read the details of the current user's access control to this resource", }); // emclient->translated
        all.Items.Add(new() { Id = XmlNs.Dav + "write-acl", Privileges = PrivilegeMask.WriteAcl, Description = "Write ACLs for a resource or collection", });// emclient->translated

        var scheduleDeliver = new PrivilegeItem { Id = XmlNs.Caldav + "schedule-deliver", Privileges = PrivilegeMask.ScheduleDeliver, Description = "Receiving of scheduling messages", Items = [], };
        scheduleDeliver.Items.Add(new() { Id = XmlNs.Caldav + "schedule-deliver-invite", Privileges = PrivilegeMask.ScheduleDeliverInvite, Description = "Deliver scheduling invitations from an organiser to this scheduling inbox", });
        scheduleDeliver.Items.Add(new() { Id = XmlNs.Caldav + "schedule-deliver-reply", Privileges = PrivilegeMask.ScheduleDeliverReply, Description = "Deliver scheduling replies from an attendee to this scheduling inbox", });
        scheduleDeliver.Items.Add(new() { Id = XmlNs.Caldav + "schedule-query-freebusy", Privileges = PrivilegeMask.ScheduleQueryFreebusy, Description = "Allow free/busy enquiries targeted at the owner of this scheduling inbox", });
        all.Items.Add(scheduleDeliver);

        var scheduleSend = new PrivilegeItem { Id = XmlNs.Caldav + "schedule-send", Privileges = PrivilegeMask.ScheduleSend, Description = "Sending of scheduling messages", Items = [], };
        scheduleSend.Items.Add(new() { Id = XmlNs.Caldav + "schedule-send-invite", Privileges = PrivilegeMask.ScheduleSendInvite, Description = "Send scheduling invitations as an organiser from the owner of this scheduling outbox.", });
        scheduleSend.Items.Add(new() { Id = XmlNs.Caldav + "schedule-send-reply", Privileges = PrivilegeMask.ScheduleSendReply, Description = "Send scheduling replies as an attendee from the owner of this scheduling outbox.", });
        scheduleSend.Items.Add(new() { Id = XmlNs.Caldav + "schedule-send-freebusy", Privileges = PrivilegeMask.ScheduleSendFreebusy, Description = "Send free/busy enquiries", });
        all.Items.Add(scheduleSend);
        return all;
    }

    public static bool HasAnyOf(this PrivilegeMask permissions, PrivilegeMask required = PrivilegeMask.All)
    {
        return (permissions & required) != PrivilegeMask.None;
    }

    public static List<PrivilegeItem> LoadList(PrivilegeMask mask = PrivilegeMask.None, PrivilegeMask exclude = PrivilegeMask.None)
    {
        var privileges = LoadTree();
        var list = new List<PrivilegeItem>();
        if (mask.HasFlag(privileges.Privileges))
        {
            if (!exclude.HasFlag(privileges.Privileges))
            {
                list.Add(privileges);
            }
        }
        return Unnest(list, privileges, mask, exclude);
    }

    private static List<PrivilegeItem> Unnest(List<PrivilegeItem> list, PrivilegeItem item, PrivilegeMask mask, PrivilegeMask exclude)
    {
        foreach (var p1 in item.Items ?? [])
        {
            if (mask.HasFlag(p1.Privileges))
            {
                if (!exclude.HasFlag(p1.Privileges))
                {
                    list.Add(p1);
                }
            }
            if (p1.Items is not null)
            {
                Unnest(list, p1, mask, exclude);
            }
        }
        return list;
    }

    public static PrivilegeMask CalculateMask(this List<PrivilegeItem> list)
    {
        var mask = PrivilegeMask.None;
        foreach (var pi in list)
        {
            mask |= pi.Privileges;
        }
        return mask;
    }

    public static string ToBitString(this PrivilegeMask mask)
    {
        return Convert.ToString((ushort)mask, 2).PadLeft(16, '0');
    }
}
