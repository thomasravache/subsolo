namespace Subsolo.Modules.Engagement.Application;

/// <summary>SPEC-0029 — remoções de "vou" direto no banco: desmarcar e a exclusão da conta.</summary>
public interface IAttendancesMaintenance
{
    /// <summary>Apaga o "vou" de <paramref name="userId"/> em <paramref name="showId"/> (se houver); quantos apagou.</summary>
    Task<int> DeleteAsync(Guid userId, Guid showId, CancellationToken cancellationToken);

    /// <summary>Apaga todos os "vou" de <paramref name="userId"/>; quantos apagou.</summary>
    Task<int> DeleteAllOfAsync(Guid userId, CancellationToken cancellationToken);
}
