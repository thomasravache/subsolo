namespace Subsolo.Modules.Notifications.Infrastructure.Deliveries;

/// <summary>
/// SPEC-0030 — o endereço público do site (<c>App:PublicUrl</c>, o mesmo do link mágico, validado pelo Users na
/// inicialização), base do link de descadastro <c>{App:PublicUrl}/avisos/descadastrar?token=…</c> de todo e-mail de aviso.
/// </summary>
public sealed class NoticeLinksOptions
{
    public const string SectionName = "App";

    /// <summary>Caminho da página de descadastro no site.</summary>
    public const string UnsubscribePath = "/avisos/descadastrar";

    public string PublicUrl { get; set; } = string.Empty;

    /// <summary>O link de descadastro com <paramref name="token"/>.</summary>
    public string UnsubscribeLink(string token) =>
        $"{PublicUrl.TrimEnd('/')}{UnsubscribePath}?token={Uri.EscapeDataString(token)}";
}
