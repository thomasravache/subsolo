using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Subsolo.Modules.Users.Application;

namespace Subsolo.Modules.Users.Infrastructure;

/// <summary>
/// SPEC-0016 (Emenda 4, §7.6 j) — validação de <c>App:PublicUrl</c> na inicialização: a base do link mágico precisa ser
/// uma URL absoluta <c>https</c> (<c>http</c> só em Development, onde o <c>aspire run</c> serve o web sem TLS). Vazia,
/// relativa ou <c>http</c> fora de Development impede a API de subir, em vez de mandar links quebrados.
/// </summary>
internal sealed class PublicUrlOptionsValidator(IHostEnvironment environment) : IValidateOptions<PublicUrlOptions>
{
    private const string Key = PublicUrlOptions.SectionName + ":" + nameof(PublicUrlOptions.PublicUrl);

    public ValidateOptionsResult Validate(string? name, PublicUrlOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!Uri.TryCreate(options.PublicUrl, UriKind.Absolute, out var url)
            || url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp)
        {
            return ValidateOptionsResult.Fail(
                $"{Key} inválido (\"{options.PublicUrl}\"): informe a URL absoluta do site (ex.: https://subsolo.app), base do link mágico.");
        }

        return url.Scheme == Uri.UriSchemeHttp && !environment.IsDevelopment()
            ? ValidateOptionsResult.Fail(
                $"{Key}=\"{options.PublicUrl}\" não é aceito em {environment.EnvironmentName}: fora de Development, o link mágico exige https.")
            : ValidateOptionsResult.Success;
    }
}
