using Calendare.Data.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;

namespace Calendare.Data.Models;

public class GrantRelation
{
    public int Id { get; set; }
    public int GrantorId { get; set; }
    public Collection Grantor { get; set; } = null!;
    public int GranteeId { get; set; }
    public Collection Grantee { get; set; } = null!;

    public GrantType GrantType { get; set; } = null!;
    public int GrantTypeId { get; set; }

    public PrivilegeMask Privileges { get; set; }
    public bool IsIndirect { get; set; }

    public Instant Created { get; set; }
    public Instant Modified { get; set; }
}


public class GrantRelationConfiguration : IEntityTypeConfiguration<GrantRelation>
{
    public void Configure(EntityTypeBuilder<GrantRelation> builder)
    {
        builder.HasAlternateKey(c => new { c.GrantorId, c.GranteeId });
        builder.Property(a => a.Created).HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();
        builder.Property(a => a.Modified).HasDefaultValueSql("now()").ValueGeneratedOnAdd();
        builder.Property(a => a.IsIndirect).HasDefaultValue("false");
        builder.Property(x => x.Privileges)
            .HasConversion(v => v.ToBitArray(), v => v.FromBitArray())
            .HasColumnType("bit(16)");
        builder.HasOne(c => c.Grantor)
            .WithMany(c => c.Grants)
            // .HasForeignKey<Collection>(c => c.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(c => c.Grantee)
            // .WithMany(c => c.Grants)
            .WithMany()
            // .WithOne()
            // .HasForeignKey<Collection>(c => c.Id)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
