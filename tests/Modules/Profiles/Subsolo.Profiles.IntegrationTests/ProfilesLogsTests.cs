using System.Diagnostics;
using Subsolo.IntegrationTests.Common;
using Subsolo.Profiles.IntegrationTests.Infrastructure;

namespace Subsolo.Profiles.IntegrationTests;

/// <summary>
/// SPEC-0021 IT-06 (ADR-0008) — com os logs e spans exportados capturados em memória durante pedido, reivindicação,
/// aprovação e recusa (e a fila do admin, que devolve o contato e o e-mail), o e-mail de quem pediu e o contato/prova
/// nunca aparecem em claro nos sinais.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ProfilesLogsTests(PostgresFixture postgres)
{
    private const string RequesterEmail = "fa.sigiloso.perfil@subsolo.test";
    private const string ClaimerEmail = "fa.sigiloso.reivindica@subsolo.test";
    private const string Contact = "contato-sigiloso-zap-41-98888-7777";
    private const string Proof = "prova-sigilosa-sou-o-dono-do-porao";
    private const string SecondContact = "contato-sigiloso-segundo-pedido";

    /// <summary>Prazo para os spans do servidor terminarem e chegarem ao exportador depois da resposta.</summary>
    private static readonly TimeSpan SpanTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-06")]
    [Trait("Kind", "Integration")]
    public async Task RequestClaimApproveAndRejectDoNotExportTheEmailNorTheContact()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new ProfilesApiFactory(await ProfilesApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        var (admin, _) = await api.SignedInAsync(ProfilesApiFactory.AdminEmail, ct);
        var (requester, _) = await api.SignedInAsync(RequesterEmail, ct);
        var (claimer, _) = await api.SignedInAsync(ClaimerEmail, ct);
        using var _ = admin;
        using var __ = requester;
        using var ___ = claimer;

        var ownerless = await admin.CreateOwnerlessAsync(ProfilesRequests.Ownerless("Porão do Rock"), ct);
        var requestId = await requester.RequestProfileAsync(ProfilesRequests.Band("Podridão", Contact), ct);
        var claimId = await claimer.ClaimAsync(ownerless, Proof, ct);
        var queue = (await admin.QueueAsync(ct)).ToString();
        queue.ShouldContain(Contact, Case.Sensitive, "a fila do admin devolve o contato");
        await admin.ApproveAsync(requestId, ct);
        await admin.RejectAsync(claimId, "Não confirmamos que você é o dono.", ct);
        var second = await claimer.RequestProfileAsync(ProfilesRequests.Band("Podridão Dois", SecondContact), ct);
        await admin.RejectAsync(second, "Banda repetida.", ct);

        var spans = await SpansWhenAsync(api, span => span.Kind == ActivityKind.Server && span.DisplayName.Contains("reject", StringComparison.OrdinalIgnoreCase), ct);
        spans.ShouldNotBeEmpty("nenhum span exportado");
        var logs = api.Logs;
        logs.ShouldNotBeEmpty("nenhum log exportado");

        string[] secrets = [RequesterEmail, ClaimerEmail, Contact, Proof, SecondContact];
        var leaks = logs.SelectMany(log =>
                new[] { log.FormattedMessage, log.Body, log.Exception?.ToString() }
                    .Concat((log.Attributes ?? []).Select(attribute => $"{attribute.Key}={attribute.Value}"))
                    .Select(text => ($"log {log.CategoryName}", text)))
            .Concat(spans.SelectMany(span =>
                new[] { span.DisplayName, span.StatusDescription }
                    .Concat(span.TagObjects.Select(tag => $"{tag.Key}={tag.Value}"))
                    .Concat(span.Events.SelectMany(e => e.Tags.Select(tag => $"{e.Name}:{tag.Key}={tag.Value}")))
                    .Select(text => ($"span {span.DisplayName}", text))))
            .Where(item => item.text is { } text && secrets.Any(secret => text.Contains(secret, StringComparison.OrdinalIgnoreCase)))
            .Select(item => $"{item.Item1}: {item.text}")
            .Distinct(StringComparer.Ordinal)
            .ToList();

        leaks.ShouldBeEmpty("e-mail ou contato em claro nos sinais exportados");
    }

    private static async Task<IReadOnlyList<Activity>> SpansWhenAsync(
        ProfilesApiFactory api, Func<Activity, bool> predicate, CancellationToken ct)
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
}
