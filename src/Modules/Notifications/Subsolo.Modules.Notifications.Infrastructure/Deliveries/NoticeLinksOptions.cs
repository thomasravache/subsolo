namespace Subsolo.Modules.Notifications.Infrastructure.Deliveries;

/// <summary>
/// SPEC-0030 — o endereço público do site (<c>App:PublicUrl</c>, o mesmo do link mágico, validado pelo Users na
/// inicialização), base do link de descadastro <c>{App:PublicUrl}/avisos/descadastrar?token=…</c> de todo e-mail de aviso.
/// SPEC-0031: e do descadastro em um clique (<see cref="OneClickUnsubscribeLink"/>) e das URLs relativas dos avisos
/// (<see cref="Absolute"/>).
/// </summary>
public sealed class NoticeLinksOptions
{
    public const string SectionName = "App";

    /// <summary>Caminho da página de descadastro no site.</summary>
    public const string UnsubscribePath = "/avisos/descadastrar";

    public string PublicUrl { get; set; } = string.Empty;

    /// <summary>SPEC-0031 (H30-04) — rota do descadastro em um clique (RFC 8058), na mesma origem do site.</summary>
    public const string OneClickUnsubscribePath = "/api/v1/notifications/unsubscribe/one-click";

    /// <summary>O link de descadastro com <paramref name="token"/>.</summary>
    public string UnsubscribeLink(string token) =>
        $"{PublicUrl.TrimEnd('/')}{UnsubscribePath}?token={Uri.EscapeDataString(token)}";

    /// <summary>SPEC-0031 (H30-04) — o endereço do <c>List-Unsubscribe</c> (descadastro em um clique) com <paramref name="token"/>.</summary>
    public string OneClickUnsubscribeLink(string token) =>
        $"{PublicUrl.TrimEnd('/')}{OneClickUnsubscribePath}?token={Uri.EscapeDataString(token)}";

    /// <summary>
    /// SPEC-0031 — a URL do aviso para o e-mail: um caminho do site (<c>/curitiba/noite-da-peste-ii</c>) vira absoluto
    /// contra <see cref="PublicUrl"/>; uma URL absoluta fica como está.
    /// </summary>
    public string Absolute(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        return url.StartsWith('/') ? $"{PublicUrl.TrimEnd('/')}{url}" : url;
    }
}
