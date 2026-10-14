using Subsolo.Examples.ArchitectureSamples.Leaky.Infrastructure;

namespace Subsolo.Examples.ArchitectureSamples.Leaky.Domain;

/// <summary>Violação proposital do ADR-0001: o Domain do módulo "Leaky" depende do Infrastructure.</summary>
public sealed class Show
{
    public string Title { get; init; } = string.Empty;

    public ShowRecord ToRecord() => new() { Title = Title };
}
