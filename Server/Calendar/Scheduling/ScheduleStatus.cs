namespace Calendare.Server.Calendar.Scheduling;
/// <summary>
/// <see cref="https://datatracker.ietf.org/doc/html/rfc6638#section-3.2.9">Schedule Status Values</see>
/// and also <see cref="https://datatracker.ietf.org/doc/html/rfc5546#section-3.6">Status Replies</see>
/// </summary>
public static class ScheduleStatus
{
    public const string Pending = "1.0";
    public const string Sent = "1.1";
    public const string Delivered = "1.2";
    public const string Success = "2.0;Success";
    public const string UnknownRecipient = "3.7;Invalid calendar user";
    public const string InsufficientPrivileges = "3.8";
    public const string DeliveryFailed = "5.1";
    public const string NoDeliveryMethod = "5.2";
    public const string Rejected = "5.3";
}
