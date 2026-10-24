using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Subsolo.Observability.UnitTests;

/// <summary>
/// SPEC-0031 IT-07 (Emenda "3 (sem mudança de contrato)") — além do filtro do "Falha de job" (que pede 3 eventos em 10
/// minutos e não pega um job diário), <c>infra/seq/alerts.json</c> tem o alerta próprio "Resumo diário falhou": seleciona
/// pelo nome do evento (<c>event.name = 'DailyDigestRunFailed'</c>, por igualdade, sem texto da mensagem), janela de 1
/// hora, dispara com um evento (<c>count &gt;= 1</c>) e cita o runbook <c>docs/runbooks/outbox-parada.md</c>, que existe
/// no repositório (no padrão do <see cref="NoticeDeliveryFailedAlertTests"/>).
/// </summary>
public sealed partial class DailyDigestRunFailedAlertTests
{
    private const string Title = "Resumo diário falhou";

    private static readonly string[] TextDerivedProperties = ["@Message", "@MessageTemplate", "@Exception", "@EventType"];

    [Fact]
    [Trait("Spec", "SPEC-0031:IT-07")]
    public void TheDigestAlertSelectsDailyDigestRunFailedByEventName()
    {
        var filter = Alert().GetProperty("filter").GetString().ShouldNotBeNull("o alerta não tem filtro");

        EqualityWithEventName().Match(filter).Groups["property"].Value.ShouldBe(
            "event.name", $"o filtro \"{filter}\" não compara event.name com 'DailyDigestRunFailed' por igualdade");
        TextDerivedProperties.Where(property => filter.Contains(property, StringComparison.OrdinalIgnoreCase))
            .ShouldBeEmpty($"o filtro \"{filter}\" seleciona pelo texto do evento, não pelo nome");
    }

    [Fact]
    [Trait("Spec", "SPEC-0031:IT-07")]
    public void TheDigestAlertFiresOnOneEventInOneHour()
    {
        var alert = Alert();

        TimeSpan.Parse(alert.GetProperty("window").GetString()!, CultureInfo.InvariantCulture)
            .ShouldBe(TimeSpan.FromHours(1), "a janela do alerta é de 1 hora");
        var having = alert.GetProperty("having").GetString().ShouldNotBeNull("o alerta não tem limite (having)");
        Regex.Replace(having, @"\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(1)).Trim()
            .ShouldBe("count >= 1", "o resumo roda uma vez por dia: uma falha basta");
    }

    [Fact]
    [Trait("Spec", "SPEC-0031:IT-07")]
    public void TheDigestAlertCitesTheOutboxRunbook()
    {
        var runbook = Alert().GetProperty("runbook").GetString().ShouldNotBeNull("o alerta não cita runbook");

        var match = RunbookPath().Match(runbook);
        match.Success.ShouldBeTrue($"o runbook \"{runbook}\" não aponta para docs/runbooks/*.md");
        match.Value.ShouldBe("docs/runbooks/outbox-parada.md");
        File.Exists(Path.Combine(RepositoryRoot(), match.Value.Replace('/', Path.DirectorySeparatorChar)))
            .ShouldBeTrue($"o runbook {match.Value} não existe no repositório");
    }

    /// <summary>O único alerta "Resumo diário falhou" de <c>infra/seq/alerts.json</c>.</summary>
    private static JsonElement Alert()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "infra", "seq", "alerts.json")));
        return document.RootElement.GetProperty("alerts").EnumerateArray()
            .Where(item => item.GetProperty("title").GetString() == Title)
            .ShouldHaveSingleItem($"infra/seq/alerts.json não tem exatamente um alerta \"{Title}\"")
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

    [GeneratedRegex(@"(?<property>@?[A-Za-z_][\w.]*(?:\[['""][^'""]+['""]\])?)\s*==?\s*'DailyDigestRunFailed'(?:\s+ci)?(?![\w%])")]
    private static partial Regex EqualityWithEventName();

    [GeneratedRegex(@"docs/runbooks/[\w.-]+\.md")]
    private static partial Regex RunbookPath();
}
