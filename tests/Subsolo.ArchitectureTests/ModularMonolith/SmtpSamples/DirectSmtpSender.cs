using MailKit.Net.Smtp;

namespace Subsolo.ArchitectureTests.ModularMonolith.SmtpSamples.Mailer.Infrastructure;

/// <summary>
/// SPEC-0018 IT-05 — dublê: um módulo fora do Notifications que fala SMTP direto com o MailKit (viola o ADR-0014).
/// Só é avaliado pelo teste da violação, nunca pelas regras dos módulos reais.
/// </summary>
public static class DirectSmtpSender
{
    public static bool IsReady(SmtpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client.IsConnected;
    }
}
