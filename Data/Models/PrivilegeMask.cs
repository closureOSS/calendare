using System;

namespace Calendare.Data.Models;

[Flags]
public enum PrivilegeMask : ushort
{
    None = 0b0000_0000_0000_0000,
    Read = 0b0000_0000_0000_0001,   // https://datatracker.ietf.org/doc/html/rfc3744#section-3.1
    WriteProperties = 0b0000_0000_0000_0010,   // https://datatracker.ietf.org/doc/html/rfc3744#section-3.3
    WriteContent = 0b0000_0000_0000_0100,   // https://datatracker.ietf.org/doc/html/rfc3744#section-3.4
    Share = 0b0000_0000_0000_1000, // https://datatracker.ietf.org/doc/html/draft-pot-webdav-resource-sharing-04#section-5.2
    ReadAcl = 0b0000_0000_0001_0000,   // https://datatracker.ietf.org/doc/html/rfc3744#section-3.6
    ReadCurrentUserPrivilegeSet = 0b0000_0000_0010_0000,   // https://datatracker.ietf.org/doc/html/rfc3744#section-3.7
    WriteAcl = 0b0000_0001_0000_0000,   // https://datatracker.ietf.org/doc/html/rfc3744#section-3.8
    Bind = 0b0000_0000_0100_0000,   // https://datatracker.ietf.org/doc/html/rfc3744#section-3.9
    Unbind = 0b0000_0000_1000_0000,   // https://datatracker.ietf.org/doc/html/rfc3744#section-3.10
    ReadFreeBusy = 0b0000_0010_0000_0000,  // https://datatracker.ietf.org/doc/html/rfc4791#section-6.1.1
    ScheduleDeliverInvite = 0b0000_0100_0000_0000,
    ScheduleDeliverReply = 0b0000_1000_0000_0000,
    ScheduleQueryFreebusy = 0b0001_0000_0000_0000,
    ScheduleSendInvite = 0b0010_0000_0000_0000,
    ScheduleSendReply = 0b0100_0000_0000_0000,
    ScheduleSendFreebusy = 0b1000_0000_0000_0000,
    All = 0b1111_1111_1111_1111,   // https://datatracker.ietf.org/doc/html/rfc3744#section-3.11

    ScheduleDeliver = ScheduleDeliverInvite | ScheduleDeliverReply | ScheduleQueryFreebusy,
    ScheduleSend = ScheduleSendInvite | ScheduleSendReply | ScheduleSendFreebusy,
    Write = WriteProperties | WriteContent | Bind | Unbind,   // https://datatracker.ietf.org/doc/html/rfc3744#section-3.2

    // select rt.name, rt."privileges", lpad("privileges"::text,16,'0')::bit(16)::int from relationship_type rt
}
