using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Profiles.Application.Features.ApproveProfileRequest;

/// <summary>SPEC-0021 — <c>POST /api/v1/admin/profile-requests/{id}/approve</c>: o admin aprova o pedido.</summary>
public sealed record ApproveProfileRequestCommand(Guid RequestId) : ICommand<ProfileRequestApproved>;

/// <summary>SPEC-0021 — o perfil que ganhou dono com a aprovação (200).</summary>
public sealed record ProfileRequestApproved(Guid ProfileId);
