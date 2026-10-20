namespace Subsolo.Modules.Engagement.IntegrationEvents;

/// <summary>
/// SPEC-0029 — leitura síncrona dos "vou" por outros módulos (ADR-0001: interface pública em IntegrationEvents): a
/// contagem pública por show e quem marcou "vou" num show (só ids, para o lembrete na véspera). Quem vai nunca é público
/// (ADR-0008): os ids só servem para a entrega do lembrete.
/// </summary>
public interface IAttendanceApi
{
    /// <summary>Quantos marcaram "vou" em cada um dos <paramref name="showIds"/>.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountAsync(IReadOnlyCollection<Guid> showIds, CancellationToken ct);

    /// <summary>Os ids, sem repetição, dos usuários que marcaram "vou" no show <paramref name="showId"/>.</summary>
    Task<IReadOnlyCollection<Guid>> GetAttendeesAsync(Guid showId, CancellationToken ct);
}
