using System.Text.Json;
using System.Text.RegularExpressions;

namespace Subsolo.Observability.UnitTests;

/// <summary>
/// SPEC-0031 H30-05 (item herdado da revisão da SPEC-0030) — o alerta "Falha de job" de <c>infra/seq/alerts.json</c> também
/// seleciona a falha do job de entregas de avisos: o filtro compara <c>event.name</c> com <c>'NoticeDeliveriesRunFailed'</c>
/// por igualdade, junto de <c>'OutboxProcessingFailed'</c> (a forma do filtro continua a do SPEC-0009:UT-05).
/// </summary>
public sealed partial class JobFailureAlertCoversNoticeDeliveriesTests
{
    [Fact]
    [Trait("Spec", "SPEC-0031:H30-05")]
    public void JobFailureAlertSelectsNoticeDeliveriesRunFailedBesideOutboxProcessingFailed()
    {
        var filter = JobFailureFilter();

        EventNameEquals("OutboxProcessingFailed").IsMatch(filter)
            .ShouldBeTrue($"o filtro \"{filter}\" perdeu event.name = 'OutboxProcessingFailed'");
        EventNameEquals("NoticeDeliveriesRunFailed").IsMatch(filter)
            .ShouldBeTrue($"o filtro \"{filter}\" não compara event.name com 'NoticeDeliveriesRunFailed'");
        AndBetweenEventNames().IsMatch(filter)
            .ShouldBeFalse($"o filtro \"{filter}\" exige os dois eventos ao mesmo tempo (use or)");
    }

    [Fact]
    [Trait("Spec", "SPEC-0031:H30-06")]
    public void NoticeDeliveryFailedAlertDoesNotSelectSkippedDeliveries()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(AlertsFile()));
        var filter = document.RootElement.GetProperty("alerts").EnumerateArray()
            .Single(item => item.GetProperty("title").GetString() == "Falha no envio de aviso")
            .GetProperty("filter").GetString() ?? string.Empty;

        filter.ShouldNotContain("NoticeDeliverySkipped", Case.Insensitive, "a entrega sem canal entra no alerta de falha");
    }

    private static Regex EventNameEquals(string eventName) =>
        new($@"(?<![\w.])event\.name\s*==?\s*'{Regex.Escape(eventName)}'(?:\s+ci)?(?![\w%])", RegexOptions.None, TimeSpan.FromSeconds(1));

    /// <summary>Filtro do alerta "Falha de job".</summary>
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

    [GeneratedRegex(@"'\s*\band\b\s*event\.name", RegexOptions.IgnoreCase)]
    private static partial Regex AndBetweenEventNames();
}
