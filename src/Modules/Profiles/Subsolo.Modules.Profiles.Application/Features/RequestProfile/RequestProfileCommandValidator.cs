using FluentValidation;
using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Modules.Profiles.Application.Features.RequestProfile;

#pragma warning disable CS9113 // Scaffold do contrato (SPEC-0021): o catálogo é usado pela implementação.

/// <summary>
/// SPEC-0021 — tipo conhecido, nome 2–80, cidade e estilos que o <see cref="IEventsCatalog"/> confirma, estilos só em
/// banda (1–5), links só <c>https</c> do domínio oficial de cada um e contato 3–200; cada falha leva
/// <see cref="ProfilesErrors.InvalidRequest"/> no <c>ErrorCode</c>, ligada ao campo.
/// </summary>
internal sealed class RequestProfileCommandValidator(IEventsCatalog catalog) : AbstractValidator<RequestProfileCommand>;

#pragma warning restore CS9113
