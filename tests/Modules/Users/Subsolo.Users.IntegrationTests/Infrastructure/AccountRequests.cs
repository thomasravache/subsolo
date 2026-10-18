using System.Diagnostics;
using System.Net.Http.Json;

namespace Subsolo.Users.IntegrationTests.Infrastructure;

/// <summary>
/// SPEC-0019 — chamadas da "Minha conta" (§6): <c>PUT /api/v1/me</c> (nome), <c>GET /api/v1/me/export</c> e
/// <c>DELETE /api/v1/me</c> (exclusão com o e-mail digitado). As mutações levam o <c>X-CSRF</c> do cliente, a menos que
/// <c>withCsrf: false</c>.
/// </summary>
internal static class AccountRequests
{
    public const string MeRoute = "/api/v1/me";

    public const string ExportRoute = "/api/v1/me/export";

    public const string ExportFileName = "subsolo-meus-dados.json";

    public static async Task<HttpResponseMessage> RenameAsync(
        this UsersApiClient client, string? displayName, CancellationToken ct, bool withCsrf = true) =>
        await client.SendAsync(
            HttpMethod.Put,
            MeRoute,
            ct,
            JsonContent.Create(new { displayName }),
            withCsrf ? await client.CsrfTokenAsync(ct) : null);

    public static Task<HttpResponseMessage> ExportAsync(this UsersApiClient client, CancellationToken ct) =>
        client.GetAsync(ExportRoute, ct);

    public static async Task<HttpResponseMessage> DeleteAccountAsync(
        this UsersApiClient client, string? confirmEmail, CancellationToken ct, bool withCsrf = true) =>
        await client.SendAsync(
            HttpMethod.Delete,
            MeRoute,
            ct,
            JsonContent.Create(new { confirmEmail }),
            withCsrf ? await client.CsrfTokenAsync(ct) : null);

    /// <summary>Tempo de <paramref name="call"/> (só a chamada medida, sem a preparação).</summary>
    public static async Task<(HttpResponseMessage Response, TimeSpan Elapsed)> TimedAsync(Func<Task<HttpResponseMessage>> call)
    {
        var watch = Stopwatch.StartNew();
        var response = await call();
        return (response, watch.Elapsed);
    }

    /// <summary>Percentil pelo método do posto mais próximo (nearest-rank).</summary>
    public static TimeSpan Percentile(IReadOnlyCollection<TimeSpan> values, int percent)
    {
        var sorted = values.Order().ToList();
        var rank = (int)Math.Ceiling(percent / 100.0 * sorted.Count);
        return sorted[Math.Max(0, rank - 1)];
    }
}
