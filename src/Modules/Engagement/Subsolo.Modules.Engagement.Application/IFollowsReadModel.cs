using Subsolo.Modules.Engagement.Domain.Follows;

namespace Subsolo.Modules.Engagement.Application;

/// <summary>SPEC-0027 — leituras dos follows para a lista do fã, a contagem pública e o export.</summary>
public interface IFollowsReadModel
{
    /// <summary>Os follows de <paramref name="userId"/>, do mais antigo para o mais novo.</summary>
    Task<IReadOnlyList<Follow>> ListOfAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Quantos usuários seguem <paramref name="target"/>.</summary>
    Task<int> CountAsync(FollowTarget target, CancellationToken cancellationToken);
}
