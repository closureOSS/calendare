using System;
using System.Collections.Generic;
using Calendare.Data.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;
using Npgsql.EntityFrameworkCore.PostgreSQL.ValueGeneration;

namespace Calendare.Data.Models;

public class Collection
{
    public int Id { get; set; }
    public int OwnerId { get; set; }
    public Usr Owner { get; set; } = null!;
    public Collection? Parent { get; set; }
    public int? ParentId { get; set; }
    public ICollection<Collection> Children { get; set; } = new List<Collection>();
    public string Uri { get; set; } = default!;
    public Guid PermanentId { get; set; }
    public CollectionType CollectionType { get; set; } = CollectionType.Collection;
    public CollectionSubType CollectionSubType { get; set; } = CollectionSubType.Default;

    public string? ParentContainerUri { get; set; }
    public string? Etag { get; set; }
    public string? DisplayName { get; set; }
    public string? Timezone { get; set; }
    public string? Description { get; set; } = "";
    public string? Color { get; set; }
    public int? OrderBy { get; set; }
    public string? ScheduleTransparency { get; set; }

    public PrincipalType? PrincipalType { get; set; }
    public int? PrincipalTypeId { get; set; }

    public PrivilegeMask OwnerProhibit { get; set; }
    public PrivilegeMask OwnerMask { get; set; }

    public PrivilegeMask AuthorizedProhibit { get; set; }
    public PrivilegeMask AuthorizedMask { get; set; }

    public PrivilegeMask GlobalPermitSelf { get; set; }
    public PrivilegeMask GlobalPermit { get; set; }

    public Instant Created { get; set; }
    public Instant Modified { get; set; }

    public ICollection<CollectionObject> Objects { get; } = new List<CollectionObject>();
    public ICollection<CollectionProperty> Properties { get; } = new List<CollectionProperty>();
    public ICollection<Collection> Groups { get; } = new List<Collection>();
    public ICollection<Collection> Members { get; } = new List<Collection>();
    public ICollection<SyncJournal> SyncJournal { get; } = new List<SyncJournal>();
    public ICollection<GrantRelation> Grants { get; } = new List<GrantRelation>();
}


public class CollectionConfiguration : IEntityTypeConfiguration<Collection>
{
    public void Configure(EntityTypeBuilder<Collection> builder)
    {
        builder.HasAlternateKey(k => k.Uri);
        builder.Property(b => b.PermanentId).HasValueGenerator<NpgsqlSequentialGuidValueGenerator>();
        builder.Property(x => x.OwnerProhibit)
            .HasConversion(v => v.ToBitArray(), v => v.FromBitArray())
            .HasColumnType("bit(16)");
        builder.Property(x => x.OwnerMask)
            .HasConversion(v => v.ToBitArray(), v => v.FromBitArray())
            .HasColumnType("bit(16)");
        builder.Property(x => x.AuthorizedProhibit)
            .HasConversion(v => v.ToBitArray(), v => v.FromBitArray())
            .HasColumnType("bit(16)");
        builder.Property(x => x.AuthorizedMask)
            .HasConversion(v => v.ToBitArray(), v => v.FromBitArray())
            .HasColumnType("bit(16)");
        builder.Property(x => x.GlobalPermitSelf)
            .HasConversion(v => v.ToBitArray(), v => v.FromBitArray())
            .HasColumnType("bit(16)");
        builder.Property(x => x.GlobalPermit)
            .HasConversion(v => v.ToBitArray(), v => v.FromBitArray())
            .HasColumnType("bit(16)");
        builder.Property(c => c.Created).HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();
        builder.Property(c => c.Modified).HasDefaultValueSql("now()").ValueGeneratedOnAdd();
        builder.HasOne(c => c.Parent).WithMany(c => c.Children).HasForeignKey(k => k.ParentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(c => c.Groups).WithMany(c => c.Members).UsingEntity<CollectionGroup>(
            l => l.HasOne(c => c.Group).WithMany().HasForeignKey(c => c.GroupId),
            r => r.HasOne(c => c.Member).WithMany().HasForeignKey(c => c.MemberId)
        );
    }
}
