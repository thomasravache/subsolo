using System.Diagnostics;

namespace Subsolo.Engagement.IntegrationTests.Infrastructure;

/// <summary>
/// SPEC-0031 — relógio dos testes de avisos que anda com o tempo real a partir de um instante ajustável: o
/// <see cref="TimeProvider"/> do contêiner também move o Quartz (outbox, inbox e jobs), então um relógio parado
/// (<c>FakeTimeProvider</c>) deixa esses jobs sem disparar. <see cref="SetUtcNow"/> e <see cref="Advance"/> mudam o
/// instante e o relógio continua andando dali.
/// </summary>
internal sealed class RunningClock(DateTimeOffset start) : TimeProvider
{
    private readonly object _gate = new();
    private DateTimeOffset _base = start;
    private long _baseTimestamp = Stopwatch.GetTimestamp();

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return _base + Stopwatch.GetElapsedTime(_baseTimestamp);
        }
    }

    /// <summary>Passa a contar a partir de <paramref name="value"/>.</summary>
    public void SetUtcNow(DateTimeOffset value)
    {
        lock (_gate)
        {
            _base = value;
            _baseTimestamp = Stopwatch.GetTimestamp();
        }
    }

    /// <summary>Pula <paramref name="delta"/> para a frente.</summary>
    public void Advance(TimeSpan delta) => SetUtcNow(GetUtcNow() + delta);
}
