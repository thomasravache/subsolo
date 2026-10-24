using System.Text.Json;
using System.Text.RegularExpressions;

namespace Subsolo.Observability.UnitTests;

/// <summary>
/// SPEC-0032 IT-04 (Emenda 3, contrato v3) — o alerta "Falha de job" de
/// <c>infra/seq/alerts.json</c> também seleciona a falha do job do lembrete na véspera: o filtro compara
/// <c>event.name</c> com <c>'DayBeforeReminderRunFailed'</c> por igualdade, junto dos eventos que já cobria
/// (<c>OutboxProcessingFailed</c>, <c>NoticeDeliveriesRunFailed</c>, <c>DailyDigestRunFailed</c>), unidos por <c>or</c>
/// (a forma do filtro do SPEC-0009:UT-05 e do <see cref="JobFailureAlertCoversNoticeDeliveriesTests"/>). O runbook do
/// alerta (<c>outbox-parada.md</c>) cita o evento.
/// </summary>
public sealed partial class DayBeforeReminderRunFailedAlertTests
{
    [Fact]
    [Trait("Spec", "SPEC-0032:IT-04")]
    public void JobFailureAlertSelectsDayBeforeReminderRunFailedBesideTheOtherJobFailures()
    {
        var filter = JobFailureAlert().GetProperty("filter").GetString().ShouldNotBeNull("o alerta \"Falha de job\" não tem filtro");

        foreach (var eventName in new[] { "DayBeforeReminderRunFailed", "OutboxProcessingFailed", "NoticeDeliveriesRunFailed", "DailyDigestRunFailed" })
        {
            EventNameEquals(eventName).IsMatch(filter)
                .ShouldBeTrue($"o filtro \"{filter}\" não compara event.name com '{eventName}'");
        }

        AndBetweenEventNames().IsMatch(filter)
            .ShouldBeFalse($"o filtro \"{filter}\" exige os eventos ao mesmo tempo (use or)");
    }

    [Fact]
    [Trait("Spec", "SPEC-0032:IT-04")]
    public void TheJobFailureRunbookMentionsDayBeforeReminderRunFailed()
    {
        var runbook = Path.Combine(RepositoryRoot(), "docs", "runbooks", "outbox-parada.md");

        File.ReadAllText(runbook).ShouldContain("DayBeforeReminderRunFailed", Case.Sensitive, "o runbook outbox-parada.md não cita DayBeforeReminderRunFailed");
    }

    private static Regex EventNameEquals(string eventName) =>
        new($@"(?<![\w.])event\.name\s*==?\s*'{Regex.Escape(eventName)}'(?:\s+ci)?(?![\w%])", RegexOptions.None, TimeSpan.FromSeconds(1));

    /// <summary>O único alerta "Falha de job" de <c>infra/seq/alerts.json</c>.</summary>
    private static JsonElement JobFailureAlert()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "infra", "seq", "alerts.json")));
        return document.RootElement.GetProperty("alerts").EnumerateArray()
            .Where(item => item.GetProperty("title").GetString() == "Falha de job")
            .ShouldHaveSingleItem("infra/seq/alerts.json não tem exatamente um alerta \"Falha de job\"")
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

    [GeneratedRegex(@"'\s*\band\b\s*event\.name", RegexOptions.IgnoreCase)]
    private static partial Regex AndBetweenEventNames();
}
