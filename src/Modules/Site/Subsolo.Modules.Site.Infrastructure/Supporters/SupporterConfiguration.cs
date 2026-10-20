using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Subsolo.Modules.Site.Domain.Supporters;

namespace Subsolo.Modules.Site.Infrastructure.Supporters;

/// <summary>
/// SPEC-0028 — tabela <c>site.supporters</c>: id, nome (até 60), link (até 300, opcional) e criação, com índice pela
/// criação (a ordem da lista pública).
/// </summary>
internal sealed class SupporterConfiguration : IEntityTypeConfiguration<Supporter>
{
    public void Configure(EntityTypeBuilder<Supporter> builder)
    {
        builder.ToTable("supporters");
        builder.HasKey(supporter => supporter.Id);
        builder.Property(supporter => supporter.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(supporter => supporter.Name).HasColumnName("name").HasMaxLength(Supporter.NameMaxLength);
        builder.Property(supporter => supporter.Url).HasColumnName("url").HasMaxLength(Supporter.UrlMaxLength);
        builder.Property(supporter => supporter.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Ignore(supporter => supporter.DomainEvents);
        builder.HasIndex(supporter => supporter.CreatedAtUtc);
    }
}
