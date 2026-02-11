using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;

namespace Calendare.Data.Models;

public class SyncJournal
{
    public Guid Id { get; set; }

    public int CollectionId { get; set; }
    public Collection Collection { get; set; } = null!;

    public int? CollectionObjectId { get; set; }
    public CollectionObject? CollectionObject { get; set; }

    public string Uri { get; set; } = null!;
    public bool IsDeleted { get; set; }

    public Instant Created { get; set; }
    public Instant? Issued { get; set; }
}


public class SyncJournalConfiguration : IEntityTypeConfiguration<SyncJournal>
{
    public void Configure(EntityTypeBuilder<SyncJournal> builder)
    {
        builder.Property(c => c.Created).HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();
        builder.HasOne(c => c.CollectionObject).WithOne().OnDelete(DeleteBehavior.SetNull);
    }
}
