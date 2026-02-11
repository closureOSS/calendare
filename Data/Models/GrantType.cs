using Calendare.Data.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Calendare.Data.Models;

public class GrantType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Confers { get; set; } = "F";
    public PrivilegeMask Privileges { get; set; }
}


public class GrantTypeConfiguration : IEntityTypeConfiguration<GrantType>
{
    public void Configure(EntityTypeBuilder<GrantType> builder)
    {
        builder.Property(x => x.Privileges)
            .HasConversion(v => v.ToBitArray(), v => v.FromBitArray())
            .HasColumnType("bit(16)");
    }
}
