namespace Subsolo.Modules.Moderation.Application;

/// <summary>
/// SPEC-0033 — unidade de trabalho do módulo: grava as mudanças rastreadas (e os eventos de domínio na outbox) numa
/// transação. A denúncia pendente repetida (índice único parcial) e a decisão concorrente (token otimista) viram
/// <see cref="ModerationConflictException"/>, com as mudanças descartadas.
/// </summary>
internal interface IModerationUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>SPEC-0033 — o que a gravação concorrente encontrou.</summary>
public enum ModerationConflict
{
    /// <summary>Outro pedido do mesmo fã gravou a denúncia pendente do show antes.</summary>
    AlreadyReported,

    /// <summary>Outra decisão do admin resolveu as denúncias antes.</summary>
    AlreadyDecided,
}

/// <summary>SPEC-0033 — gravação recusada pelo banco por concorrência (ver <see cref="ModerationConflict"/>).</summary>
public sealed class ModerationConflictException : Exception
{
    public ModerationConflictException()
    {
    }

    public ModerationConflictException(string message)
        : base(message)
    {
    }

    public ModerationConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ModerationConflictException(ModerationConflict conflict, Exception innerException)
        : base($"Gravação concorrente: {conflict}.", innerException) =>
        Conflict = conflict;

    public ModerationConflict Conflict { get; }
}
