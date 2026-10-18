using FluentValidation;
using Subsolo.Modules.Events.IntegrationEvents;
using Subsolo.Modules.Profiles.Application.Features.Shared;
using Subsolo.Modules.Profiles.Domain.Requests;

namespace Subsolo.Modules.Profiles.Application.Features.RequestProfile;

/// <summary>
/// SPEC-0021 — tipo conhecido, nome 2–80, cidade e estilos que o <see cref="IEventsCatalog"/> confirma, estilos só em
/// banda (1–5), links só <c>https</c> do domínio oficial de cada um e contato 3–200; cada falha leva
/// <see cref="ProfilesErrors.InvalidRequest"/> no <c>ErrorCode</c>, ligada ao campo.
/// </summary>
internal sealed class RequestProfileCommandValidator : ProfileFieldsValidator<RequestProfileCommand>
{
    public RequestProfileCommandValidator(IEventsCatalog catalog)
        : base(catalog)
    {
        RuleFor(command => command.Contact)
            .Must(contact => contact?.Trim() is { Length: >= ProfileRequest.TextMinLength and <= ProfileRequest.ContactMaxLength })
            .WithMessage($"Informe um contato de {ProfileRequest.TextMinLength} a {ProfileRequest.ContactMaxLength} caracteres.")
            .WithErrorCode(ProfilesErrors.InvalidRequest);
    }
}
