using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Subsolo.Modules.Notifications.Application.Features.Enqueue;

namespace Subsolo.Modules.Notifications.Infrastructure.WebPush;

/// <summary>
/// SPEC-0030 — o par de chaves VAPID em uso: o de <c>Notifications:WebPush</c> (validado na inicialização por
/// <see cref="WebPushOptionsValidator"/>); sem ele, em Development um par gerado para o processo (as inscrições valem até
/// reiniciar a API) e fora de Development nenhum: o canal Web Push fica desligado (a chave pública responde 503 e as
/// entregas push falham). A chave privada nunca sai deste tipo (ADR-0008).
/// </summary>
internal sealed class WebPushKeys : IWebPushKeys
{
    /// <summary><c>sub</c> do JWT quando não configurado (só em Development).</summary>
    public const string DevelopmentSubject = "mailto:nao-responda@subsolo.local";

    public WebPushKeys(IOptions<WebPushOptions> options, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);
        var configured = options.Value;
        if (!string.IsNullOrEmpty(configured.PublicKey) && !string.IsNullOrEmpty(configured.PrivateKey))
        {
            PublicKey = configured.PublicKey;
            PrivateKey = configured.PrivateKey;
            Subject = string.IsNullOrEmpty(configured.Subject) ? DevelopmentSubject : configured.Subject;
        }
        else if (environment.IsDevelopment())
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var parameters = key.ExportParameters(includePrivateParameters: true);
            PublicKey = Base64Url.EncodeToString([0x04, .. parameters.Q.X!, .. parameters.Q.Y!]);
            PrivateKey = Base64Url.EncodeToString(parameters.D);
            Subject = string.IsNullOrEmpty(configured.Subject) ? DevelopmentSubject : configured.Subject;
        }
    }

    /// <summary>Chave pública não comprimida (65 bytes) em base64url, ou <see langword="null"/> com o canal desligado.</summary>
    public string? PublicKey { get; }

    /// <summary>Chave privada (escalar de 32 bytes) em base64url.</summary>
    public string? PrivateKey { get; }

    /// <summary>Contato do servidor no JWT (<c>mailto:</c> ou <c>https:</c>).</summary>
    public string? Subject { get; }
}
