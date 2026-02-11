using NodaTime;

namespace Calendare.Server.Api.Models;

public class AddressbookItem
{
    public string Uri { get; set; } = default!;
    public string Uid { get; set; } = default!;
    public string Etag { get; set; } = default!;
    public string? RawData { get; set; }
    public string? VObjectType { get; set; }
    public Instant Created { get; set; }
    public Instant Modified { get; set; }

    public string? FormattedName { get; set; }
    public string? Name { get; set; }
    public string? NickName { get; set; }
}


