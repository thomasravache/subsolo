using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Subsolo.Modules.Users.Domain.MagicLinks;
using Subsolo.Modules.Users.Domain.Users;

namespace Subsolo.Modules.Users.Infrastructure.MagicLinks;

/// <summary>
/// SPEC-0016 — tabela <c>users.magic_link_tokens</c>: chave = hash SHA-256 do token (o token não é gravado), e-mail
/// normalizado (índice para o limite por e-mail), <c>return_to</c> saneado, criação, validade e uso; índices de
/// <c>expires_at_utc</c> e <c>used_at_utc</c> para a limpeza periódica.
/// </summary>
internal sealed class MagicLinkTokenConfiguration : IEntityTypeConfiguration<MagicLinkToken>
{
    public void Configure(EntityTypeBuilder<MagicLinkToken> builder)
    {
        builder.ToTable("magic_link_tokens");
        builder.HasKey(token => token.TokenHash);
        builder.Property(token => token.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(MagicLinkToken.TokenHashLength)
            .IsFixedLength();
        builder.Property(token => token.Email).HasColumnName("email").HasMaxLength(User.EmailMaxLength);
        builder.Property(token => token.ReturnTo).HasColumnName("return_to").HasMaxLength(MagicLinkToken.ReturnToMaxLength);
        builder.Property(token => token.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(token => token.ExpiresAtUtc).HasColumnName("expires_at_utc");
        builder.Property(token => token.UsedAtUtc).HasColumnName("used_at_utc");
        builder.HasIndex(token => new { token.Email, token.CreatedAtUtc });
        builder.HasIndex(token => token.ExpiresAtUtc);
        builder.HasIndex(token => token.UsedAtUtc);
    }
}
