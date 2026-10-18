using FluentValidation;

namespace Subsolo.Modules.Events.Application.Features.CreateShow;

/// <summary>
/// SPEC-0023 — título 2–100, cidade e estilos existentes (1–5), início local válido e no futuro (no fuso da cidade),
/// casa com perfil ou nome 2–80, line-up 1–15 (perfil ou nome 1–80), preço 1–40, link do ingresso só <c>https</c> e o
/// flyer informado; cada falha leva <c>events.invalid_show</c> no <c>ErrorCode</c>, ligada ao campo.
/// </summary>
internal sealed class CreateShowCommandValidator : AbstractValidator<CreateShowCommand>;
