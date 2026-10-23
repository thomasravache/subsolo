namespace Subsolo.Modules.Notifications.Infrastructure.WebPush;

/// <summary>
/// SPEC-0030 — configuração do canal Web Push (seção <see cref="SectionName"/>, ADR-0014): o par de chaves VAPID
/// (P-256, em base64url: a pública não comprimida de 65 bytes e a privada de 32 bytes) e o <c>sub</c> do JWT
/// (<c>mailto:</c> ou <c>https:</c>). A chave privada vem só de segredo (ADR-0008); as de desenvolvimento são geradas.
/// </summary>
public sealed class WebPushOptions
{
    public const string SectionName = "Notifications:WebPush";

    /// <summary>
    /// Nome do <c>HttpClient</c> (IHttpClientFactory) por onde sai toda chamada ao serviço de push: os testes trocam o
    /// handler dele pelo servidor de push falso.
    /// </summary>
    public const string HttpClientName = "Subsolo.Notifications.WebPush";

    public string PublicKey { get; set; } = string.Empty;

    public string PrivateKey { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;
}
