using FluentValidation;
using Subsolo.Modules.Site.Domain.Supporters;

namespace Subsolo.Modules.Site.Application.Features.AddSupporter;

/// <summary>
/// SPEC-0028 — nome com 2 a 60 caracteres (sem contar os espaços das pontas) e link nulo ou <c>https</c> absoluto →
/// senão <see cref="SiteErrors.InvalidSupporter"/> (no <c>ErrorCode</c> da falha).
/// </summary>
internal sealed class AddSupporterCommandValidator : AbstractValidator<AddSupporterCommand>
{
    private const int NameMinLength = Supporter.NameMinLength;

    private const int NameMaxLength = Supporter.NameMaxLength;

    public AddSupporterCommandValidator()
    {
        RuleFor(command => command.Name)
            .Must(name => name?.Trim().Length is >= NameMinLength and <= NameMaxLength)
            .WithErrorCode(SiteErrors.InvalidSupporter)
            .WithMessage($"O nome deve ter de {NameMinLength} a {NameMaxLength} caracteres.");

        RuleFor(command => command.Url)
            .Must(url => url is null || IsHttps(url))
            .WithErrorCode(SiteErrors.InvalidSupporter)
            .WithMessage("O link deve começar com https://.");
    }

    /// <summary>Endereço absoluto <c>https</c> com host, até <see cref="Supporter.UrlMaxLength"/> caracteres.</summary>
    public static bool IsHttps(string url) =>
        url.Length <= Supporter.UrlMaxLength
        && Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && !string.IsNullOrEmpty(uri.Host);
}
