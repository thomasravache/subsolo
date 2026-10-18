using FluentValidation;
using Subsolo.Modules.Profiles.Domain.Requests;

namespace Subsolo.Modules.Profiles.Application.Features.RejectProfileRequest;

/// <summary>
/// SPEC-0021 — o motivo é obrigatório (3–200, sem espaços nas pontas); a falha leva
/// <see cref="ProfilesErrors.InvalidRequest"/> no <c>ErrorCode</c>, ligada ao campo <c>Reason</c>.
/// </summary>
internal sealed class RejectProfileRequestCommandValidator : AbstractValidator<RejectProfileRequestCommand>
{
    public RejectProfileRequestCommandValidator() =>
        RuleFor(command => command.Reason)
            .Must(reason => reason?.Trim() is { Length: >= ProfileRequest.TextMinLength and <= ProfileRequest.ContactMaxLength })
            .WithMessage($"Informe o motivo da recusa, de {ProfileRequest.TextMinLength} a {ProfileRequest.ContactMaxLength} caracteres.")
            .WithErrorCode(ProfilesErrors.InvalidRequest);
}
