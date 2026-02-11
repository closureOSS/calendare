using NodaTime;

namespace Calendare.Data.Models;

public interface IHistorize
{
    public Instant CreatedOn { get; set; }
    public Instant? DeletedOn { get; set; }
}
