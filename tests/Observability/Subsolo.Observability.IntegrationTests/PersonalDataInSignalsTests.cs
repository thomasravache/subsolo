using System.Net;
using Subsolo.IntegrationTests.Common;
using Subsolo.Observability.IntegrationTests.Infrastructure;

namespace Subsolo.Observability.IntegrationTests;

/// <summary>
/// SPEC-0009 IT-02 (ADR-0008) — um endpoint que loga um e-mail e recebe um cookie: nenhum log nem atributo de span
/// exportado pelo Subsolo.Api contém o e-mail ou o cookie em texto puro.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PersonalDataInSignalsTests(PostgresFixture postgres)
{
    private const string Email = "fa.sigiloso@exemplo.com";
    private const string CookieValue = "sessao=c0ok1e-que-nao-pode-vazar";

    [Fact]
    [Trait("Spec", "SPEC-0009:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task ExportedLogsAndSpansDoNotContainEmailOrCookie()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new ObservedApiFactory(await ObservedApi.DatabaseAsync(postgres, ct), withTestEndpoints: true);
        using var client = api.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"{PersonalDataEndpoint.Route}?email={Uri.EscapeDataString(Email)}");
        request.Headers.Add("Cookie", CookieValue);

        using var response = await client.SendAsync(request, ct);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var logs = api.Logs;
        var spans = api.Spans;
        logs.ShouldContain(log => (log.Body ?? string.Empty).Contains("Cadastro de", StringComparison.Ordinal),
            "o log do endpoint não foi exportado");
        spans.ShouldContain(span => span.Kind == System.Diagnostics.ActivityKind.Server,
            "o span da requisição não foi exportado");

        var leaks = logs.SelectMany(log =>
                new[] { log.FormattedMessage, log.Body }
                    .Concat((log.Attributes ?? []).Select(a => $"{a.Key}={a.Value}"))
                    .Select(text => ("log", text)))
            .Concat(spans.SelectMany(span =>
                new[] { span.DisplayName, span.StatusDescription }
                    .Concat(span.TagObjects.Select(tag => $"{tag.Key}={tag.Value}"))
                    .Concat(span.Events.SelectMany(e => e.Tags.Select(tag => $"{e.Name}:{tag.Key}={tag.Value}")))
                    .Select(text => ($"span {span.DisplayName}", text))))
            .Where(item => item.text is not null &&
                           (item.text.Contains(Email, StringComparison.OrdinalIgnoreCase) ||
                            item.text.Contains(CookieValue, StringComparison.Ordinal)))
            .Select(item => $"{item.Item1}: {item.text}")
            .Distinct()
            .ToList();

        leaks.ShouldBeEmpty("dado pessoal em texto puro nos sinais exportados");
    }
}
