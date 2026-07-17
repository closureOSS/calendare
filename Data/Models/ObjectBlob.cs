using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;

namespace Calendare.Data.Models;

public class ObjectBlob
{
    public int Id { get; set; }

    public int CollectionObjectId { get; set; }
    public CollectionObject CollectionObject { get; set; } = null!;

    public string ContentType { get; set; } = default!;
    public string Location { get; set; } = default!;
    public long? ContentLength { get; set; }
    public string? DisplayName { get; set; }
    public string? LanguageCode { get; set; }

    /// <summary>File create date (modification possible)</summary>
    public Instant Created { get; set; }

    /// <summary>File modification date (modification possible)</summary>
    public Instant Modified { get; set; }

    /// <summary>File last access date</summary>
    public Instant LastAccess { get; set; }
}


public class ObjectBlobConfiguration : IEntityTypeConfiguration<ObjectBlob>
{
    public void Configure(EntityTypeBuilder<ObjectBlob> builder)
    {
        builder.Property(c => c.Created).HasDefaultValueSql("now()").ValueGeneratedOnAdd();
        builder.Property(c => c.Modified).HasDefaultValueSql("now()").ValueGeneratedOnAdd();
        builder.Property(c => c.LastAccess).HasDefaultValueSql("now()").ValueGeneratedOnAdd();
    }
}
