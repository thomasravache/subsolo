namespace Subsolo.Modules.Events.Application;

/// <summary>
/// SPEC-0024 (H23-02) — a gravação esbarrou num flyer que a limpeza apagou depois da conferência: a FK do show para
/// <c>events.flyers</c> (cadastro e edição) ou a linha do flyer que não existe mais (o <c>Renew</c> do reenvio). Lançada
/// pelo <see cref="IEventsUnitOfWork"/>; o cadastro e a edição a convertem em 400 <c>events.flyer_not_found</c>, e o
/// envio processa o arquivo de novo, nunca 500.
/// </summary>
public sealed class MissingFlyerException : Exception
{
    public MissingFlyerException()
    {
    }

    public MissingFlyerException(string message)
        : base(message)
    {
    }

    public MissingFlyerException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
