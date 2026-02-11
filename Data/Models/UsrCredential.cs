using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;

namespace Calendare.Data.Models;

public class UsrCredential
{
    public int Id { get; set; }
    public int UsrId { get; set; }
    public Usr Usr { get; set; } = null!;
    public UsrCredentialType CredentialType { get; set; } = null!;
    public int CredentialTypeId { get; set; }
    public string Accesskey { get; set; } = default!;
    public string? Secret { get; set; }
    public Instant? Locked { get; set; }
    public Interval? Validity { get; set; }

    public Instant? LastUsed { get; set; }
    public Instant Created { get; set; }
    public Instant Modified { get; set; }
}


public class UsrCredentialConfiguration : IEntityTypeConfiguration<UsrCredential>
{
    public void Configure(EntityTypeBuilder<UsrCredential> builder)
    {
        builder.Property(a => a.Created).HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();
        builder.Property(a => a.Modified).HasDefaultValueSql("now()").ValueGeneratedOnAdd();
        builder.HasIndex(a => new { a.Accesskey, a.CredentialTypeId }).IsUnique();
    }
}
