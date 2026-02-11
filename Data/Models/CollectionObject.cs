using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;

namespace Calendare.Data.Models;

public class CollectionObject
{
    public int Id { get; set; }

    public int CollectionId { get; set; }
    public Collection Collection { get; set; } = null!;

    public string VObjectType { get; set; } = default!;
    public string Uri { get; set; } = default!;
    public string Uid { get; set; } = default!;
    public string RawData { get; set; } = default!;
    public string Etag { get; set; } = default!;
    public string? ScheduleTag { get; set; }

    public ObjectCalendar? CalendarItem { get; set; }
    public ObjectAddress? AddressItem { get; set; }

    public bool IsPublic { get; set; }
    public bool IsPrivate { get; set; }
    public bool IsConfidential { get; set; }

    public int OwnerId { get; set; }
    public Usr Owner { get; set; } = null!;
    public int ActualUserId { get; set; }
    public Usr ActualUser { get; set; } = null!;

    public Instant Created { get; set; }
    public Instant Modified { get; set; }
    public Instant? Deleted { get; set; }
}


public class CollectionObjectConfiguration : IEntityTypeConfiguration<CollectionObject>
{
    public void Configure(EntityTypeBuilder<CollectionObject> builder)
    {
        builder.HasAlternateKey(k => k.Uri);
        builder.Property(c => c.Created).HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();
        builder.Property(c => c.Modified).HasDefaultValueSql("now()").ValueGeneratedOnAdd();

        builder.HasOne(c => c.CalendarItem)
            .WithOne(c => c.CollectionObject)
            // .HasForeignKey<ObjectCalendar>(c => c.CollectionObjectId)
            // .HasForeignKey<CollectionObject>(c => c.CalendarItemId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(c => c.AddressItem)
            .WithOne(c => c.CollectionObject)
            // .HasForeignKey<CollectionObject>(c => c.AddressItemId)
            // .HasForeignKey<ObjectAddress>(c => c.CollectionObjectId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
        // builder.HasMany(c => c.Data).WithOne(c => c.Collection).IsRequired();
        // builder.HasMany(c => c.Items).WithOne(c => c.Collection).IsRequired();
        // builder.HasMany(e => e.Properties).WithOne(e => e.Collection).HasForeignKey(k => k.Name).IsRequired();
    }
}
