using FluentValidation;
using Subsolo.Modules.Profiles.Domain.Requests;

namespace Subsolo.Modules.Profiles.Application.Features.ClaimProfile;

/// <summary>SPEC-0021 — a prova tem de 3 a 300 caracteres; a falha leva <see cref="ProfilesErrors.InvalidRequest"/>, no campo <c>Proof</c>.</summary>
internal sealed class ClaimProfileCommandValidator : AbstractValidator<ClaimProfileCommand>
{
    public ClaimProfileCommandValidator() =>
        RuleFor(command => command.Proof)
            .Must(proof => proof?.Trim() is { Length: >= ProfileRequest.TextMinLength and <= ProfileRequest.ProofMaxLength })
            .WithMessage($"Conte como provar que o perfil é seu, de {ProfileRequest.TextMinLength} a {ProfileRequest.ProofMaxLength} caracteres.")
            .WithErrorCode(ProfilesErrors.InvalidRequest);
}
