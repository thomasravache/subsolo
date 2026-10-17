using System.Text.Json;
using System.Text.RegularExpressions;

namespace Subsolo.Observability.UnitTests;

/// <summary>
/// SPEC-0009 UT-05 (contrato v2) — o alerta "Falha de job" de <c>infra/seq/alerts.json</c> seleciona os eventos pelo
/// nome do evento (<c>EventName</c> = <c>OutboxProcessingFailed</c>), não pelo texto da mensagem.
/// <para>
/// Como o nome chega ao Seq: o exportador OTLP do .NET (1.20) grava <c>EventId.Name</c> só no campo
/// <c>LogRecord.event_name</c> do OTLP (campo 12; conferido serializando um log), que o Seq (2025.2) não expõe para
/// consulta. Por isso o ServiceDefaults (<c>EventNameLogProcessor</c>) grava também o atributo <c>event.name</c>,
/// e o filtro precisa ser exatamente <c>event.name = 'OutboxProcessingFailed'</c>. A gravação do atributo é provada
/// pelo <see cref="EventNameAttributeTests"/>; este teste prova a forma do filtro: igualdade sobre
/// <c>event.name</c>, nenhuma propriedade derivada do texto (<c>@Message</c>, <c>@MessageTemplate</c>,
/// <c>@Exception</c>) nem o hash do modelo (<c>@EventType</c>), e nenhuma busca por texto (<c>like</c>,
/// <c>Contains</c>, <c>StartsWith</c>...).
/// </para>
/// </summary>
public sealed partial class JobFailureAlertFilterTests
{
    private const string EventName = "OutboxProcessingFailed";

    /// <summary>Atributo em que o ServiceDefaults grava o nome do evento (o que o Seq expõe para o filtro).</summary>
    private const string EventNameAttribute = "event.name";

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
        property.ShouldBe(EventNameAttribute,
            $"o filtro \"{filter}\" não compara o atributo {EventNameAttribute} (gravado pelo ServiceDefaults) com '{EventName}'");
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
