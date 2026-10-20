namespace Subsolo.Modules.Engagement.Application;

/// <summary>
/// SPEC-0029 (IT-02) — a gravação de um "vou" perdeu a corrida para outro igual (chave primária
/// <c>(user_id, show_id)</c>, 23505). Lançada pelo <see cref="IEngagementUnitOfWork"/> depois de descartar as mudanças;
/// marcar é idempotente, então o handler a trata como sucesso (204), nunca 500.
/// </summary>
public sealed class AttendanceAlreadyExistsException : Exception
{
    public AttendanceAlreadyExistsException()
    {
    }

    public AttendanceAlreadyExistsException(string message)
        : base(message)
    {
    }

    public AttendanceAlreadyExistsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
