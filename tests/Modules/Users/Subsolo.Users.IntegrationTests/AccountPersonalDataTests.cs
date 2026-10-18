using System.Diagnostics;
using System.Net;
using Subsolo.IntegrationTests.Common;
using Subsolo.Users.IntegrationTests.Infrastructure;

namespace Subsolo.Users.IntegrationTests;

/// <summary>
/// SPEC-0019 IT-06 (ADR-0008) — com os logs e spans exportados capturados em memória durante mudar o nome, exportar e
/// excluir a conta, o e-mail do usuário nunca aparece em claro (só mascarado, se aparecer).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class AccountPersonalDataTests(PostgresFixture postgres)
{
    private const string Email = "fa.sigiloso.conta@subsolo.test";

    /// <summary>Prazo para o span do servidor terminar e chegar ao exportador depois da resposta.</summary>
    private static readonly TimeSpan SpanTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Spec", "SPEC-0019:IT-06")]
    [Trait("Kind", "Integration")]
    public async Task RenameExportAndDeleteDoNotExportTheEmailInPlainText()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await UsersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct);
        await using var api = new UsersApiFactory(database);
        using var client = api.CreateUsersClient();
        await client.SignInAsync(Email, ct);

        using (var rename = await client.RenameAsync("Caio Grind", ct))
        {
            rename.StatusCode.ShouldBe(HttpStatusCode.OK, UsersApiClient.Excerpt(await rename.Content.ReadAsStringAsync(ct)));
        }

        using (var export = await client.ExportAsync(ct))
        {
            var body = await export.Content.ReadAsStringAsync(ct);
            export.StatusCode.ShouldBe(HttpStatusCode.OK, UsersApiClient.Excerpt(body));
            body.ShouldContain(Email, Case.Insensitive, "o export devolve o e-mail ao próprio usuário");
        }

        using (var mismatch = await client.DeleteAccountAsync("outro.email@subsolo.test", ct))
        {
            mismatch.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        using (var delete = await client.DeleteAccountAsync($" {Email.ToUpperInvariant()} ", ct))
        {
            delete.StatusCode.ShouldBe(HttpStatusCode.NoContent, UsersApiClient.Excerpt(await delete.Content.ReadAsStringAsync(ct)));
        }

        var spans = await SpansWhenAsync(api, span => IsServerSpan(span, "DELETE", AccountRequests.MeRoute), ct);
        spans.ShouldContain(span => IsServerSpan(span, "PUT", AccountRequests.MeRoute), "o span do PUT /me não foi exportado");
        spans.ShouldContain(span => IsServerSpan(span, "GET", AccountRequests.ExportRoute), "o span do export não foi exportado");
        spans.ShouldContain(span => IsServerSpan(span, "DELETE", AccountRequests.MeRoute), "o span do DELETE /me não foi exportado");
        var logs = api.Logs;
        logs.ShouldNotBeEmpty("nenhum log exportado");

        var leaks = logs.SelectMany(log =>
                new[] { log.FormattedMessage, log.Body, log.Exception?.ToString() }
                    .Concat((log.Attributes ?? []).Select(attribute => $"{attribute.Key}={attribute.Value}"))
                    .Select(text => ($"log {log.CategoryName}", text)))
            .Concat(spans.SelectMany(span =>
                new[] { span.DisplayName, span.StatusDescription }
                    .Concat(span.TagObjects.Select(tag => $"{tag.Key}={tag.Value}"))
                    .Concat(span.Events.SelectMany(e => e.Tags.Select(tag => $"{e.Name}:{tag.Key}={tag.Value}")))
                    .Select(text => ($"span {span.DisplayName}", text))))
            .Where(item => item.text?.Contains(Email, StringComparison.OrdinalIgnoreCase) == true)
            .Select(item => $"{item.Item1}: {item.text}")
            .Distinct(StringComparer.Ordinal)
            .ToList();

        leaks.ShouldBeEmpty("e-mail em claro nos sinais exportados");
    }

    private static async Task<IReadOnlyList<Activity>> SpansWhenAsync(
        UsersApiFactory api, Func<Activity, bool> predicate, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        while (true)
        {
            var spans = api.Spans;
            if (spans.Any(predicate) || watch.Elapsed >= SpanTimeout)
            {
                return spans;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), ct);
        }
    }

    private static bool IsServerSpan(Activity span, string method, string route) =>
        span.Kind == ActivityKind.Server
        && span.TagObjects.Any(tag => tag.Key == "http.request.method" && string.Equals(tag.Value?.ToString(), method, StringComparison.Ordinal))
        && span.TagObjects.Any(tag => tag.Value?.ToString() is { } value
            && (value == route || value.EndsWith(route, StringComparison.Ordinal) || value.Contains(route + "?", StringComparison.Ordinal)));
}
