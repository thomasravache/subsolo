namespace Subsolo.Modules.Engagement.Domain.Attendances;

/// <summary>
/// SPEC-0029 — os "vou" no schema <c>engagement</c> (escrita: marcar; leitura: contagens e quem vai, para a interface
/// pública). Os testes unitários têm um dublê desta interface.
/// </summary>
public interface IAttendanceRepository
{
    /// <summary><paramref name="userId"/> já marcou "vou" em <paramref name="showId"/>.</summary>
    Task<bool> ExistsAsync(Guid userId, Guid showId, CancellationToken cancellationToken);

    /// <summary>Inclui um "vou" novo (gravado no próximo <c>SaveChangesAsync</c>).</summary>
    void Add(Attendance attendance);

    /// <summary>Quantos "vou" cada show de <paramref name="showIds"/> tem (shows sem nenhum podem ficar de fora).</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountAsync(IReadOnlyCollection<Guid> showIds, CancellationToken cancellationToken);

    /// <summary>Os ids dos usuários que marcaram "vou" em <paramref name="showId"/>.</summary>
    Task<IReadOnlyList<Guid>> GetAttendeeIdsAsync(Guid showId, CancellationToken cancellationToken);
}
