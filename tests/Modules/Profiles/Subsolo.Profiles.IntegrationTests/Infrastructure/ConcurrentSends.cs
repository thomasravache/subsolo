using System.Net;

namespace Subsolo.Profiles.IntegrationTests.Infrastructure;

/// <summary>
/// SPEC-0021 (Emenda 3) — mutações mandadas ao mesmo tempo (copiado do <c>ConcurrentCreates</c> da SPEC-0017): cada envio
/// com a própria sessão e o próprio token anti-forgery, liberados juntos por um semáforo; confere que exatamente um teve
/// sucesso e o resto deu 409 com o código esperado, nunca 500.
/// </summary>
internal static class ConcurrentSends
{
    /// <summary>Clientes por corrida (admins aprovando ao mesmo tempo).</summary>
    public const int Clients = 4;

    /// <summary>Rodadas de cada corrida (uma corrida que passa uma vez pode ter tido sorte).</summary>
    public const int Rounds = 5;

    /// <summary>Uma mutação de uma corrida: o cliente, o token já buscado, a rota e o corpo.</summary>
    public sealed record Send(ProfilesApiClient Client, string Token, string Route, object? Body);

    /// <summary><paramref name="count"/> admins já logados, cada um com o token para o <c>X-CSRF</c>.</summary>
    public static async Task<List<(ProfilesApiClient Client, string Token)>> AdminsAsync(
        ProfilesApiFactory api, CancellationToken ct, int count = Clients)
    {
        var admins = new List<(ProfilesApiClient, string)>();
        for (var i = 0; i < count; i++)
        {
            var client = api.CreateProfilesClient();
            await client.SignInAsync(ProfilesApiFactory.AdminEmail, ct);
            admins.Add((client, await client.CsrfTokenAsync(ct)));
        }

        return admins;
    }

    /// <summary>
    /// Manda todos os <paramref name="sends"/> ao mesmo tempo e confere: nenhum 500, exatamente um
    /// <paramref name="success"/> e o resto 409 com um dos <paramref name="codes"/>. Devolve o índice do vencedor.
    /// </summary>
    public static async Task<int> ShouldSucceedOnceAsync(
        IReadOnlyList<Send> sends,
        HttpStatusCode success,
        CancellationToken ct,
        params string[] codes)
    {
        using var start = new SemaphoreSlim(0, sends.Count);
        var tasks = sends.Select(async send =>
        {
            await start.WaitAsync(ct);
            return await send.Client.SendAsync(HttpMethod.Post, send.Route, ct, send.Body, send.Token);
        }).ToList();
        start.Release(sends.Count);
        var responses = await Task.WhenAll(tasks);
        try
        {
            var statuses = responses.Select(response => response.StatusCode).ToList();
            var description = string.Join(", ", statuses.Select(status => (int)status));
            statuses.ShouldNotContain(HttpStatusCode.InternalServerError, $"envios concorrentes deram 500: {description}");
            statuses.Count(status => status == success).ShouldBe(1, $"envios concorrentes ({(int)success} esperado uma vez): {description}");
            foreach (var response in responses.Where(response => response.StatusCode != success))
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                response.StatusCode.ShouldBe(HttpStatusCode.Conflict, $"{description}: {ProfilesApiClient.Excerpt(body)}");
                codes.ShouldContain(code => body.Contains($"\"{code}\"", StringComparison.Ordinal), $"409 sem um de {string.Join(", ", codes)}: {body}");
            }

            return statuses.IndexOf(success);
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
