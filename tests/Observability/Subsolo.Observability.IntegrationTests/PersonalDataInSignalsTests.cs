using System.Net;
using Subsolo.Common.Presentation.Security;
using Subsolo.IntegrationTests.Common;
using Subsolo.Observability.IntegrationTests.Infrastructure;

namespace Subsolo.Observability.IntegrationTests;

/// <summary>
/// SPEC-0009 IT-02 (ADR-0008) — um endpoint que loga um e-mail e recebe um cookie: nenhum log nem atributo de span
/// exportado pelo Subsolo.Api contém o e-mail ou o cookie em texto puro. Contrato v2: um endpoint que lança uma
/// exceção com o e-mail na mensagem — nem o log nem o evento <c>exception</c> do span exportados contêm o e-mail.
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
        using var client = api.CreateHttpsClient();
        var (token, antiforgeryCookie) = await ObservedApiFactory.AntiforgeryAsync(client, ct);
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"{PersonalDataEndpoint.Route}?email={Uri.EscapeDataString(Email)}");
        request.Headers.Add(AntiforgeryFilter.HeaderName, token);
        request.Headers.Add("Cookie", $"{CookieValue}; {antiforgeryCookie}");

        using var response = await client.SendAsync(request, ct);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var spans = await api.SpansWhenAsync(IsServerSpanOf(PersonalDataEndpoint.Route), SpanTimeout, ct);
        var logs = api.Logs;
        logs.ShouldContain(log => (log.Body ?? string.Empty).Contains("Cadastro de", StringComparison.Ordinal),
            "o log do endpoint não foi exportado");
        spans.ShouldContain(span => IsServerSpanOf(PersonalDataEndpoint.Route)(span),
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

    [Fact]
    [Trait("Spec", "SPEC-0009:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task ExceptionWithEmailIsNotExportedInPlainText()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new ObservedApiFactory(await ObservedApi.DatabaseAsync(postgres, ct), withTestEndpoints: true);
        using var client = api.CreateHttpsClient();
        var (token, antiforgeryCookie) = await ObservedApiFactory.AntiforgeryAsync(client, ct);
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"{PersonalDataExceptionEndpoint.Route}?email={Uri.EscapeDataString(Email)}");
        request.Headers.Add(AntiforgeryFilter.HeaderName, token);
        request.Headers.Add("Cookie", antiforgeryCookie);

        using var response = await client.SendAsync(request, ct);
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);

        var spans = await api.SpansWhenAsync(IsServerSpanOf(PersonalDataExceptionEndpoint.Route), SpanTimeout, ct);
        var logs = api.Logs;
        logs.ShouldContain(log => log.Exception != null, "o log da exceção não foi exportado");
        var server = spans.Where(IsServerSpanOf(PersonalDataExceptionEndpoint.Route))
            .ShouldHaveSingleItem("o span da requisição não foi exportado");
        server.Events.ShouldContain(e => e.Name == "exception", "o evento exception não foi exportado no span");

        var leaks = logs.SelectMany(log =>
                new[]
                    {
                        log.FormattedMessage, log.Body, log.Exception?.Message, log.Exception?.ToString(),
                    }
                    .Concat((log.Attributes ?? []).Select(a => $"{a.Key}={a.Value}"))
                    .Select(text => ($"log {log.CategoryName}", text)))
            .Concat(spans.SelectMany(span =>
                new[] { span.DisplayName, span.StatusDescription }
                    .Concat(span.TagObjects.Select(tag => $"{tag.Key}={tag.Value}"))
                    .Concat(span.Events.SelectMany(e => e.Tags.Select(tag => $"{e.Name}:{tag.Key}={tag.Value}")))
                    .Select(text => ($"span {span.DisplayName}", text))))
            .Where(item => item.text?.Contains(Email, StringComparison.OrdinalIgnoreCase) == true)
            .Select(item => $"{item.Item1}: {Excerpt(item.text!)}")
            .Distinct()
            .ToList();

        leaks.ShouldBeEmpty("e-mail da exceção em texto puro nos sinais exportados");
    }

    /// <summary>Prazo para o span do servidor terminar e chegar ao exportador depois da resposta.</summary>
    private static readonly TimeSpan SpanTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Span de servidor da requisição a <paramref name="route"/>.</summary>
    private static Func<System.Diagnostics.Activity, bool> IsServerSpanOf(string route) =>
        span => span.Kind == System.Diagnostics.ActivityKind.Server &&
                span.TagObjects.Any(tag => tag.Value?.ToString()?.Contains(route, StringComparison.Ordinal) == true);

    /// <summary>Trecho do texto em volta do e-mail (o stack inteiro deixaria a mensagem ilegível).</summary>
    private static string Excerpt(string text)
    {
        var index = text.IndexOf(Email, StringComparison.OrdinalIgnoreCase);
        var start = Math.Max(0, index - 80);
        return text.Substring(start, Math.Min(text.Length - start, Email.Length + 160));
    }
}
