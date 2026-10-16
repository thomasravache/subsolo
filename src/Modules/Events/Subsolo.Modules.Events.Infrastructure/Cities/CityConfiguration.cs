using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Subsolo.Modules.Events.Domain.Cities;

namespace Subsolo.Modules.Events.Infrastructure.Cities;

/// <summary>SPEC-0008 — tabela <c>events.cities</c>, com o slug único.</summary>
internal sealed class CityConfiguration : IEntityTypeConfiguration<City>
{
    public void Configure(EntityTypeBuilder<City> builder)
    {
        builder.ToTable("cities");
        builder.HasKey(city => city.Id);
        builder.Property(city => city.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(city => city.Slug).HasColumnName("slug").HasMaxLength(60);
        builder.Property(city => city.Name).HasColumnName("name").HasMaxLength(120);
        builder.Property(city => city.State).HasColumnName("state").HasMaxLength(2).IsFixedLength();
        builder.Property(city => city.TimeZone).HasColumnName("time_zone").HasMaxLength(64);
        builder.HasIndex(city => city.Slug).IsUnique();
    }
}
