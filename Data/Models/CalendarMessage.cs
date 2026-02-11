using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;

namespace Calendare.Data.Models;

public class SchedulingMessage
{
    public int Id { get; set; }
    public string Uid { get; set; } = default!;
    public int Sequence { get; set; }

    public string SenderEmail { get; set; } = null!;
    public string ReceiverEmail { get; set; } = null!;
    public string Body { get; set; } = null!;
    public Instant Created { get; set; }
    public Instant? Processed { get; set; }
}


public class SchedulingMessageConfiguration : IEntityTypeConfiguration<SchedulingMessage>
{
    public void Configure(EntityTypeBuilder<SchedulingMessage> builder)
    {
        builder.Property(c => c.Created).HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();
    }
}
