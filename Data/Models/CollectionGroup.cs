namespace Calendare.Data.Models;

public class CollectionGroup
{
    public int GroupId { get; set; }
    public Collection Group { get; set; } = null!;
    public int MemberId { get; set; }
    public Collection Member { get; set; } = null!;
}
