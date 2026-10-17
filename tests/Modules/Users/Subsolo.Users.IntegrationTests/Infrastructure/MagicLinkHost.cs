using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Time.Testing;
using Subsolo.IntegrationTests.Common;

// SPEC-0016 — um Mailpit real (container efêmero) para todo o assembly: os testes do link mágico mandam e-mail de verdade
// pelo módulo Notifications e leem o link pela API HTTP do Mailpit. Cada teste usa e-mails próprios (sem limpar a caixa).
[assembly: AssemblyFixture(typeof(MailpitFixture))]

namespace Subsolo.Users.IntegrationTests.Infrastructure;

/// <summary>
/// SPEC-0016 — o Subsolo.Api em memória (<see cref="UsersApiFactory"/>) ligado ao Mailpit real: SMTP sem TLS no
/// Mailpit, <c>App:PublicUrl</c> = <see cref="PublicUrl"/> (o mesmo endereço do cliente de teste) e limites de taxa
/// altos por padrão (os testes que medem o limite passam os seus).
/// </summary>
internal static class MagicLinkHost
{
    public const string RequestRoute = "/api/v1/auth/magic-link";

    public const string ConsumeRoute = "/api/v1/auth/magic-link/consume";

    /// <summary><c>App:PublicUrl</c> dos testes: o endereço do cliente HTTPS do <see cref="UsersApiFactory"/>.</summary>
    public const string PublicUrl = "https://localhost";

    /// <summary>Começo do link mágico no e-mail.</summary>
    public const string ConfirmPrefix = PublicUrl + "/entrar/confirmar?token=";

    private static readonly Regex LinkToken = new(
        @"https://localhost/entrar/confirmar\?token=(?<token>[A-Za-z0-9_\-.~%]+)",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Configuração do host com o Mailpit de <paramref name="mailpit"/> (ou a porta SMTP <paramref name="smtpPort"/>),
    /// limites <c>auth</c> e <c>default</c> altos e as chaves de <paramref name="overrides"/> por cima.
    /// </summary>
    public static Dictionary<string, string> Settings(
        MailpitFixture mailpit,
        IReadOnlyDictionary<string, string>? overrides = null,
        int? smtpPort = null)
    {
        ArgumentNullException.ThrowIfNull(mailpit);
        var settings = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Notifications:Smtp:Host"] = smtpPort is null ? mailpit.SmtpHost : "127.0.0.1",
            ["Notifications:Smtp:Port"] = (smtpPort ?? mailpit.SmtpPort).ToString(CultureInfo.InvariantCulture),
            ["Notifications:Smtp:Security"] = "None",
            ["App:PublicUrl"] = PublicUrl,
            ["Security:RateLimiting:auth:PermitLimit"] = "10000",
            ["Security:RateLimiting:auth:Window"] = "00:01:00",
            ["Security:RateLimiting:default:PermitLimit"] = "10000",
            ["Security:RateLimiting:default:Window"] = "00:01:00",
        };
        foreach (var (key, value) in overrides ?? new Dictionary<string, string>())
        {
            settings[key] = value;
        }

        return settings;
    }

    /// <summary>A API com um database novo, o Mailpit e, opcionalmente, o relógio falso.</summary>
    public static async Task<(UsersApiFactory Api, string Database)> StartAsync(
        PostgresFixture postgres,
        MailpitFixture mailpit,
        CancellationToken ct,
        FakeTimeProvider? clock = null,
        IReadOnlyDictionary<string, string>? overrides = null,
        int? smtpPort = null)
    {
        ArgumentNullException.ThrowIfNull(postgres);
        var database = await UsersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct);
        var api = new UsersApiFactory(database, clock: clock, settings: Settings(mailpit, overrides, smtpPort));
        return (api, database);
    }

    /// <summary>Um e-mail novo e só deste teste (<c>@subsolo.test</c>).</summary>
    public static string NewEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@subsolo.test";

    /// <summary><c>POST /api/v1/auth/magic-link</c> com <c>{email, returnTo}</c> e, por padrão, o <c>X-CSRF</c>.</summary>
    public static async Task<HttpResponseMessage> RequestMagicLinkAsync(
        this UsersApiClient client,
        string email,
        CancellationToken ct,
        string? returnTo = null,
        bool withCsrf = true)
    {
        ArgumentNullException.ThrowIfNull(client);
        var csrf = withCsrf ? await client.CsrfTokenAsync(ct) : null;
        return await client.SendAsync(
            HttpMethod.Post, RequestRoute, ct, JsonContent.Create(new { email, returnTo }), csrf);
    }

    /// <summary><c>POST /api/v1/auth/magic-link/consume</c> com <c>{token}</c> e, por padrão, o <c>X-CSRF</c>.</summary>
    public static async Task<HttpResponseMessage> ConsumeMagicLinkAsync(
        this UsersApiClient client,
        string token,
        CancellationToken ct,
        bool withCsrf = true)
    {
        ArgumentNullException.ThrowIfNull(client);
        var csrf = withCsrf ? await client.CsrfTokenAsync(ct) : null;
        return await client.SendAsync(HttpMethod.Post, ConsumeRoute, ct, JsonContent.Create(new { token }), csrf);
    }

    /// <summary>Pede o link para <paramref name="email"/>, exige 202 e devolve o token lido do e-mail no Mailpit.</summary>
    public static async Task<string> RequestTokenAsync(
        this UsersApiClient client,
        MailpitFixture mailpit,
        string email,
        CancellationToken ct,
        string? returnTo = null)
    {
        ArgumentNullException.ThrowIfNull(mailpit);
        var before = (await mailpit.GetMessagesAsync(email)).Select(TokenOf).ToHashSet(StringComparer.Ordinal);
        using var response = await client.RequestMagicLinkAsync(email, ct, returnTo);
        await response.ShouldBeAcceptedAsync(ct);
        var messages = await WaitForMessagesAsync(mailpit, email, before.Count + 1, ct);
        messages.Count.ShouldBe(before.Count + 1, $"o e-mail do link para {email} não chegou ao Mailpit");
        return messages.Select(TokenOf).Where(token => !before.Contains(token))
            .ShouldHaveSingleItem("o pedido não gerou um token novo");
    }

    /// <summary>Exige 202 com o corpo do contrato (<c>{}</c>).</summary>
    public static async Task ShouldBeAcceptedAsync(this HttpResponseMessage response, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(response);
        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(
            HttpStatusCode.Accepted,
            $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}: {UsersApiClient.Excerpt(body)}");
        using var document = JsonDocument.Parse(body);
        document.RootElement.ValueKind.ShouldBe(JsonValueKind.Object, body);
        document.RootElement.EnumerateObject().ShouldBeEmpty($"o 202 do pedido de link é {{}}: {body}");
    }

    /// <summary>As mensagens para <paramref name="to"/>, esperando até <paramref name="count"/> chegarem (até <paramref name="timeout"/>, padrão 10 s).</summary>
    public static async Task<IReadOnlyList<MailpitMessage>> WaitForMessagesAsync(
        MailpitFixture mailpit,
        string to,
        int count,
        CancellationToken ct,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(mailpit);
        var watch = Stopwatch.StartNew();
        var messages = await mailpit.GetMessagesAsync(to);
        while (messages.Count < count && watch.Elapsed < (timeout ?? TimeSpan.FromSeconds(10)))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
            messages = await mailpit.GetMessagesAsync(to);
        }

        return messages;
    }

    /// <summary>O token do link mágico no texto do e-mail.</summary>
    public static string TokenOf(MailpitMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var match = LinkToken.Match(message.Text);
        match.Success.ShouldBeTrue($"e-mail sem o link {ConfirmPrefix}…: {UsersApiClient.Excerpt(message.Text)}");
        return Uri.UnescapeDataString(match.Groups["token"].Value);
    }

    /// <summary>Uma porta TCP local sem nenhum serviço (conexão recusada): o SMTP "parado".</summary>
    public static int ClosedPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>Percentil 95 (posto mais próximo).</summary>
    public static TimeSpan P95(IReadOnlyCollection<TimeSpan> durations)
    {
        ArgumentNullException.ThrowIfNull(durations);
        var sorted = durations.Order().ToList();
        return sorted.Count == 0 ? TimeSpan.Zero : sorted[(int)Math.Ceiling(sorted.Count * 0.95) - 1];
    }
}
