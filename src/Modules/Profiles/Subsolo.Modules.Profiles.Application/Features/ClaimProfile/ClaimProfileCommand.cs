using Subsolo.Common.Application.Messaging;
using Subsolo.Modules.Profiles.Application.Features.RequestProfile;

namespace Subsolo.Modules.Profiles.Application.Features.ClaimProfile;

/// <summary>
/// SPEC-0021 — <c>POST /api/v1/profiles/{id}/claims</c>: o fã <paramref name="UserId"/> reivindica o perfil aprovado sem
/// dono <paramref name="ProfileId"/>, com a prova (3–300) de que é o dono, vista só pelo admin.
/// </summary>
public sealed record ClaimProfileCommand(Guid UserId, Guid ProfileId, string? Proof) : ICommand<ProfileRequestCreated>;
