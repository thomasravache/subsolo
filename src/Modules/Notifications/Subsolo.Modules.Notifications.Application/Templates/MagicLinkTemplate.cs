using System.Globalization;
using System.Net;

namespace Subsolo.Modules.Notifications.Application.Templates;

/// <summary>
/// SPEC-0018 — modelo <c>MagicLink</c> (§6): assunto "Seu link para entrar no Subsolo", HTML em pt-BR no tema do
/// design system (botão de texto com as cores inline, contraste AA) e texto puro. Dados obrigatórios: <c>link</c> (URL
/// absoluta https; http só em Development) e <c>minutes</c> (inteiro de 1 a 60). Todo dado entra no HTML escapado.
/// </summary>
internal static class MagicLinkTemplate
{
    public const string Subject = "Seu link para entrar no Subsolo";

    private const string LinkKey = "link";
    private const string MinutesKey = "minutes";
    private const int MinMinutes = 1;
    private const int MaxMinutes = 60;

    /// <summary>
    /// O conteúdo do e-mail, ou <see langword="null"/> quando falta uma chave ou um valor é inválido para o modelo.
    /// </summary>
    public static EmailContent? Render(IReadOnlyDictionary<string, string>? data, bool allowHttp)
    {
        if (data is null
            || !data.TryGetValue(LinkKey, out var link)
            || !IsAllowedLink(link, allowHttp)
            || !data.TryGetValue(MinutesKey, out var minutesText)
            || !int.TryParse(minutesText, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
            || minutes is < MinMinutes or > MaxMinutes)
        {
            return null;
        }

        var validity = $"O link vale por {minutes.ToString(CultureInfo.InvariantCulture)} {(minutes == 1 ? "minuto" : "minutos")} e só funciona uma vez.";
        return new EmailContent(Subject, Html(link, validity), Text(link, validity));
    }

    private static bool IsAllowedLink(string? link, bool allowHttp) =>
        !string.IsNullOrWhiteSpace(link)
        && Uri.TryCreate(link, UriKind.Absolute, out var uri)
        && !string.IsNullOrEmpty(uri.Host)
        && (uri.Scheme == Uri.UriSchemeHttps || (allowHttp && uri.Scheme == Uri.UriSchemeHttp));

    /// <summary>
    /// HTML do e-mail: tabelas e estilos inline (os clientes de e-mail ignoram folhas de estilo), cores do design
    /// system — fundo #0B0B0C, texto #E8E2D4, botão #D7263D com texto branco (4,96:1).
    /// </summary>
    private static string Html(string link, string validity)
    {
        var href = WebUtility.HtmlEncode(link);
        var text = WebUtility.HtmlEncode(validity);
        return $"""
            <!DOCTYPE html>
            <html lang="pt-BR">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{Subject}</title>
            </head>
            <body style="margin:0;padding:0;background-color:#0B0B0C;color:#E8E2D4;font-family:Arial,Helvetica,sans-serif;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background-color:#0B0B0C;">
            <tr>
            <td align="center" style="padding:32px 16px;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="max-width:480px;">
            <tr>
            <td style="padding-bottom:24px;font-size:24px;font-weight:bold;letter-spacing:4px;color:#E8E2D4;">SUBSOLO</td>
            </tr>
            <tr>
            <td style="padding-bottom:24px;font-size:16px;line-height:24px;color:#E8E2D4;">Use o botão abaixo para entrar no Subsolo.</td>
            </tr>
            <tr>
            <td style="padding-bottom:24px;">
            <a href="{href}" style="display:inline-block;padding:14px 28px;background-color:#D7263D;color:#FFFFFF;font-size:16px;font-weight:bold;text-decoration:none;border-radius:4px;">Entrar no Subsolo</a>
            </td>
            </tr>
            <tr>
            <td style="padding-bottom:16px;font-size:14px;line-height:22px;color:#E8E2D4;">{text}</td>
            </tr>
            <tr>
            <td style="padding-bottom:16px;font-size:14px;line-height:22px;color:#E8E2D4;">Se não foi você, ignore este e-mail.</td>
            </tr>
            <tr>
            <td style="font-size:12px;line-height:18px;color:#E8E2D4;word-break:break-all;">Se o botão não funcionar, copie este endereço no navegador: {href}</td>
            </tr>
            </table>
            </td>
            </tr>
            </table>
            </body>
            </html>
            """;
    }

    private static string Text(string link, string validity) => $"""
        SUBSOLO

        Use o link abaixo para entrar no Subsolo:
        {link}

        {validity}

        Se não foi você, ignore este e-mail.
        """;
}

/// <summary>SPEC-0018 — e-mail renderizado por um modelo: assunto, HTML e texto puro.</summary>
internal sealed record EmailContent(string Subject, string HtmlBody, string TextBody);
