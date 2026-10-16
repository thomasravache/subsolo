using Subsolo.Examples.ArchitectureSamples.Tidy.Domain;

namespace Subsolo.Examples.ArchitectureSamples.Tidy.Infrastructure;

/// <summary>Registro de persistência do módulo "Tidy": a dependência vai do Infrastructure para o Domain (permitido).</summary>
public sealed class VenueRecord
{
    public string Name { get; init; } = string.Empty;

    public static VenueRecord From(Venue venue) => new() { Name = venue.Name };
}
