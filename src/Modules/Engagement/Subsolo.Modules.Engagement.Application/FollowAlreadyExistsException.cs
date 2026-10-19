namespace Subsolo.Modules.Engagement.Application;

/// <summary>
/// SPEC-0027 (IT-02) — a gravação de um follow perdeu a corrida para outro igual (chave primária
/// <c>(user_id, target_kind, target_key)</c>, 23505). Lançada pelo <see cref="IEngagementUnitOfWork"/> depois de descartar
/// as mudanças; seguir é idempotente, então o handler a trata como sucesso (204), nunca 500.
/// </summary>
public sealed class FollowAlreadyExistsException : Exception
{
    public FollowAlreadyExistsException()
    {
    }

    public FollowAlreadyExistsException(string message)
        : base(message)
    {
    }

    public FollowAlreadyExistsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
