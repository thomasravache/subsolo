using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Subsolo.Observability.UnitTests;

/// <summary>
/// SPEC-0016 IT-10 (Emenda 4, §7.6 k; Plano de Rollout §8) — <c>infra/seq/alerts.json</c> tem o alerta de falha no envio
/// do link mágico: seleciona pelo nome do evento (<c>event.name = 'MagicLinkSendFailed'</c>, por igualdade, sem texto
/// da mensagem — como o "Falha de job" do <see cref="JobFailureAlertFilterTests"/>), janela de 10 minutos, dispara
/// acima de 5 (<c>count &gt; 5</c>) e cita um runbook de <c>docs/runbooks/</c> que existe no repositório.
/// </summary>
public sealed partial class MagicLinkSendFailedAlertTests
{
    private const string EventName = "MagicLinkSendFailed";

    private static readonly string[] TextDerivedProperties = ["@Message", "@MessageTemplate", "@Exception", "@EventType"];

    [Fact]
    [Trait("Spec", "SPEC-0016:IT-10")]
    public void AlertSelectsMagicLinkSendFailedByEventName()
    {
        var filter = Alert().GetProperty("filter").GetString().ShouldNotBeNull("o alerta não tem filtro");

        EqualityWithEventName().Match(filter).Groups["property"].Value.ShouldBe(
            "event.name", $"o filtro \"{filter}\" não compara event.name com '{EventName}' por igualdade");
        TextDerivedProperties.Where(property => filter.Contains(property, StringComparison.OrdinalIgnoreCase))
            .ShouldBeEmpty($"o filtro \"{filter}\" seleciona pelo texto do evento, não pelo nome");
        TextSearch().IsMatch(filter).ShouldBeFalse($"o filtro \"{filter}\" busca por texto");
    }

    [Fact]
    [Trait("Spec", "SPEC-0016:IT-10")]
    public void AlertFiresAboveFiveInTenMinutes()
    {
        var alert = Alert();

        TimeSpan.Parse(alert.GetProperty("window").GetString()!, CultureInfo.InvariantCulture)
            .ShouldBe(TimeSpan.FromMinutes(10), "a janela do alerta é de 10 minutos");
        var having = alert.GetProperty("having").GetString().ShouldNotBeNull("o alerta não tem limite (having)");
        Regex.Replace(having, @"\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(1)).Trim()
            .ShouldBe("count > 5", "o alerta dispara com mais de 5 falhas");
    }

    [Fact]
    [Trait("Spec", "SPEC-0016:IT-10")]
    public void AlertCitesAnExistingRunbook()
    {
        var runbook = Alert().GetProperty("runbook").GetString().ShouldNotBeNull("o alerta não cita runbook");

        var match = RunbookPath().Match(runbook);
        match.Success.ShouldBeTrue($"o runbook \"{runbook}\" não aponta para docs/runbooks/*.md");
        File.Exists(Path.Combine(RepositoryRoot(), match.Value.Replace('/', Path.DirectorySeparatorChar)))
            .ShouldBeTrue($"o runbook {match.Value} não existe no repositório");
    }

    /// <summary>O único alerta de <c>infra/seq/alerts.json</c> cujo filtro cita <c>MagicLinkSendFailed</c>.</summary>
    private static JsonElement Alert()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "infra", "seq", "alerts.json")));
        return document.RootElement.GetProperty("alerts").EnumerateArray()
            .Where(item => item.TryGetProperty("filter", out var filter)
                && (filter.GetString() ?? string.Empty).Contains(EventName, StringComparison.Ordinal))
            .ShouldHaveSingleItem($"infra/seq/alerts.json não tem exatamente um alerta com '{EventName}' no filtro")
            .Clone();
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Subsolo.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Subsolo.slnx não encontrado acima de " + AppContext.BaseDirectory);
    }

    [GeneratedRegex(@"(?<property>@?[A-Za-z_][\w.]*(?:\[['""][^'""]+['""]\])?)\s*==?\s*'MagicLinkSendFailed'(?:\s+ci)?(?![\w%])")]
    private static partial Regex EqualityWithEventName();

    [GeneratedRegex(@"\blike\b|\bContains\s*\(|\bStartsWith\s*\(|\bEndsWith\s*\(|\bIndexOf\s*\(", RegexOptions.IgnoreCase)]
    private static partial Regex TextSearch();

    [GeneratedRegex(@"docs/runbooks/[\w.-]+\.md")]
    private static partial Regex RunbookPath();
}
