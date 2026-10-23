using System.Net;
using Subsolo.Modules.Notifications.Domain.Notices;

namespace Subsolo.Modules.Notifications.Application.Templates;

/// <summary>
/// SPEC-0030 — e-mail de um aviso (<c>digest</c> | <c>reminder</c>, ADR-0014): o título no assunto, o corpo, o botão para
/// a URL do aviso e, no rodapé, o link de descadastro daquele tipo (ADR-0008), nas partes HTML e texto. HTML no tema do
/// design system como o <see cref="MagicLinkTemplate"/>; todo dado entra escapado.
/// </summary>
internal static class NoticeEmailTemplate
{
    public static EmailContent Render(string kind, string title, string body, string url, string unsubscribeLink)
    {
        var reason = kind == NoticeKinds.Digest
            ? "Você recebe este e-mail porque ligou o resumo diário por e-mail no Subsolo."
            : "Você recebe este e-mail porque ligou o lembrete na véspera por e-mail no Subsolo.";
        return new EmailContent(title, Html(title, body, url, reason, unsubscribeLink), Text(title, body, url, reason, unsubscribeLink));
    }

    private static string Html(string title, string body, string url, string reason, string unsubscribeLink)
    {
        var encodedTitle = WebUtility.HtmlEncode(title);
        var encodedBody = WebUtility.HtmlEncode(body);
        var href = WebUtility.HtmlEncode(url);
        var unsubscribe = WebUtility.HtmlEncode(unsubscribeLink);
        var encodedReason = WebUtility.HtmlEncode(reason);
        return $"""
            <!DOCTYPE html>
            <html lang="pt-BR">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{encodedTitle}</title>
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
            <td style="padding-bottom:12px;font-size:20px;font-weight:bold;line-height:28px;color:#E8E2D4;">{encodedTitle}</td>
            </tr>
            <tr>
            <td style="padding-bottom:24px;font-size:16px;line-height:24px;color:#E8E2D4;white-space:pre-line;">{encodedBody}</td>
            </tr>
            <tr>
            <td style="padding-bottom:32px;">
            <a href="{href}" style="display:inline-block;padding:14px 28px;background-color:#D7263D;color:#FFFFFF;font-size:16px;font-weight:bold;text-decoration:none;border-radius:4px;">Ver no Subsolo</a>
            </td>
            </tr>
            <tr>
            <td style="font-size:12px;line-height:18px;color:#E8E2D4;">{encodedReason} <a href="{unsubscribe}" style="color:#E8E2D4;text-decoration:underline;">Não quero mais receber este e-mail</a>: {unsubscribe}</td>
            </tr>
            </table>
            </td>
            </tr>
            </table>
            </body>
            </html>
            """;
    }

    private static string Text(string title, string body, string url, string reason, string unsubscribeLink) => $"""
        SUBSOLO

        {title}

        {body}

        Ver no Subsolo: {url}

        {reason}
        Para não receber mais este e-mail: {unsubscribeLink}
        """;
}
