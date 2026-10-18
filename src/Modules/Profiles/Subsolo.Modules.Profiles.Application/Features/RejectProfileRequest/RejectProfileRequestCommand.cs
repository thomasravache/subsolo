using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Profiles.Application.Features.RejectProfileRequest;

/// <summary>
/// SPEC-0021 — <c>POST /api/v1/admin/profile-requests/{id}/reject</c>: o admin recusa o pedido com o motivo (3–200), que
/// o autor vê em "Meus perfis".
/// </summary>
public sealed record RejectProfileRequestCommand(Guid RequestId, string? Reason) : ICommand;
