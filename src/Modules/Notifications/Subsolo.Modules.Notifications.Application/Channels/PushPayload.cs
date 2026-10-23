using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace Subsolo.Modules.Notifications.Application.Channels;

/// <summary>
/// SPEC-0030 (§6, Emenda de 2026-10-23; UT-04) — o conteúdo do push: o JSON <c>{title, body, url}</c>, nada além do
/// aviso. As letras acentuadas saem como são (UTF-8, 2 bytes cada) e não como <c>\uXXXX</c> (6 bytes cada): com título
/// e corpo no tamanho máximo, só assim o push cifrado cabe nos 4.096 bytes do Web Push.
/// </summary>
internal static class PushPayload
{
    /// <summary>
    /// Não escapa nenhuma faixa Unicode. Seguro aqui porque o único leitor é o service worker (<c>web/public/sw.js</c>),
    /// que faz <c>JSON.parse</c> e passa os textos para <c>showNotification</c> — nunca os insere como HTML. Os
    /// caracteres que o JSON exige (aspas, barra invertida, controle) continuam escapados; <c>&lt;</c>, <c>&gt;</c>,
    /// <c>&amp;</c> e <c>'</c> seguem escapados pelo codificador, que não relaxa esses mesmo com todas as faixas liberadas.
    /// </summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    /// <summary>Serializa o aviso para o push.</summary>
    public static string Serialize(string title, string body, string url) =>
        JsonSerializer.Serialize(new Content(title, body, url), Json);

    private sealed record Content(string Title, string Body, string Url);
}
