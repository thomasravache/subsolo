using Subsolo.Modules.Moderation.Application.Features.ListReports;
using Subsolo.Modules.Moderation.Domain.Reports;

namespace Subsolo.Moderation.UnitTests;

/// <summary>
/// SPEC-0033 UT-02 — o agrupador da fila: com denúncias de três shows (motivos variados, fora de ordem), devolve um item
/// por show, na ordem da primeira denúncia (mais antigos primeiro), com as contagens por motivo (zero nos ausentes), até
/// três detalhes mais recentes (do mais novo para o mais antigo, sem os vazios) e o instante da primeira denúncia; sem
/// denúncias, a fila é vazia.
/// </summary>
public sealed class ReportQueueGrouperTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid Oldest = Guid.Parse("0199e5a0-0000-7000-8000-000000000001");
    private static readonly Guid Middle = Guid.Parse("0199e5a0-0000-7000-8000-000000000002");
    private static readonly Guid Newest = Guid.Parse("0199e5a0-0000-7000-8000-000000000003");

    private static PendingReport Report(Guid show, string reason, int minutes, string? details = null) =>
        new(show, reason, details, Start.AddMinutes(minutes));

    /// <summary>Três shows: o <see cref="Middle"/> denunciado primeiro na lista, mas o <see cref="Oldest"/> é o mais antigo.</summary>
    private static List<PendingReport> ThreeShows() =>
    [
        Report(Middle, ReportReasons.WrongInfo, 10, "Data errada."),
        Report(Newest, ReportReasons.Fake, 30),
        Report(Oldest, ReportReasons.Fake, 50, "Detalhe 3."),
        Report(Oldest, ReportReasons.CancelledUnannounced, 0, "Detalhe 1."),
        Report(Oldest, ReportReasons.Other, 40, "Detalhe 2."),
        Report(Oldest, ReportReasons.Fake, 60, "Detalhe 4."),
        Report(Oldest, ReportReasons.CancelledUnannounced, 70, null),
        Report(Middle, ReportReasons.WrongInfo, 20, ""),
    ];

    [Fact]
    [Trait("Spec", "SPEC-0033:UT-02")]
    public void ReturnsOneItemPerShowOrderedByTheFirstReport()
    {
        var queue = ReportQueueGrouper.Group(ThreeShows());

        queue.Select(item => item.ShowId).ShouldBe([Oldest, Middle, Newest]);
        queue.Select(item => item.FirstReportedAtUtc).ShouldBe([Start, Start.AddMinutes(10), Start.AddMinutes(30)]);
    }

    [Fact]
    [Trait("Spec", "SPEC-0033:UT-02")]
    public void CountsTheReportsOfEachShowByReason()
    {
        var queue = ReportQueueGrouper.Group(ThreeShows()).ToDictionary(item => item.ShowId);

        queue[Oldest].Counts.ShouldBe(new ReportCounts(Fake: 2, CancelledUnannounced: 2, WrongInfo: 0, Other: 1));
        queue[Middle].Counts.ShouldBe(new ReportCounts(Fake: 0, CancelledUnannounced: 0, WrongInfo: 2, Other: 0));
        queue[Newest].Counts.ShouldBe(new ReportCounts(Fake: 1, CancelledUnannounced: 0, WrongInfo: 0, Other: 0));
    }

    [Fact]
    [Trait("Spec", "SPEC-0033:UT-02")]
    public void KeepsUpToThreeLatestNonEmptyDetailsNewestFirst()
    {
        var queue = ReportQueueGrouper.Group(ThreeShows()).ToDictionary(item => item.ShowId);

        queue[Oldest].LatestDetails.ShouldBe(["Detalhe 4.", "Detalhe 3.", "Detalhe 2."]);
        queue[Middle].LatestDetails.ShouldBe(["Data errada."]);
        queue[Newest].LatestDetails.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Spec", "SPEC-0033:UT-02")]
    public void NoReportsMakeAnEmptyQueue() =>
        ReportQueueGrouper.Group([]).ShouldBeEmpty();
}
