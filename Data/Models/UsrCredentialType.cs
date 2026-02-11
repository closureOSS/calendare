using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Calendare.Data.Models;

public class UsrCredentialType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}


public class UsrCredentialTypeConfiguration : IEntityTypeConfiguration<UsrCredentialType>
{
    public void Configure(EntityTypeBuilder<UsrCredentialType> builder)
    {
        builder.HasIndex(a => a.Label).IsUnique();
    }
}
