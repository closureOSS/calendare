namespace Calendare.Server.Api;

public class UserConfirmEmailRequest
{
    public required string ConfirmationToken { get; set; }
}
