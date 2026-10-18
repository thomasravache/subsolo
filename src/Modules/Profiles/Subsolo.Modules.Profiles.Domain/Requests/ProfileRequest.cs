using Subsolo.Common.Domain;
using Subsolo.Modules.Profiles.Domain.Profiles;

namespace Subsolo.Modules.Profiles.Domain.Requests;

/// <summary>
/// SPEC-0021 — pedido de perfil novo (<see cref="ProfileRequestKinds.New"/>, com o <see cref="Draft"/> e o contato) ou
/// reivindicação de perfil sem dono (<see cref="ProfileRequestKinds.Claim"/>, com o <see cref="ProfileId"/> e a prova),
/// decidido pelo admin (aprovado ou recusado com motivo). O contato e a prova (<see cref="ContactOrProof"/>) só servem
/// à aprovação (ADR-0008) e são apagados com o pedido.
/// </summary>
public sealed class ProfileRequest : Entity
{
    /// <summary>Tamanho mínimo do contato, da prova e do motivo da recusa.</summary>
    public const int TextMinLength = 3;

    /// <summary>Tamanho máximo do contato e do motivo da recusa.</summary>
    public const int ContactMaxLength = 200;

    /// <summary>Tamanho máximo da prova de uma reivindicação.</summary>
    public const int ProofMaxLength = 300;

    private ProfileRequest()
    {
    }

    public Guid Id { get; private set; }

    /// <summary><see cref="ProfileRequestKinds"/>.</summary>
    public string Kind { get; private set; } = string.Empty;

    public Guid RequesterUserId { get; private set; }

    /// <summary>O perfil reivindicado (claim) ou o criado na aprovação (new); <see langword="null"/> num pedido novo pendente.</summary>
    public Guid? ProfileId { get; private set; }

    /// <summary>Os dados do perfil pedido (só <see cref="ProfileRequestKinds.New"/>).</summary>
    public ProfileDraft? Draft { get; private set; }

    /// <summary>O contato (pedido novo) ou a prova (reivindicação), visível só ao admin.</summary>
    public string ContactOrProof { get; private set; } = string.Empty;

    /// <summary><see cref="ProfileRequestStatuses"/>.</summary>
    public string Status { get; private set; } = ProfileRequestStatuses.Pending;

    /// <summary>Motivo da recusa.</summary>
    public string? Reason { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? DecidedAtUtc { get; private set; }

    /// <summary>Pedido de perfil novo, pendente.</summary>
    public static ProfileRequest SubmitNew(Guid requesterUserId, ProfileDraft draft, string contact, DateTimeOffset nowUtc) =>
        throw new NotImplementedException("SPEC-0021: ProfileRequest.SubmitNew ainda não implementado.");

    /// <summary>Reivindicação do perfil <paramref name="profileId"/>, pendente.</summary>
    public static ProfileRequest SubmitClaim(Guid requesterUserId, Guid profileId, string proof, DateTimeOffset nowUtc) =>
        throw new NotImplementedException("SPEC-0021: ProfileRequest.SubmitClaim ainda não implementado.");
}
