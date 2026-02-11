using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;

namespace Calendare.Data.Models;

public class TrxJournal
{
    public int Id { get; set; }

    public string Method { get; set; } = null!;
    public string Path { get; set; } = null!;

    public List<string> RequestHeaders { get; set; } = [];
    public string? RequestBody { get; set; }

    public int? ResponseStatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public string? ResponseError { get; set; }

    public List<string> ResponseHeaders { get; set; } = [];

    public Instant Created { get; set; }
}


public class TrxJournalConfiguration : IEntityTypeConfiguration<TrxJournal>
{
    public void Configure(EntityTypeBuilder<TrxJournal> builder)
    {
        builder.Property(c => c.Created).HasDefaultValueSql("now()").ValueGeneratedOnAddOrUpdate();
    }
}
