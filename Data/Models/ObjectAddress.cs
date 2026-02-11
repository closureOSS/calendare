using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;

namespace Calendare.Data.Models;

public class ObjectAddress
{
    public int Id { get; set; }

    public int CollectionObjectId { get; set; }
    public CollectionObject CollectionObject { get; set; } = null!;

    /// <summary>
    /// FN - https://datatracker.ietf.org/doc/html/rfc6350#section-6.2.1
    /// </summary>
    public string? FormattedName { get; set; }

    /// <summary>
    /// N - https://datatracker.ietf.org/doc/html/rfc6350#section-6.2.2
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// NICKNAME - https://datatracker.ietf.org/doc/html/rfc6350#section-6.2.3
    /// </summary>
    public string? Nickname { get; set; }

    /// <summary>
    /// BDAY - https://datatracker.ietf.org/doc/html/rfc6350#section-6.2.5
    /// </summary>
    public Instant? Birthday { get; set; }

    public string? CardVersion { get; set; }
}


public class ObjectAddressConfiguration : IEntityTypeConfiguration<ObjectAddress>
{
    public void Configure(EntityTypeBuilder<ObjectAddress> builder)
    {
    }
}
