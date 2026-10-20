using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Subsolo.Modules.Engagement.Domain.Attendances;

namespace Subsolo.Modules.Engagement.Infrastructure.Attendances;

/// <summary>
/// SPEC-0029 — tabela <c>engagement.attendances</c> (§6): usuário, show e criação; chave primária (usuário, show) — um
/// "vou" por usuário e show — e índice por show (contagens e quem vai, para o lembrete).
/// </summary>
internal sealed class AttendanceConfiguration : IEntityTypeConfiguration<Attendance>
{
    public void Configure(EntityTypeBuilder<Attendance> builder)
    {
        builder.ToTable("attendances");
        builder.HasKey(attendance => new { attendance.UserId, attendance.ShowId })
            .HasName(EngagementDbContext.AttendancesPrimaryKey);
        builder.Property(attendance => attendance.UserId).HasColumnName("user_id");
        builder.Property(attendance => attendance.ShowId).HasColumnName("show_id");
        builder.Property(attendance => attendance.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Ignore(attendance => attendance.DomainEvents);
        builder.HasIndex(attendance => attendance.ShowId);
    }
}
