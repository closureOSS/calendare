using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;

namespace Calendare.Data.Models;

public class Usr
{
    public int Id { get; set; }
    public bool IsActive { get; set; }
    public string Username { get; set; } = default!;
    public string? Email { get; set; }
    public Instant? EmailOk { get; set; }
    public string? DateFormatType { get; set; }
    public string? Locale { get; set; }
    public Instant? Created { get; set; }
    public Instant? Modified { get; set; }
    public Instant? Closed { get; set; }

    public ICollection<Collection> Collections { get; } = new List<Collection>();
    public ICollection<UsrCredential> Credentials { get; } = new List<UsrCredential>();
}


public class UsrConfiguration : IEntityTypeConfiguration<Usr>
{
    public void Configure(EntityTypeBuilder<Usr> builder)
    {
        builder.Property(a => a.Created).HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();
        builder.Property(a => a.Modified).HasDefaultValueSql("now()").ValueGeneratedOnAdd();

        builder.HasMany(u => u.Collections).WithOne(u => u.Owner).HasForeignKey(e => e.OwnerId).IsRequired();
        builder.HasMany(u => u.Credentials).WithOne(u => u.Usr).HasForeignKey(e => e.UsrId).IsRequired();

        builder.HasIndex(a => a.Username).IsUnique();
    }
}
