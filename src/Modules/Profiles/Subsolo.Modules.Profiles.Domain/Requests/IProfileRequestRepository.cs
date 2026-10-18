namespace Subsolo.Modules.Profiles.Domain.Requests;

/// <summary>SPEC-0021 — pedidos de perfil e reivindicações no schema <c>profiles</c>.</summary>
public interface IProfileRequestRepository
{
    /// <summary>O pedido <paramref name="id"/>, ou <see langword="null"/>.</summary>
    Task<ProfileRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary><paramref name="requesterUserId"/> tem pedido (novo ou reivindicação) em análise.</summary>
    Task<bool> HasPendingAsync(Guid requesterUserId, CancellationToken cancellationToken);

    /// <summary>Inclui um pedido novo (gravado no próximo <c>SaveChangesAsync</c>).</summary>
    void Add(ProfileRequest request);
}
