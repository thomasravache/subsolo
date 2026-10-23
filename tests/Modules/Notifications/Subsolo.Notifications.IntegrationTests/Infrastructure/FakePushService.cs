using System.Collections.Concurrent;
using System.Net;

namespace Subsolo.Notifications.IntegrationTests.Infrastructure;

/// <summary>
/// SPEC-0030 — servidor de push falso, em processo: é o handler primário do <c>HttpClient</c> do canal Web Push (ver
/// <see cref="NotificationsApiFactory"/>), então toda chamada a <c>https://fcm.googleapis.com/…</c> (ou Mozilla, Apple)
/// chega aqui e nunca à rede. Registra cada chamada (endpoint, cabeçalhos, corpo, quando) e responde 201, ou o status
/// configurado para o endpoint (<see cref="RespondWith"/>: 410 para uma inscrição morta, 500 para uma falha temporária).
/// </summary>
internal sealed class FakePushService : HttpMessageHandler
{
    private readonly ConcurrentQueue<PushCall> _calls = new();
    private readonly ConcurrentDictionary<string, HttpStatusCode> _statuses = new(StringComparer.Ordinal);

    /// <summary>Todas as chamadas recebidas, na ordem de chegada.</summary>
    public IReadOnlyList<PushCall> Calls => [.. _calls];

    /// <summary>Faz o endpoint <paramref name="endpoint"/> responder <paramref name="status"/> daqui em diante.</summary>
    public void RespondWith(string endpoint, HttpStatusCode status) => _statuses[endpoint] = status;

    /// <summary>As chamadas a <paramref name="endpoint"/>.</summary>
    public IReadOnlyList<PushCall> CallsTo(string endpoint) =>
        [.. _calls.Where(call => string.Equals(call.Endpoint, endpoint, StringComparison.Ordinal))];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        var headers = request.Headers
            .Concat(request.Content?.Headers.AsEnumerable() ?? [])
            .ToDictionary(header => header.Key, header => string.Join(", ", header.Value), StringComparer.OrdinalIgnoreCase);
        var endpoint = request.RequestUri?.AbsoluteUri ?? string.Empty;
        var status = _statuses.TryGetValue(endpoint, out var configured) ? configured : HttpStatusCode.Created;
        _calls.Enqueue(new PushCall(endpoint, request.Method.Method, headers, body, DateTimeOffset.UtcNow, status));
        return new HttpResponseMessage(status) { RequestMessage = request, Content = new ByteArrayContent([]) };
    }

    // O HttpClientFactory pode descartar o handler vencido: o HttpMessageHandler base não guarda estado de descarte, então
    // o servidor falso continua respondendo (e registrando) durante todo o teste.
}

/// <summary>Uma chamada ao serviço de push: endpoint, método, cabeçalhos (sem diferenciar caixa), corpo, quando e a resposta dada.</summary>
internal sealed record PushCall(
    string Endpoint,
    string Method,
    IReadOnlyDictionary<string, string> Headers,
    byte[] Body,
    DateTimeOffset At,
    HttpStatusCode Status)
{
    public string? Header(string name) => Headers.TryGetValue(name, out var value) ? value : null;
}
