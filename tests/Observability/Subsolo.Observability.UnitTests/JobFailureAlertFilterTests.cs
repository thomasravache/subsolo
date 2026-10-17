using System.Text.Json;
using System.Text.RegularExpressions;

namespace Subsolo.Observability.UnitTests;

/// <summary>
/// SPEC-0009 UT-05 (contrato v2) — o alerta "Falha de job" de <c>infra/seq/alerts.json</c> seleciona os eventos pelo
/// nome do evento (<c>EventName</c> = <c>OutboxProcessingFailed</c>), não pelo texto da mensagem.
/// <para>
/// Como o nome chega ao Seq: o exportador OTLP do .NET (1.20) grava <c>EventId.Name</c> no campo
/// <c>LogRecord.event_name</c> do OTLP (campo 12; conferido serializando um log), e só com
/// <c>OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EVENT_LOG_ATTRIBUTES=true</c> também no atributo
/// <c>logrecord.event.name</c>. A propriedade exata em que o Seq expõe esse campo não é fixada aqui (fica com o
/// IT-06, contra o Seq real); este teste exige só a forma do filtro: uma igualdade com
/// <c>'OutboxProcessingFailed'</c> sobre uma propriedade que não seja derivada do texto (<c>@Message</c>,
/// <c>@MessageTemplate</c>, <c>@Exception</c>) nem o hash do modelo (<c>@EventType</c>), e nenhuma busca por texto
/// (<c>like</c>, <c>Contains</c>, <c>StartsWith</c>...).
/// </para>
/// </summary>
public sealed partial class JobFailureAlertFilterTests
{
    private const string EventName = "OutboxProcessingFailed";

    /// <summary>Propriedades do Seq derivadas do texto do evento (ou o hash do modelo), que não identificam o nome.</summary>
    private static readonly string[] TextDerivedProperties = ["@Message", "@MessageTemplate", "@Exception", "@EventType"];

    [Fact]
    [Trait("Spec", "SPEC-0009:UT-05")]
    public void JobFailureAlertSelectsByEventNameNotByMessageText()
    {
        var filter = JobFailureFilter();

        filter.ShouldSatisfyAllConditions(
            () => EqualityWithEventName().IsMatch(filter).ShouldBeTrue(
                $"o filtro \"{filter}\" não compara uma propriedade com '{EventName}' por igualdade"),
            () => TextDerivedProperties
                .Where(property => filter.Contains(property, StringComparison.OrdinalIgnoreCase))
                .ShouldBeEmpty($"o filtro \"{filter}\" seleciona pelo texto do evento, não pelo nome"),
            () => TextSearch().IsMatch(filter).ShouldBeFalse(
                $"o filtro \"{filter}\" busca por texto (like/Contains/StartsWith/EndsWith/IndexOf)"));

        var property = EqualityWithEventName().Match(filter).Groups["property"].Value;
        TextDerivedProperties.ShouldNotContain(property, $"a propriedade comparada ({property}) é derivada do texto");
    }

    /// <summary>Filtro do alerta "Falha de job" em <c>infra/seq/alerts.json</c>.</summary>
    private static string JobFailureFilter()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(AlertsFile()));
        var alert = document.RootElement.GetProperty("alerts").EnumerateArray()
            .Where(item => item.GetProperty("title").GetString() == "Falha de job")
            .ShouldHaveSingleItem("infra/seq/alerts.json não tem exatamente um alerta \"Falha de job\"");
        return alert.GetProperty("filter").GetString().ShouldNotBeNull("o alerta \"Falha de job\" não tem filtro");
    }

    private static string AlertsFile()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "infra", "seq", "alerts.json");
            if (File.Exists(Path.Combine(directory.FullName, "Subsolo.slnx")) && File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("infra/seq/alerts.json não encontrado acima de " + AppContext.BaseDirectory);
    }

    /// <summary><c>&lt;propriedade&gt; = 'OutboxProcessingFailed'</c> (ou com <c>==</c>; a propriedade pode vir entre colchetes).</summary>
    [GeneratedRegex(@"(?<property>@?[A-Za-z_][\w.]*(?:\[['""][^'""]+['""]\])?)\s*==?\s*'OutboxProcessingFailed'(?:\s+ci)?(?![\w%])")]
    private static partial Regex EqualityWithEventName();

    [GeneratedRegex(@"\blike\b|\bContains\s*\(|\bStartsWith\s*\(|\bEndsWith\s*\(|\bIndexOf\s*\(", RegexOptions.IgnoreCase)]
    private static partial Regex TextSearch();
}
