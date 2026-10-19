namespace Subsolo.Modules.Engagement.Domain.Follows;

/// <summary>
/// SPEC-0027 — follows no schema <c>engagement</c> (escrita: seguir; leitura: quem segue os alvos, para os avisos).
/// Os testes unitários têm um dublê desta interface: leituras de lista, contagem, remoção e export ficam em outras
/// interfaces (read model / manutenção), não aqui.
/// </summary>
public interface IFollowRepository
{
    /// <summary><paramref name="userId"/> já segue <paramref name="target"/>.</summary>
    Task<bool> ExistsAsync(Guid userId, FollowTarget target, CancellationToken cancellationToken);

    /// <summary>Inclui um follow novo (gravado no próximo <c>SaveChangesAsync</c>).</summary>
    void Add(Follow follow);

    /// <summary>
    /// Os ids dos usuários de cada follow de algum dos <paramref name="targets"/> (um por follow: quem segue dois dos
    /// alvos aparece duas vezes).
    /// </summary>
    Task<IReadOnlyList<Guid>> GetFollowerIdsAsync(IReadOnlyCollection<FollowTarget> targets, CancellationToken cancellationToken);
}
