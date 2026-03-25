using System;

namespace Calendare.Data.Models;

[Flags]
public enum PrivilegeMask : ushort
{
    /// <summary>
    /// No privileges
    /// </summary>
    None = 0b0000_0000_0000_0000,
    /// <summary>
    /// https://datatracker.ietf.org/doc/html/rfc3744#section-3.1
    /// </summary>
    Read = 0b0000_0000_0000_0001,
    /// <summary>
    /// https://datatracker.ietf.org/doc/html/rfc3744#section-3.3
    /// </summary>
    WriteProperties = 0b0000_0000_0000_0010,
    /// <summary>
    /// https://datatracker.ietf.org/doc/html/rfc3744#section-3.4
    /// </summary>
    WriteContent = 0b0000_0000_0000_0100,
    /// <summary>
    /// https://datatracker.ietf.org/doc/html/draft-pot-webdav-resource-sharing-04#section-5.2
    /// </summary>
    Share = 0b0000_0000_0000_1000,
    /// <summary>
    ///  https://datatracker.ietf.org/doc/html/rfc3744#section-3.6
    /// </summary>
    ReadAcl = 0b0000_0000_0001_0000,
    /// <summary>
    /// https://datatracker.ietf.org/doc/html/rfc3744#section-3.7
    /// </summary>
    ReadCurrentUserPrivilegeSet = 0b0000_0000_0010_0000,
    /// <summary>
    /// https://datatracker.ietf.org/doc/html/rfc3744#section-3.8
    ///
    /// For administrative permissions: Allow system operation
    /// </summary>
    WriteAcl = 0b0000_0001_0000_0000,
    /// <summary>
    /// https://datatracker.ietf.org/doc/html/rfc3744#section-3.9
    /// </summary>
    Bind = 0b0000_0000_0100_0000,
    /// <summary>
    /// https://datatracker.ietf.org/doc/html/rfc3744#section-3.10
    ///
    /// For administrative permissions: Allow creation of principals
    /// </summary>
    Unbind = 0b0000_0000_1000_0000,
    /// <summary>
    /// https://datatracker.ietf.org/doc/html/rfc4791#section-6.1.1
    /// </summary>
    ReadFreeBusy = 0b0000_0010_0000_0000,
    ScheduleDeliverInvite = 0b0000_0100_0000_0000,
    ScheduleDeliverReply = 0b0000_1000_0000_0000,
    ScheduleQueryFreebusy = 0b0001_0000_0000_0000,
    ScheduleSendInvite = 0b0010_0000_0000_0000,
    ScheduleSendReply = 0b0100_0000_0000_0000,
    ScheduleSendFreebusy = 0b1000_0000_0000_0000,
    /// <summary>
    /// https://datatracker.ietf.org/doc/html/rfc3744#section-3.11
    /// </summary>
    All = 0b1111_1111_1111_1111,

    ScheduleDeliver = ScheduleDeliverInvite | ScheduleDeliverReply | ScheduleQueryFreebusy,
    ScheduleSend = ScheduleSendInvite | ScheduleSendReply | ScheduleSendFreebusy,
    /// <summary>
    /// https://datatracker.ietf.org/doc/html/rfc3744#section-3.2
    /// </summary>
    Write = WriteProperties | WriteContent | Bind | Unbind,

    // select rt.name, rt."privileges", lpad("privileges"::text,16,'0')::bit(16)::int from relationship_type rt
}
