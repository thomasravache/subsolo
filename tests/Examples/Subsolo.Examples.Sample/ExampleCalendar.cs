namespace Subsolo.Examples.Sample;

/// <summary>SPEC-0005 UT-02 — serviço de exemplo que lê o relógio só pelo <see cref="TimeProvider"/> injetado.</summary>
public sealed class ExampleCalendar(TimeProvider timeProvider)
{
    /// <summary>Data de hoje (UTC) segundo o relógio injetado.</summary>
    public DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
}
