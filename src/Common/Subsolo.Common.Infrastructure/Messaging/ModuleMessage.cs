namespace Subsolo.Common.Infrastructure.Messaging;

/// <summary>
/// SPEC-0007 — mensagem guardada na outbox ou na inbox de um módulo: o evento serializado e o estado do
/// processamento (tentativas, próximo horário de tentativa e último erro), usado pelos jobs com retry e backoff.
/// </summary>
internal abstract class ModuleMessage
{
    /// <summary>Teto do intervalo entre tentativas.</summary>
    internal static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(5);

    /// <summary>Tamanho máximo guardado do último erro.</summary>
    private const int MaxErrorLength = 2000;

    public Guid Id { get; init; }

    /// <summary>Nome completo do tipo do evento (resolvido pelo <see cref="MessageTypeRegistry"/> do módulo).</summary>
    public required string Type { get; init; }

    /// <summary>Evento serializado em JSON.</summary>
    public required string Content { get; init; }

    public DateTime OccurredOnUtc { get; init; }

    public DateTime? ProcessedOnUtc { get; private set; }

    public int Attempts { get; private set; }

    public DateTime? NextAttemptOnUtc { get; private set; }

    /// <summary>Tipo e mensagem da última falha (sem stack trace; a exceção completa vai para o log).</summary>
    public string? Error { get; private set; }

    public void MarkProcessed(DateTime nowUtc)
    {
        ProcessedOnUtc = nowUtc;
        NextAttemptOnUtc = null;
    }

    /// <summary>Registra a falha e agenda a próxima tentativa com backoff exponencial (2 s, 4 s, 8 s … até 5 min).</summary>
    public void MarkFailed(Exception exception, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Attempts++;
        var description = $"{exception.GetType().FullName}: {exception.Message}";
        Error = description.Length <= MaxErrorLength ? description : description[..MaxErrorLength];
        NextAttemptOnUtc = nowUtc + Backoff(Attempts);
    }

    /// <summary>Intervalo até a próxima tentativa depois de <paramref name="attempts"/> falhas.</summary>
    internal static TimeSpan Backoff(int attempts)
    {
        var seconds = Math.Pow(2, Math.Clamp(attempts, 1, 30));
        return TimeSpan.FromSeconds(Math.Min(seconds, MaxBackoff.TotalSeconds));
    }
}

/// <summary>Registro de que um handler já processou uma mensagem (idempotência por handler).</summary>
internal abstract class ModuleMessageConsumer
{
    public Guid MessageId { get; init; }

    /// <summary>Nome completo do tipo do handler.</summary>
    public required string Name { get; init; }
}
