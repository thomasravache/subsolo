namespace Subsolo.Examples.Sample;

/// <summary>SPEC-0005 UT-02 — serviço de exemplo que lê o relógio só pelo <see cref="TimeProvider"/> injetado.</summary>
public sealed class ExampleCalendar
{
    public ExampleCalendar(TimeProvider timeProvider) => throw new NotImplementedException();

    /// <summary>Data de hoje (UTC) segundo o relógio injetado.</summary>
    public DateOnly Today() => throw new NotImplementedException();
}
