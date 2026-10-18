using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Subsolo.Modules.Events.Domain.Styles;

namespace Subsolo.Modules.Events.Infrastructure.Styles;

/// <summary>SPEC-0017 — tabela <c>events.styles</c>, com o slug único (os estilos iniciais vêm da migração <c>Styles</c>).</summary>
internal sealed class StyleConfiguration : IEntityTypeConfiguration<Style>
{
    public void Configure(EntityTypeBuilder<Style> builder)
    {
        builder.ToTable("styles");
        builder.HasKey(style => style.Id);
        builder.Property(style => style.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(style => style.Slug).HasColumnName("slug").HasMaxLength(60);
        builder.Property(style => style.Name).HasColumnName("name").HasMaxLength(30);
        builder.HasIndex(style => style.Slug).IsUnique();
    }
}
