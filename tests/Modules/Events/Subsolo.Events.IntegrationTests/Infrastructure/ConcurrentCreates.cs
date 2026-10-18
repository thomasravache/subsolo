using System.Net;

namespace Subsolo.Events.IntegrationTests.Infrastructure;

/// <summary>
/// SPEC-0017 (Emenda 2) — o mesmo POST mandado ao mesmo tempo por vários admins (cada um com a própria sessão e o próprio
/// token anti-forgery), em várias rodadas: em cada rodada, exatamente um 201 e o resto 409 com o código esperado, nunca 500.
/// </summary>
internal static class ConcurrentCreates
{
    public const int Clients = 4;

    public const int Rounds = 5;

    /// <summary>Admins já logados, cada um com o token para o <c>X-CSRF</c>.</summary>
    public static async Task<List<(AdminApiClient Client, string Token)>> AdminsAsync(AdminApiFactory api, CancellationToken ct)
    {
        var admins = new List<(AdminApiClient, string)>();
        for (var i = 0; i < Clients; i++)
        {
            var client = api.CreateAdminClient();
            await client.SignInAsync(AdminApiFactory.AdminEmail, ct);
            admins.Add((client, await client.CsrfTokenAsync(ct)));
        }

        return admins;
    }

    /// <summary>Manda <paramref name="body"/> de todos os admins ao mesmo tempo e confere um 201 e o resto 409 <paramref name="code"/>.</summary>
    public static async Task ShouldCreateOnceAsync(
        List<(AdminApiClient Client, string Token)> admins,
        string route,
        object body,
        string code,
        CancellationToken ct)
    {
        using var start = new SemaphoreSlim(0, admins.Count);
        var sends = admins.Select(async admin =>
        {
            await start.WaitAsync(ct);
            return await admin.Client.SendAsync(HttpMethod.Post, route, ct, body, admin.Token);
        }).ToList();
        start.Release(admins.Count);
        var responses = await Task.WhenAll(sends);
        try
        {
            var statuses = responses.Select(response => response.StatusCode).ToList();
            var description = string.Join(", ", statuses.Select(status => (int)status));
            statuses.ShouldNotContain(HttpStatusCode.InternalServerError, $"POST {route} concorrente deu 500: {description}");
            statuses.Count(status => status == HttpStatusCode.Created).ShouldBe(1, $"POST {route} concorrente: {description}");
            foreach (var response in responses.Where(response => response.StatusCode != HttpStatusCode.Created))
            {
                await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, code, ct);
            }
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }
}
