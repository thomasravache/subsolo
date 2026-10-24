using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Subsolo.Modules.Engagement.Application;

namespace Subsolo.Modules.Engagement.Infrastructure.Notices;

/// <summary>SPEC-0032 (H31-01) — uma linha de <c>engagement.digest_runs</c>: o dia (São Paulo) com resumo concluído.</summary>
internal sealed class DigestRunRow
{
    public DateOnly DigestDate { get; set; }

    public DateTimeOffset CompletedAtUtc { get; set; }
}

/// <summary>SPEC-0032 (H31-01) — tabela <c>engagement.digest_runs(digest_date date PK, completed_at_utc timestamptz)</c>.</summary>
internal sealed class DigestRunConfiguration : IEntityTypeConfiguration<DigestRunRow>
{
    public void Configure(EntityTypeBuilder<DigestRunRow> builder)
    {
        builder.ToTable("digest_runs");
        builder.HasKey(row => row.DigestDate);
        builder.Property(row => row.DigestDate).HasColumnName("digest_date").HasColumnType("date").ValueGeneratedNever();
        builder.Property(row => row.CompletedAtUtc).HasColumnName("completed_at_utc");
    }
}

/// <summary>
/// SPEC-0032 (H31-01) — <see cref="IDigestRuns"/> sobre <c>engagement.digest_runs</c>: a gravação é um upsert pela data
/// (uma reexecução no mesmo dia só atualiza a hora), sem passar pela unidade de trabalho.
/// </summary>
internal sealed class DigestRuns(EngagementDbContext db) : IDigestRuns
{
    public Task<bool> CompletedAsync(DateOnly digestDate, CancellationToken cancellationToken) =>
        db.DigestRuns.AsNoTracking().AnyAsync(row => row.DigestDate == digestDate, cancellationToken);

    public async Task RecordAsync(DateOnly digestDate, DateTimeOffset completedAtUtc, CancellationToken cancellationToken) =>
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            insert into engagement.digest_runs (digest_date, completed_at_utc)
            values ({digestDate}, {completedAtUtc})
            on conflict (digest_date) do update set completed_at_utc = excluded.completed_at_utc
            """,
            cancellationToken);
}
