using System.Net;
using Aspire.Hosting.Testing;
using Subsolo.Observability.AppHostTests.Infrastructure;

namespace Subsolo.Observability.AppHostTests;

/// <summary>
/// SPEC-0009 IT-01 — com o AppHost e um coletor OTLP de teste, <c>GET /cidade-exemplo</c> no <c>web</c> (com um
/// <c>traceparent</c> do teste) gera, sob o mesmo trace id, spans do serviço <c>web</c>, do serviço <c>api</c> e da
/// consulta ao PostgreSQL (atributo <c>db.system</c>/<c>db.system.name</c> = <c>postgresql</c>).
/// CT-01 (rota do web) — com a instrumentação ativa (spans do <c>web</c> chegando ao coletor), <c>/cidade-exemplo</c>
/// continua 200 com a agenda e <c>/cidade-inexistente</c> continua 404, como no contrato v1 da agenda.
/// </summary>
public sealed class EndToEndTraceTests(ObservedAppHost stack) : IClassFixture<ObservedAppHost>
{
    private static readonly TimeSpan ExportTimeout = TimeSpan.FromSeconds(60);

    [Fact]
    [Trait("Spec", "SPEC-0009:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task CityPageProducesOneTraceAcrossWebApiAndPostgres()
    {
        var ct = TestContext.Current.CancellationToken;
        var (traceParent, traceId) = ObservedAppHost.NewTraceParent();

        using var web = stack.App.CreateHttpClient("web");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/cidade-exemplo");
        request.Headers.Add("traceparent", traceParent);
        using var response = await web.SendAsync(request, ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var spans = await stack.WaitForTraceAsync(
            traceId, s => HasService(s, "web") && HasService(s, "api") && HasPostgres(s), ExportTimeout, ct);

        var found = spans.Count == 0
            ? $"nenhum span do trace {traceId}; serviços que exportaram: " +
              string.Join(", ", stack.Collector.Spans.Select(s => s.ServiceName).Distinct())
            : string.Join("; ", spans.Select(s => $"{s.ServiceName}: {s.Name}"));
        HasService(spans, "web").ShouldBeTrue($"sem span do serviço web no trace ({found})");
        HasService(spans, "api").ShouldBeTrue($"sem span do serviço api no trace ({found})");
        HasPostgres(spans).ShouldBeTrue($"sem span do PostgreSQL no trace ({found})");
    }

    [Theory]
    [Trait("Spec", "SPEC-0009:CT-01")]
    [Trait("Kind", "Integration")]
    [InlineData("/cidade-exemplo", HttpStatusCode.OK, "Agenda Cidade Exemplo")]
    [InlineData("/cidade-inexistente", HttpStatusCode.NotFound, "Cidade não encontrada")]
    public async Task CityRouteKeepsTheContractWithInstrumentationActive(string path, HttpStatusCode expected, string text)
    {
        var ct = TestContext.Current.CancellationToken;
        var (traceParent, traceId) = ObservedAppHost.NewTraceParent();

        using var web = stack.App.CreateHttpClient("web");
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("traceparent", traceParent);
        using var response = await web.SendAsync(request, ct);
        var html = await response.Content.ReadAsStringAsync(ct);

        response.StatusCode.ShouldBe(expected);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
        html.ShouldContain(text);

        var spans = await stack.WaitForTraceAsync(traceId, s => HasService(s, "web"), ExportTimeout, ct);
        HasService(spans, "web").ShouldBeTrue($"instrumentação do web inativa: nenhum span do web no trace {traceId}");
    }

    private static bool HasService(IReadOnlyList<CollectedSpan> spans, string service) =>
        spans.Any(span => span.ServiceName == service);

    private static bool HasPostgres(IReadOnlyList<CollectedSpan> spans) =>
        spans.Any(span =>
            span.Attributes.GetValueOrDefault("db.system") == "postgresql" ||
            span.Attributes.GetValueOrDefault("db.system.name") == "postgresql");
}
