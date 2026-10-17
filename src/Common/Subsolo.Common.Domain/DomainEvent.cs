namespace Subsolo.Common.Domain;

/// <summary>
/// SPEC-0007 — base dos eventos de domínio: <see cref="Id"/> (Guid v7) e <see cref="OccurredOnUtc"/> preenchidos na
/// criação. Os setters <c>init</c> permitem que a desserialização da outbox restaure os valores originais, para que
/// uma nova tentativa publique o evento de integração com o mesmo Id.
/// SPEC-0013 — ao gravar na outbox, o interceptor substitui <see cref="OccurredOnUtc"/> pelo instante do
/// <see cref="TimeProvider"/> da aplicação; o valor da criação (relógio do sistema) é só o padrão fora desse caminho.
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}
