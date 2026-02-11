using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;

namespace Calendare.Data.Models;

public class PushSubscription
{
    public int Id { get; set; }

    /// <summary>
    /// External unique identification for this subscription
    /// </summary>
    public string SubscriptionId { get; set; } = default!;

    public int UserId { get; set; }
    public Usr? User { get; set; }
    public Collection Resource { get; set; } = default!;
    public int ResourceId { get; set; }

    /// <summary>
    /// Unique push destination URI
    ///
    /// Specifies the absolute URI that identifies the endpoint
    /// where Web Push notifications are sent to.
    ///
    /// A Web Push subscription is uniquely identified by its push resource.
    /// </summary>
    public string PushDestinationUri { get; set; } = default!;

    public string? ClientPublicKeyType { get; set; }
    public string? ClientPublicKey { get; set; }
    public string? AuthSecret { get; set; }
    public string? ContentEncoding { get; set; }

    public int FailCounter { get; set; }
    public Instant Created { get; set; }
    public Instant Expiration { get; set; }
    public Instant? LastNotification { get; set; }
}


public class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> builder)
    {
        builder.Property(c => c.Created).HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();

        // builder.HasMany(c => c.Attendees).WithOne(c => c.Calendar).IsRequired(true).OnDelete(DeleteBehavior.Cascade);
        // builder.HasOne(c => c.Organizer).WithMany().IsRequired(false).OnDelete(DeleteBehavior.SetNull);
    }
}
