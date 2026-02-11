using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;

namespace Calendare.Data.Models;

public class DataMigrationDto
{
    public string Id { get; set; } = string.Empty;
    public Instant CreatedOn { get; set; }
    public Instant? CompletedOn { get; set; }
}


public class DataMigrationConfiguration : IEntityTypeConfiguration<DataMigrationDto>
{
    public void Configure(EntityTypeBuilder<DataMigrationDto> builder)
    {
        builder.Property(a => a.CreatedOn).HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();
    }
}
