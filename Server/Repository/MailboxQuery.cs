namespace Calendare.Server.Repository;

public class MailboxQuery : RepositoryQuery
{
    public string? Uid { get; set; }
    public string? SenderEmail { get; set; }
    public bool IncludeProcessed { get; set; }
}
