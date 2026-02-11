using NodaTime;

namespace Calendare.Server.Api.Models;

public class MailboxItem
{
    // public int Id { get; set; }
    public string Uid { get; set; } = default!;
    public int Sequence { get; set; }

    public string SenderEmail { get; set; } = null!;
    public string ReceiverEmail { get; set; } = null!;
    public string Body { get; set; } = null!;
    public Instant Created { get; set; }
    public Instant? Processed { get; set; }
}


