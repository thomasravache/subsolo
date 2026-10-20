using FluentValidation;

namespace Subsolo.Modules.Site.Application.Features.AddSupporter;

/// <summary>
/// SPEC-0028 — nome com 2 a 60 caracteres e link nulo ou <c>https</c> → senão <see cref="SiteErrors.InvalidSupporter"/>
/// (no <c>ErrorCode</c> da falha). Scaffold do contrato: as regras entram na implementação.
/// </summary>
internal sealed class AddSupporterCommandValidator : AbstractValidator<AddSupporterCommand>
{
}
