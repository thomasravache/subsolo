using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

namespace Subsolo.Observability.UnitTests;

/// <summary>
/// SPEC-0009 UT-01 (ADR-0008) — o redator de dados pessoais do ServiceDefaults mascara, antes da exportação, o e-mail
/// (<c>joao@exemplo.com</c> → <c>j***@e***.com</c>) e troca token (<c>Authorization</c>) e cookie por
/// <c>[REDACTED]</c>, tanto nos atributos e na mensagem formatada dos logs quanto nos atributos dos spans.
/// </summary>
public sealed class PersonalDataRedactionTests
{
    private const string Email = "joao@exemplo.com";
    private const string MaskedEmail = "j***@e***.com";
    private const string Authorization = "Bearer abc";
    private const string Cookie = "sessao=c0ok1e-segredo-123";
    private const string Redacted = "[REDACTED]";

    /// <summary>Fonte própria do teste, assinada pelo ServiceDefaults (<c>Subsolo.*</c>).</summary>
    private const string TestSourceName = "Subsolo.Observability.UnitTests.Redaction";

    private static readonly ActivitySource TestSource = new(TestSourceName);

    [Fact]
    [Trait("Spec", "SPEC-0009:UT-01")]
    public void LogAttributesAndFormattedMessageAreMasked()
    {
        using var telemetry = new ServiceDefaultsTelemetry();
        var logger = telemetry.CreateLogger("Subsolo.Observability.UnitTests");
        var marker = Guid.NewGuid().ToString("N");

#pragma warning disable CA1848, CA1873, CA2254 // mensagem de log montada no teste, de propósito com dados sensíveis
        logger.LogInformation(
            "Login {Marker} de {Email} com Authorization {Authorization} e cookie {Cookie}",
            marker, Email, Authorization, Cookie);
#pragma warning restore CA1848, CA1873, CA2254

        var record = telemetry.Logs.ShouldHaveSingleItem("o log não foi exportado pelo pipeline do ServiceDefaults");
        var attributes = Attributes(record);

        attributes.ShouldContainKeyAndValue("Email", MaskedEmail, "o e-mail do atributo não foi mascarado");
        attributes.ShouldContainKeyAndValue("Authorization", Redacted, "o token (Authorization) não foi trocado por [REDACTED]");
        attributes.ShouldContainKeyAndValue("Cookie", Redacted, "o cookie não foi trocado por [REDACTED]");

        var exported = Exported(record);
        exported.ShouldNotContain(Email, Case.Insensitive, "o e-mail aparece em texto puro no log exportado");
        exported.ShouldNotContain(Authorization, Case.Insensitive, "o token aparece em texto puro no log exportado");
        exported.ShouldNotContain(Cookie, Case.Insensitive, "o cookie aparece em texto puro no log exportado");
        (record.FormattedMessage ?? string.Empty).ShouldContain(MaskedEmail, Case.Sensitive,
            "a mensagem formatada não traz o e-mail mascarado");
    }

    [Fact]
    [Trait("Spec", "SPEC-0009:UT-01")]
    public void EmailInsideFreeTextIsMasked()
    {
        using var telemetry = new ServiceDefaultsTelemetry();
        var logger = telemetry.CreateLogger("Subsolo.Observability.UnitTests");

#pragma warning disable CA1848, CA1873 // mensagem de log montada no teste
        logger.LogWarning("Falha ao enviar o aviso: {Detail}", $"destinatário {Email} recusou a mensagem");
#pragma warning restore CA1848, CA1873

        var record = telemetry.Logs.ShouldHaveSingleItem("o log não foi exportado pelo pipeline do ServiceDefaults");
        var exported = Exported(record);
        exported.ShouldNotContain(Email, Case.Insensitive, "o e-mail dentro de um texto livre aparece em texto puro");
        exported.ShouldContain(MaskedEmail, Case.Sensitive, "o e-mail dentro de um texto livre não foi mascarado");
    }

    [Fact]
    [Trait("Spec", "SPEC-0009:UT-01")]
    public void SpanAttributesAreMasked()
    {
        using var telemetry = new ServiceDefaultsTelemetry();
        string spanId;

        using (var activity = TestSource.StartActivity("SPEC-0009:UT-01 span com dados pessoais"))
        {
            activity.ShouldNotBeNull("o ServiceDefaults não assina as fontes Subsolo.*");
            spanId = activity.SpanId.ToHexString();
            activity.SetTag("user.email", Email);
            activity.SetTag("http.request.header.authorization", Authorization);
            activity.SetTag("http.request.header.cookie", Cookie);
        }

        var span = telemetry.Spans.Where(s => s.SpanId.ToHexString() == spanId)
            .ShouldHaveSingleItem("o span não foi exportado pelo pipeline do ServiceDefaults");
        var tags = span.TagObjects.ToDictionary(tag => tag.Key, tag => tag.Value?.ToString(), StringComparer.Ordinal);

        tags.ShouldContainKeyAndValue("user.email", MaskedEmail, "o e-mail do atributo do span não foi mascarado");
        tags.ShouldContainKeyAndValue("http.request.header.authorization", Redacted, "o token do span não foi trocado por [REDACTED]");
        tags.ShouldContainKeyAndValue("http.request.header.cookie", Redacted, "o cookie do span não foi trocado por [REDACTED]");
    }

    private static Dictionary<string, string?> Attributes(LogRecord record) =>
        (record.Attributes ?? [])
            .GroupBy(attribute => attribute.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last().Value?.ToString(), StringComparer.Ordinal);

    /// <summary>Tudo o que o exportador leria do registro: mensagem formatada, corpo e atributos.</summary>
    private static string Exported(LogRecord record) =>
        string.Join(
            "\n",
            new[] { record.FormattedMessage, record.Body }
                .Concat((record.Attributes ?? []).Select(attribute => $"{attribute.Key}={attribute.Value}")));
}
