namespace Subsolo.Modules.Events.Application;

/// <summary>
/// SPEC-0017 (Emenda 2) — a gravação esbarrou no slug único (outro pedido gravou o mesmo endereço entre a conferência e
/// a gravação). Lançada pelo <see cref="IEventsUnitOfWork"/>; os handlers a convertem no 409 do contrato
/// (<c>events.city_slug_taken</c> / <c>events.style_taken</c>), nunca 500.
/// </summary>
public sealed class DuplicateSlugException : Exception
{
    public DuplicateSlugException()
    {
    }

    public DuplicateSlugException(string message)
        : base(message)
    {
    }

    public DuplicateSlugException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
