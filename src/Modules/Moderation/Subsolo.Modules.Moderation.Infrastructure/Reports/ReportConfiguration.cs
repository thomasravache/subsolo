using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Subsolo.Modules.Moderation.Domain.Reports;

namespace Subsolo.Modules.Moderation.Infrastructure.Reports;

/// <summary>
/// SPEC-0033 — tabela <c>moderation.reports</c> (§6): o show, quem denunciou (nulo depois da exclusão da conta), o
/// motivo, o detalhe (até 500), a situação, as datas, quem decidiu e a nota interna (Emenda 2); o índice único parcial
/// <c>(show_id, reporter_user_id) WHERE status = 'pending'</c>, o da fila (situação + criação), o do show e o de quem
/// denunciou (export e exclusão). A decisão usa o token de concorrência otimista (<c>xmin</c>).
/// </summary>
internal sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    /// <summary>Propriedade de sombra do token <c>xmin</c>.</summary>
    public const string VersionProperty = "Version";

    private const int ReasonMaxLength = 30;

    private const int StatusMaxLength = 10;

    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.ToTable("reports");
        builder.HasKey(report => report.Id);
        builder.Property(report => report.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property<uint>(VersionProperty).IsRowVersion();
        builder.Property(report => report.ShowId).HasColumnName("show_id");
        builder.Property(report => report.ReporterUserId).HasColumnName("reporter_user_id");
        builder.Property(report => report.Reason).HasColumnName("reason").HasMaxLength(ReasonMaxLength);
        builder.Property(report => report.Details).HasColumnName("details").HasMaxLength(ReportReasons.DetailsMaxLength);
        builder.Property(report => report.Status).HasColumnName("status").HasMaxLength(StatusMaxLength);
        builder.Property(report => report.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(report => report.DecidedAtUtc).HasColumnName("decided_at_utc");
        builder.Property(report => report.DecidedBy).HasColumnName("decided_by");
        builder.Property(report => report.DecisionNote).HasColumnName("decision_note").HasMaxLength(Report.DecisionNoteMaxLength);
        builder.Ignore(report => report.IsPending);

        builder.HasIndex(report => new { report.ShowId, report.ReporterUserId }, ModerationDbContext.PendingReportIndex)
            .IsUnique()
            .HasFilter($"status = '{ReportStatuses.Pending}'");
        builder.HasIndex(report => new { report.Status, report.CreatedAtUtc });
        builder.HasIndex(report => report.ShowId);
        builder.HasIndex(report => report.ReporterUserId);
    }
}
