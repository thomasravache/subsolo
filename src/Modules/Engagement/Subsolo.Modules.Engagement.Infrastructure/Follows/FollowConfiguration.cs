using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Subsolo.Modules.Engagement.Domain.Follows;

namespace Subsolo.Modules.Engagement.Infrastructure.Follows;

/// <summary>
/// SPEC-0027 — tabela <c>engagement.follows</c> (§6): usuário, tipo do alvo (<c>city</c> | <c>profile</c>), chave do
/// alvo (slug da cidade ou id do perfil) e criação; chave primária (usuário, tipo, chave) — um follow por usuário e alvo
/// — e índice por alvo (contagem e quem segue, para os avisos).
/// </summary>
internal sealed class FollowConfiguration : IEntityTypeConfiguration<Follow>
{
    public void Configure(EntityTypeBuilder<Follow> builder)
    {
        builder.ToTable("follows");
        builder.HasKey(follow => new { follow.UserId, follow.TargetKind, follow.TargetKey })
            .HasName(EngagementDbContext.FollowsPrimaryKey);
        builder.Property(follow => follow.UserId).HasColumnName("user_id");
        builder.Property(follow => follow.TargetKind).HasColumnName("target_kind").HasColumnType("text");
        builder.Property(follow => follow.TargetKey).HasColumnName("target_key").HasColumnType("text");
        builder.Property(follow => follow.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Ignore(follow => follow.Target);
        builder.Ignore(follow => follow.DomainEvents);
        builder.HasIndex(follow => new { follow.TargetKind, follow.TargetKey });
    }
}
