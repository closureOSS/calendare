using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Calendare.Data.Models;

public class PrincipalType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}


public class PrincipalTypeConfiguration : IEntityTypeConfiguration<PrincipalType>
{
    public void Configure(EntityTypeBuilder<PrincipalType> builder)
    {
        builder.HasIndex(a => a.Label).IsUnique();
    }
}
