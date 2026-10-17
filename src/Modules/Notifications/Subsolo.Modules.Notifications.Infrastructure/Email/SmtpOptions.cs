namespace Subsolo.Modules.Notifications.Infrastructure.Email;

/// <summary>
/// SPEC-0018 — configuração do envio por SMTP (seção <see cref="SectionName"/>). No local e no CI, Host e Port vêm da
/// referência <c>smtp</c> do Mailpit no AppHost (<see cref="SmtpSecurity.None"/>); em Production, TLS é obrigatório e
/// a senha vem de segredo (ADR-0008).
/// </summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Notifications:Smtp";

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; }

    public SmtpSecurity Security { get; set; } = SmtpSecurity.StartTls;

    public string? User { get; set; }

    public string? Password { get; set; }

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "Subsolo";
}

/// <summary>SPEC-0018 — segurança da conexão SMTP.</summary>
public enum SmtpSecurity
{
    None,
    StartTls,
    SslOnConnect,
}
