using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Subsolo.Modules.Events.Domain.Flyers;

namespace Subsolo.Modules.Events.Infrastructure.Flyers;

/// <summary>
/// SPEC-0022 — tabela <c>events.flyers</c>: id <c>char(64)</c> (SHA-256 do original), quem enviou, quando e quando foi
/// ligado a um show; o índice dos não ligados por data serve à limpeza. SPEC-0024: as medidas da variante <c>full</c>
/// (nulas nos flyers enviados antes).
/// </summary>
internal sealed class FlyerConfiguration : IEntityTypeConfiguration<Flyer>
{
    public void Configure(EntityTypeBuilder<Flyer> builder)
    {
        builder.ToTable("flyers");
        builder.HasKey(flyer => flyer.Id);
        builder.Property(flyer => flyer.Id).HasColumnName("id").HasMaxLength(Flyer.IdLength).IsFixedLength().ValueGeneratedNever();
        builder.Property(flyer => flyer.UploadedBy).HasColumnName("uploaded_by");
        builder.Property(flyer => flyer.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(flyer => flyer.AttachedAtUtc).HasColumnName("attached_at_utc");
        builder.Property(flyer => flyer.FullWidth).HasColumnName("full_width");
        builder.Property(flyer => flyer.FullHeight).HasColumnName("full_height");
        builder.HasIndex(flyer => flyer.CreatedAtUtc)
            .HasDatabaseName("ix_flyers_unattached")
            .HasFilter("attached_at_utc IS NULL");
    }
}
