namespace Subsolo.Modules.Events.Application;

/// <summary>
/// SPEC-0022 — a gravação esbarrou no id do flyer (outro envio do mesmo arquivo gravou a linha entre a conferência e a
/// gravação). Lançada pelo <see cref="IEventsUnitOfWork"/>; o upload a converte no 200 do mesmo flyer, nunca 500.
/// </summary>
public sealed class DuplicateFlyerException : Exception
{
    public DuplicateFlyerException()
    {
    }

    public DuplicateFlyerException(string message)
        : base(message)
    {
    }

    public DuplicateFlyerException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
