using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Subsolo.Observability.UnitTests;

/// <summary>
/// SPEC-0009 UT-04 (contrato v2, ADR-0008) — o redator cobre o que vai além da mensagem e dos atributos string:
/// (a) a exceção do log (mensagem, stack, <c>ToString</c>) e o evento <c>exception</c> do span
/// (<see cref="Activity.AddException"/>); (b) um escopo <see cref="Dictionary{TKey, TValue}"/>; (c) um valor
/// não-string (<see cref="Uri"/>) no log e no span; (d) um texto patológico que estoura o timeout da regex sai
/// <c>[REDACTED]</c>, sem lançar (falha fechado).
/// </summary>
public sealed class RedactionBeyondMessageTests
{
    private const string Email = "joao@exemplo.com";
    private const string MaskedEmail = "j***@e***.com";
    private const string Redacted = "[REDACTED]";
    private const string Category = "Subsolo.Observability.UnitTests";
    private const string TestSourceName = "Subsolo.Observability.UnitTests.RedactionBeyondMessage";

    private static readonly ActivitySource TestSource = new(TestSourceName);

    /// <summary>
    /// Um "e-mail" com a parte local enorme e sem domínio: a regex de e-mail (com backtracking, timeout de 100 ms)
    /// retrocede em cada posição (O(n²)) e estoura o timeout.
    /// </summary>
    private static readonly string Pathological = new string('a', 60_000) + "@";

    [Fact]
    [Trait("Spec", "SPEC-0009:UT-04")]
    public void ExceptionOfTheLogIsMasked()
    {
        using var telemetry = new ServiceDefaultsTelemetry();
        var logger = telemetry.CreateLogger(Category);

#pragma warning disable CA1848, CA1873 // log montado no teste, de propósito com dado pessoal na exceção
        logger.LogError(EmailException(), "Falha ao enviar o aviso");
#pragma warning restore CA1848, CA1873

        var record = telemetry.Logs.ShouldHaveSingleItem("o log não foi exportado pelo pipeline do ServiceDefaults");
        var exported = ExportedLog.Text(record);
        exported.ShouldNotContain(Email, Case.Insensitive,
            "o e-mail da exceção (mensagem, stack ou ToString) sai em texto puro no log exportado");
        exported.ShouldContain(MaskedEmail, Case.Sensitive,
            "a exceção do log não foi exportada com o e-mail mascarado (a exceção não pode sumir do log)");
    }

    [Fact]
    [Trait("Spec", "SPEC-0009:UT-04")]
    public void ExceptionEventOfTheSpanIsMasked()
    {
        using var telemetry = new ServiceDefaultsTelemetry(TestSourceName);
        string spanId;

        using (var activity = TestSource.StartActivity("SPEC-0009:UT-04 span com exceção"))
        {
            activity.ShouldNotBeNull("o ServiceDefaults não assina as fontes Subsolo.*");
            spanId = activity.SpanId.ToHexString();
            activity.AddException(EmailException());
            activity.SetStatus(ActivityStatusCode.Error, nameof(InvalidOperationException));
        }

        var span = telemetry.Spans.Where(s => s.SpanId.ToHexString() == spanId)
            .ShouldHaveSingleItem("o span não foi exportado pelo pipeline do ServiceDefaults");
        var exception = span.Events.Where(e => e.Name == "exception")
            .ShouldHaveSingleItem("o evento exception não foi exportado no span");
        var text = string.Join("\n", exception.Tags.Select(tag => $"{tag.Key}={tag.Value}"));

        text.ShouldNotContain(Email, Case.Insensitive,
            "o e-mail sai em texto puro no evento exception do span (exception.message/exception.stacktrace)");
        exception.Tags.ShouldContain(tag => tag.Key == "exception.message" && (tag.Value as string ?? "").Contains(MaskedEmail),
            "o evento exception do span não traz exception.message com o e-mail mascarado");
    }

    [Fact]
    [Trait("Spec", "SPEC-0009:UT-04")]
    public void DictionaryScopeIsMasked()
    {
        using var telemetry = new ServiceDefaultsTelemetry();
        var logger = telemetry.CreateLogger(Category);

#pragma warning disable CA1848 // log montado no teste
        using (logger.BeginScope(new Dictionary<string, object?> { ["email"] = Email, ["pedido"] = 42 }))
        {
            logger.LogInformation("Pedido recebido");
        }
#pragma warning restore CA1848

        var record = telemetry.Logs.ShouldHaveSingleItem("o log não foi exportado pelo pipeline do ServiceDefaults");
        var fields = ExportedLog.Fields(record);
        fields.ShouldContainKey("email", "o escopo dicionário não foi exportado com o log");
        fields.ShouldContainKeyAndValue("email", MaskedEmail, "o e-mail do escopo dicionário não foi mascarado");
        fields.ShouldContainKeyAndValue("pedido", "42", "o valor não sensível do escopo não pode mudar");
        ExportedLog.Text(record).ShouldNotContain(Email, Case.Insensitive, "o e-mail do escopo sai em texto puro");
    }

    [Fact]
    [Trait("Spec", "SPEC-0009:UT-04")]
    public void NonStringLogValueIsMasked()
    {
        using var telemetry = new ServiceDefaultsTelemetry();
        var logger = telemetry.CreateLogger(Category);
        var link = new Uri($"https://subsolo.local/convite?para={Email}");

#pragma warning disable CA1848, CA1873 // log montado no teste
        logger.LogInformation("Convite enviado: {Link}", link);
#pragma warning restore CA1848, CA1873

        var record = telemetry.Logs.ShouldHaveSingleItem("o log não foi exportado pelo pipeline do ServiceDefaults");
        var exported = ExportedLog.Text(record);
        exported.ShouldNotContain(Email, Case.Insensitive,
            "o e-mail dentro de um valor não-string (Uri) sai em texto puro no log exportado");
        exported.ShouldContain(MaskedEmail, Case.Sensitive, "o e-mail dentro do Uri não foi mascarado");
    }

    [Fact]
    [Trait("Spec", "SPEC-0009:UT-04")]
    public void NonStringSpanTagIsMasked()
    {
        using var telemetry = new ServiceDefaultsTelemetry(TestSourceName);
        string spanId;

        using (var activity = TestSource.StartActivity("SPEC-0009:UT-04 span com Uri"))
        {
            activity.ShouldNotBeNull("o ServiceDefaults não assina as fontes Subsolo.*");
            spanId = activity.SpanId.ToHexString();
            activity.SetTag("subsolo.invite.link", new Uri($"https://subsolo.local/convite?para={Email}"));
        }

        var span = telemetry.Spans.Where(s => s.SpanId.ToHexString() == spanId)
            .ShouldHaveSingleItem("o span não foi exportado pelo pipeline do ServiceDefaults");
        var value = span.GetTagItem("subsolo.invite.link")?.ToString() ?? string.Empty;
        value.ShouldNotContain(Email, Case.Insensitive, "o e-mail dentro do Uri sai em texto puro no atributo do span");
        value.ShouldContain(MaskedEmail, Case.Sensitive, "o e-mail dentro do Uri do span não foi mascarado");
    }

    [Fact]
    [Trait("Spec", "SPEC-0009:UT-04")]
    public void LogValueThatTimesOutTheRegexIsRedactedWithoutThrowing()
    {
        using var telemetry = new ServiceDefaultsTelemetry();
        var logger = telemetry.CreateLogger(Category);

#pragma warning disable CA1848, CA1873 // log montado no teste
        Should.NotThrow(() => logger.LogWarning("Entrada recebida: {Detail}", Pathological),
            "o redator lançou ao mascarar um texto que estoura o timeout da regex");
#pragma warning restore CA1848, CA1873

        var record = telemetry.Logs.ShouldHaveSingleItem("o log não foi exportado pelo pipeline do ServiceDefaults");
        ExportedLog.Fields(record).ShouldContainKeyAndValue("Detail", Redacted,
            "o valor cujo mascaramento estourou o timeout não saiu [REDACTED]");
        ExportedLog.Text(record).ShouldNotContain(Pathological, Case.Sensitive,
            "o texto cujo mascaramento estourou o timeout sai sem mascaramento");
    }

    [Fact]
    [Trait("Spec", "SPEC-0009:UT-04")]
    public void SpanTagThatTimesOutTheRegexIsRedactedWithoutThrowing()
    {
        using var telemetry = new ServiceDefaultsTelemetry(TestSourceName);
        string? spanId = null;

        Should.NotThrow(
            () =>
            {
                using var activity = TestSource.StartActivity("SPEC-0009:UT-04 span patológico");
                activity.ShouldNotBeNull("o ServiceDefaults não assina as fontes Subsolo.*");
                spanId = activity.SpanId.ToHexString();
                activity.SetTag("subsolo.detail", Pathological);
            },
            "o processor de spans lançou ao mascarar um texto que estoura o timeout da regex");

        var span = telemetry.Spans.Where(s => s.SpanId.ToHexString() == spanId)
            .ShouldHaveSingleItem("o span não foi exportado pelo pipeline do ServiceDefaults");
        span.GetTagItem("subsolo.detail").ShouldBe(Redacted,
            "o atributo cujo mascaramento estourou o timeout não saiu [REDACTED]");
    }

    /// <summary>Exceção com o e-mail na mensagem (e na da exceção interna), já lançada (com stack).</summary>
    private static InvalidOperationException EmailException()
    {
        try
        {
            try
            {
                throw new FormatException($"destinatário {Email} inválido");
            }
            catch (FormatException inner)
            {
                throw new InvalidOperationException($"não foi possível avisar {Email}", inner);
            }
        }
        catch (InvalidOperationException exception)
        {
            return exception;
        }
    }
}
