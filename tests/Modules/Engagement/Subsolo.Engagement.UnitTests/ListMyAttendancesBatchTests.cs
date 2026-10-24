using Subsolo.Engagement.UnitTests.Infrastructure;
using Subsolo.Modules.Engagement.Application.Features.ListMyAttendances;

namespace Subsolo.Engagement.UnitTests;

/// <summary>
/// SPEC-0032 H29-01 (item herdado da revisão da SPEC-0029; contrato v2) — "meus vou" lê a situação dos shows em lote
/// (<c>IEventsCatalog.GetShowStatesAsync</c>, uma chamada só), não um <c>GetShowStateAsync</c> por "vou" marcado; a lista
/// continua a do SPEC-0029 (só os shows que existem e não começaram, cancelado ou adiado futuro incluídos, do "vou" mais
/// antigo para o mais novo).
/// </summary>
public sealed class ListMyAttendancesBatchTests
{
    private static readonly Guid Fan = Guid.CreateVersion7();

    [Fact]
    [Trait("Spec", "SPEC-0032:H29-01")]
    public async Task TheShowStatesAreReadInOneBatch()
    {
        var harness = new EngagementHarness();
        var now = harness.Clock.GetUtcNow();
        var upcoming = harness.Catalog.AddShow("scheduled", now.AddDays(3));
        var cancelled = harness.Catalog.AddShow("cancelled", now.AddDays(4));
        var postponed = harness.Catalog.AddShow("postponed", now.AddDays(5));
        var past = harness.Catalog.AddShow("scheduled", now.AddHours(-1));
        var gone = Guid.CreateVersion7();
        foreach (var show in new[] { upcoming, cancelled, past, gone, postponed })
        {
            harness.AddAttendance(Fan, show);
            harness.Clock.Advance(TimeSpan.FromSeconds(1));
        }

        harness.AddAttendance(Guid.CreateVersion7(), upcoming);
        harness.Catalog.ShowQueries.Clear();

        var result = await harness.Create<ListMyAttendancesQueryHandler>()
            .Handle(new ListMyAttendancesQuery(Fan, "upcoming"), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        result.Value.ShowIds.ShouldBe([upcoming, cancelled, postponed], "a lista mudou: só os que existem e não começaram, na ordem do \"vou\"");
        harness.Catalog.ShowQueries.ShouldBeEmpty(
            $"a lista consultou o IEventsCatalog um show por vez ({harness.Catalog.ShowQueries.Count} chamadas a GetShowStateAsync)");
        harness.Catalog.StatesQueries.ShouldHaveSingleItem("as situações deveriam vir numa leitura em lote só")
            .ShouldBe([upcoming, cancelled, past, gone, postponed], ignoreOrder: true);
    }

    [Fact]
    [Trait("Spec", "SPEC-0032:H29-01")]
    public async Task WithoutAttendancesNoShowIsRead()
    {
        var harness = new EngagementHarness();

        var result = await harness.Create<ListMyAttendancesQueryHandler>()
            .Handle(new ListMyAttendancesQuery(Fan, null), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShowIds.ShouldBeEmpty();
        harness.Catalog.ShowQueries.ShouldBeEmpty();
        harness.Catalog.StatesQueries.Where(ids => ids.Count > 0).ShouldBeEmpty();
    }
}
