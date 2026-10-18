using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Subsolo.Modules.Profiles.Domain.Profiles;
using Subsolo.Modules.Profiles.Domain.Slugs;

namespace Subsolo.Modules.Profiles.Infrastructure.Profiles;

/// <summary>
/// SPEC-0021 — tabela <c>profiles.profiles</c>: tipo, nome, slug (único por tipo), cidade, estilos em <c>text[]</c>,
/// links em <c>jsonb</c>, dono (nulo = sem dono) e criação; índices do dono e da busca de reivindicação.
/// </summary>
internal sealed class ProfileConfiguration : IEntityTypeConfiguration<Profile>
{
    /// <summary>Tamanho máximo do tipo e da cidade (slug do Events).</summary>
    public const int TypeMaxLength = 20;

    public const int CityMaxLength = 60;

    public void Configure(EntityTypeBuilder<Profile> builder)
    {
        builder.ToTable("profiles");
        builder.HasKey(profile => profile.Id);
        builder.Property(profile => profile.Id).HasColumnName("id").ValueGeneratedNever();
        // Emenda 3: token de concorrência otimista (a coluna de sistema xmin do PostgreSQL).
        builder.Property<uint>(ProfilesDbContextVersion.Name).IsRowVersion();
        builder.Property(profile => profile.Type).HasColumnName("type").HasMaxLength(TypeMaxLength);
        builder.Property(profile => profile.Name).HasColumnName("name").HasMaxLength(Profile.NameMaxLength);
        builder.Property(profile => profile.Slug).HasColumnName("slug").HasMaxLength(ProfileSlug.MaxLength);
        builder.Property(profile => profile.City).HasColumnName("city").HasMaxLength(CityMaxLength);
        builder.PrimitiveCollection(profile => profile.Styles).HasColumnName("styles");
        builder.Property(profile => profile.Links)
            .HasColumnName("links")
            .HasColumnType("jsonb")
            .HasConversion(JsonColumn.Converter<ProfileLinks>(), JsonColumn.Comparer<ProfileLinks>());
        builder.Property(profile => profile.OwnerUserId).HasColumnName("owner_user_id");
        builder.Property(profile => profile.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.HasIndex(profile => new { profile.Type, profile.Slug }).IsUnique();
        builder.HasIndex(profile => profile.OwnerUserId);
    }
}
