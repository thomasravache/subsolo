using FluentValidation;

namespace Subsolo.Modules.Profiles.Application.Features.RejectProfileRequest;

/// <summary>
/// SPEC-0021 — o motivo é obrigatório (3–200, sem espaços nas pontas); a falha leva
/// <see cref="ProfilesErrors.InvalidRequest"/> no <c>ErrorCode</c>, ligada ao campo <c>Reason</c>.
/// </summary>
internal sealed class RejectProfileRequestCommandValidator : AbstractValidator<RejectProfileRequestCommand>;
