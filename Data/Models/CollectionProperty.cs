using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;

namespace Calendare.Data.Models;

public class CollectionProperty
{
    public int CollectionId { get; set; }
    public string Name { get; set; } = null!;
    public string Value { get; set; } = null!;
    public Instant Modified { get; set; }
    public int ModifiedById { get; set; }
    public Usr ModifiedBy { get; set; } = null!;
}


public class CollectionPropertyConfiguration : IEntityTypeConfiguration<CollectionProperty>
{
    public void Configure(EntityTypeBuilder<CollectionProperty> builder)
    {
        builder.HasKey(e => new { e.CollectionId, e.Name, });
        builder.Property(c => c.Modified).HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();
    }
}
