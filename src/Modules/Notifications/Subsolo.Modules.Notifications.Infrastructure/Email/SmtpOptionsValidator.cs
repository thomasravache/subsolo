using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Subsolo.Modules.Notifications.Infrastructure.Email;

/// <summary>
/// SPEC-0018 — validação de <see cref="SmtpOptions"/> na inicialização: fora de Development (Production, Staging —
/// Emenda 2), TLS é obrigatório (<see cref="SmtpSecurity.StartTls"/> ou <see cref="SmtpSecurity.SslOnConnect"/>), e a
/// API não sobe com <see cref="SmtpSecurity.None"/>. Em Development, <c>None</c> serve ao Mailpit (local e CI).
/// </summary>
internal sealed class SmtpOptionsValidator(IHostEnvironment environment) : IValidateOptions<SmtpOptions>
{
    public ValidateOptionsResult Validate(string? name, SmtpOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!Enum.IsDefined(options.Security))
        {
            return ValidateOptionsResult.Fail(
                $"{SmtpOptions.SectionName}:Security inválido ({options.Security}): use None, StartTls ou SslOnConnect.");
        }

        return !environment.IsDevelopment() && options.Security == SmtpSecurity.None
            ? ValidateOptionsResult.Fail(
                $"{SmtpOptions.SectionName}:Security=None não é aceito em {environment.EnvironmentName}: fora de Development, o e-mail exige TLS (StartTls ou SslOnConnect).")
            : ValidateOptionsResult.Success;
    }
}
