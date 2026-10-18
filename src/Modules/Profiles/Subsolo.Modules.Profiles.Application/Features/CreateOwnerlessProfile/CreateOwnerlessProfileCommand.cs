using Subsolo.Common.Application.Messaging;
using Subsolo.Modules.Profiles.Application.Features.RequestProfile;
using Subsolo.Modules.Profiles.Application.Features.Shared;

namespace Subsolo.Modules.Profiles.Application.Features.CreateOwnerlessProfile;

/// <summary>
/// SPEC-0021 — <c>POST /api/v1/admin/profiles</c>: o admin cria um perfil aprovado sem dono (os mesmos campos do pedido,
/// sem o contato), para cadastrar shows enquanto a banda, a casa ou o produtor ainda não está no Subsolo.
/// </summary>
public sealed record CreateOwnerlessProfileCommand(
    string? Type,
    string? Name,
    string? City,
    IReadOnlyList<string>? Styles,
    ProfileLinksInput? Links) : ICommand<OwnerlessProfileCreated>, IProfileFields;

/// <summary>SPEC-0021 — o perfil criado (201), aprovado e sem dono.</summary>
public sealed record OwnerlessProfileCreated(Guid ProfileId);
