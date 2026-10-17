using System.Diagnostics;
using System.Net;
using Subsolo.IntegrationTests.Common;
using Subsolo.Users.IntegrationTests.Infrastructure;

namespace Subsolo.Users.IntegrationTests;

/// <summary>
/// SPEC-0014 IT-06 (ADR-0008) — com os logs e spans exportados capturados em memória durante a entrada e o
/// <c>/me</c>, nenhum deles contém o e-mail do usuário em claro.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PersonalDataTests(PostgresFixture postgres)
{
    private const string Email = "fa.sigiloso@subsolo.test";

    /// <summary>Prazo para o span do servidor terminar e chegar ao exportador depois da resposta.</summary>
    private static readonly TimeSpan SpanTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Spec", "SPEC-0014:IT-06")]
    [Trait("Kind", "Integration")]
    public async Task SignInAndMeDoNotExportTheEmailInPlainText()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await UsersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct);
        await using var api = new UsersApiFactory(database);
        using var client = api.CreateUsersClient();

        await client.SignInAsync(Email, ct);
        using (var me = await client.GetAsync(MeEndpointTests.MeRoute, ct))
        {
            var body = await me.Content.ReadAsStringAsync(ct);
            me.StatusCode.ShouldBe(HttpStatusCode.OK, UsersApiClient.Excerpt(body));
            body.ShouldContain(Email, Case.Insensitive, "o /me devolve o e-mail ao próprio usuário");
        }

        var spans = await SpansWhenAsync(api, IsServerSpanOf(MeEndpointTests.MeRoute), ct);
        spans.ShouldContain(span => IsServerSpanOf(MeEndpointTests.MeRoute)(span), "o span do /me não foi exportado");
        spans.ShouldContain(span => IsServerSpanOf(UsersTestEndpoints.SignIn)(span), "o span da entrada não foi exportado");
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

    private static Func<Activity, bool> IsServerSpanOf(string route) =>
        span => span.Kind == ActivityKind.Server &&
                span.TagObjects.Any(tag => tag.Value?.ToString()?.Contains(route, StringComparison.Ordinal) == true);
}
