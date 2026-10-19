using Subsolo.Modules.Engagement.Domain.Follows;

namespace Subsolo.Modules.Engagement.Application;

/// <summary>SPEC-0027 — remoções direto no banco: deixar de seguir e a exclusão da conta.</summary>
public interface IFollowsMaintenance
{
    /// <summary>Apaga o follow de <paramref name="userId"/> para <paramref name="target"/> (se houver); quantos apagou.</summary>
    Task<int> DeleteAsync(Guid userId, FollowTarget target, CancellationToken cancellationToken);

    /// <summary>Apaga todos os follows de <paramref name="userId"/>; quantos apagou.</summary>
    Task<int> DeleteAllOfAsync(Guid userId, CancellationToken cancellationToken);
}
