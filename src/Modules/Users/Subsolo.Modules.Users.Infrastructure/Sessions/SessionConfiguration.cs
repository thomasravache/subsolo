using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Subsolo.Modules.Users.Domain.Sessions;
using Subsolo.Modules.Users.Domain.Users;

namespace Subsolo.Modules.Users.Infrastructure.Sessions;

/// <summary>
/// SPEC-0014 — tabela <c>users.sessions</c>: chave = hash SHA-256 do token (o token não é gravado), método como
/// <c>magic_link</c> | <c>google</c>, prazos e revogação.
/// </summary>
internal sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("sessions");
        builder.HasKey(session => session.IdHash);
        builder.Property(session => session.IdHash).HasColumnName("id_hash").HasMaxLength(Session.IdHashLength).IsFixedLength();
        builder.Property(session => session.UserId).HasColumnName("user_id");
        builder.Property(session => session.Method)
            .HasColumnName("method")
            .HasMaxLength(20)
            .HasConversion(method => SignInMethods.ToCode(method), code => SignInMethods.FromCode(code));
        builder.Property(session => session.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(session => session.LastSeenAtUtc).HasColumnName("last_seen_at_utc");
        builder.Property(session => session.ExpiresAtUtc).HasColumnName("expires_at_utc");
        builder.Property(session => session.AbsoluteExpiresAtUtc).HasColumnName("absolute_expires_at_utc");
        builder.Property(session => session.RevokedAtUtc).HasColumnName("revoked_at_utc");
        builder.HasOne<User>().WithMany().HasForeignKey(session => session.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(session => session.UserId);
    }
}
