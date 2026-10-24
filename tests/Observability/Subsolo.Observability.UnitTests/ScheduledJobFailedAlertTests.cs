using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Subsolo.Observability.UnitTests;

/// <summary>
/// SPEC-0036 UT-01 (H32-01) — o alerta de <c>count &gt;= 1</c> em 1 hora de <c>infra/seq/alerts.json</c> cobre os três
/// jobs agendados (<c>DailyDigestRunFailed</c>, <c>DayBeforeReminderRunFailed</c>, <c>NoticesRetentionRunFailed</c>), então
/// se chama "Job agendado falhou" (nenhum alerta se chama mais "Resumo diário falhou"): seleciona os três eventos por
/// igualdade de <c>event.name</c>, unidos por <c>or</c>. O runbook do alerta (<c>docs/runbooks/outbox-parada.md</c>) cita o
/// alerta pelo nome novo, lista os três eventos no trecho dele e não diz mais que o job roda "uma vez por dia" (o resumo
/// tem a recuperação a cada 15 min).
/// </summary>
public sealed partial class ScheduledJobFailedAlertTests
{
    private const string Title = "Job agendado falhou";

    private const string OldTitle = "Resumo diário falhou";

    private static readonly string[] ScheduledJobFailures =
        ["DailyDigestRunFailed", "DayBeforeReminderRunFailed", "NoticesRetentionRunFailed"];

    [Fact]
    [Trait("Spec", "SPEC-0036:UT-01")]
    public void TheScheduledJobAlertFiresOnOneOfTheThreeJobFailuresInOneHour()
    {
        var alert = Alert();

        var filter = alert.GetProperty("filter").GetString().ShouldNotBeNull($"o alerta \"{Title}\" não tem filtro");
        foreach (var eventName in ScheduledJobFailures)
        {
            EventNameEquals(eventName).IsMatch(filter)
                .ShouldBeTrue($"o filtro \"{filter}\" não compara event.name com '{eventName}'");
        }

        AndBetweenEventNames().IsMatch(filter)
            .ShouldBeFalse($"o filtro \"{filter}\" exige os eventos ao mesmo tempo (use or)");
        TimeSpan.Parse(alert.GetProperty("window").GetString()!, CultureInfo.InvariantCulture)
            .ShouldBe(TimeSpan.FromHours(1), "a janela do alerta é de 1 hora");
        var having = alert.GetProperty("having").GetString().ShouldNotBeNull("o alerta não tem limite (having)");
        Regex.Replace(having, @"\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(1)).Trim()
            .ShouldBe("count >= 1", "uma falha de job agendado basta");
    }

    [Fact]
    [Trait("Spec", "SPEC-0036:UT-01")]
    public void NoAlertIsCalledResumoDiarioFalhou()
    {
        Alerts().Select(item => item.GetProperty("title").GetString())
            .ShouldNotContain(OldTitle, $"infra/seq/alerts.json ainda tem o alerta \"{OldTitle}\"");
    }

    [Fact]
    [Trait("Spec", "SPEC-0036:UT-01")]
    public void TheRunbookListsTheThreeEventsUnderTheScheduledJobAlert()
    {
        var runbook = File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "runbooks", "outbox-parada.md"));

        runbook.ShouldNotContain(OldTitle, Case.Sensitive, $"o runbook outbox-parada.md ainda cita o alerta \"{OldTitle}\"");
        runbook.ShouldNotContain("uma vez por dia", Case.Insensitive, "o runbook outbox-parada.md ainda diz \"uma vez por dia\"");
        var start = runbook.IndexOf(Title, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, $"o runbook outbox-parada.md não cita o alerta \"{Title}\"");

        // O trecho do alerta: do primeiro "Job agendado falhou" até o fim do parágrafo (linha em branco).
        var end = runbook.IndexOf("\n\n", start, StringComparison.Ordinal);
        var section = end < 0 ? runbook[start..] : runbook[start..end];
        foreach (var eventName in ScheduledJobFailures)
        {
            section.ShouldContain(eventName, Case.Sensitive, $"o trecho do alerta \"{Title}\" no runbook não cita {eventName}: {section}");
        }
    }

    private static Regex EventNameEquals(string eventName) =>
        new($@"(?<![\w.])event\.name\s*==?\s*'{Regex.Escape(eventName)}'(?:\s+ci)?(?![\w%])", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static JsonElement[] Alerts()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "infra", "seq", "alerts.json")));
        return [.. document.RootElement.GetProperty("alerts").EnumerateArray().Select(item => item.Clone())];
    }

    /// <summary>O único alerta "Job agendado falhou" de <c>infra/seq/alerts.json</c>.</summary>
    private static JsonElement Alert() =>
        Alerts()
            .Where(item => item.GetProperty("title").GetString() == Title)
            .ShouldHaveSingleItem($"infra/seq/alerts.json não tem exatamente um alerta \"{Title}\"");

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

    [GeneratedRegex(@"'\s*\band\b\s*event\.name", RegexOptions.IgnoreCase)]
    private static partial Regex AndBetweenEventNames();
}
