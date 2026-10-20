using Subsolo.Engagement.UnitTests.Infrastructure;
using Subsolo.Modules.Engagement.Application.Features.GetAttendees;
using Subsolo.Modules.Engagement.IntegrationEvents;

namespace Subsolo.Engagement.UnitTests;

/// <summary>
/// SPEC-0029 UT-02 — <see cref="IAttendanceApi"/> sobre os "vou" de vários usuários: <c>CountAsync</c> devolve a
/// contagem certa de cada show pedido (sem trazer shows não pedidos; show sem "vou" conta 0) e
/// <c>GetAttendeesAsync</c> devolve os ids de quem marcou aquele show, sem repetição (base do lembrete).
/// </summary>
public sealed class AttendanceApiTests
{
    private static readonly Guid ShowA = Guid.CreateVersion7();
    private static readonly Guid ShowB = Guid.CreateVersion7();
    private static readonly Guid ShowC = Guid.CreateVersion7();
    private static readonly Guid Other = Guid.CreateVersion7();

    // Pela interface pública (como o lembrete a usará).
#pragma warning disable CA1859
    private static IAttendanceApi Api(EngagementHarness harness) => harness.Create<AttendanceApi>();
#pragma warning restore CA1859

    /// <summary>Três fãs em A (um deles também em B), um em B e outro num show não pedido; C sem ninguém.</summary>
    private static (Guid First, Guid Second, Guid Both, Guid OnlyB) Seed(EngagementHarness harness)
    {
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();
        var both = Guid.CreateVersion7();
        var onlyB = Guid.CreateVersion7();
        harness.AddAttendance(first, ShowA);
        harness.AddAttendance(second, ShowA);
        harness.AddAttendance(both, ShowA);
        harness.AddAttendance(both, ShowB);
        harness.AddAttendance(onlyB, ShowB);
        harness.AddAttendance(Guid.CreateVersion7(), Other);
        return (first, second, both, onlyB);
    }

    [Fact]
    [Trait("Spec", "SPEC-0029:UT-02")]
    public async Task CountAsyncReturnsTheCountOfEachRequestedShow()
    {
        var harness = new EngagementHarness();
        Seed(harness);

        var counts = await Api(harness).CountAsync([ShowA, ShowB, ShowC], TestContext.Current.CancellationToken);

        counts.GetValueOrDefault(ShowA).ShouldBe(3);
        counts.GetValueOrDefault(ShowB).ShouldBe(2);
        counts.GetValueOrDefault(ShowC).ShouldBe(0, "show sem vou conta 0");
        counts.Keys.ShouldNotContain(Other, "a contagem trouxe show que não foi pedido");
        counts.Keys.ShouldBeSubsetOf([ShowA, ShowB, ShowC]);
    }

    [Fact]
    [Trait("Spec", "SPEC-0029:UT-02")]
    public async Task CountAsyncWithRepeatedIdsCountsEachShowOnce()
    {
        var harness = new EngagementHarness();
        Seed(harness);

        var counts = await Api(harness).CountAsync([ShowA, ShowA, ShowB], TestContext.Current.CancellationToken);

        counts.GetValueOrDefault(ShowA).ShouldBe(3, "id repetido na consulta não dobra a contagem");
        counts.GetValueOrDefault(ShowB).ShouldBe(2);
    }

    [Fact]
    [Trait("Spec", "SPEC-0029:UT-02")]
    public async Task CountAsyncWithoutIdsIsEmpty()
    {
        var harness = new EngagementHarness();
        Seed(harness);

        var counts = await Api(harness).CountAsync([], TestContext.Current.CancellationToken);

        counts.Values.Where(count => count > 0).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Spec", "SPEC-0029:UT-02")]
    public async Task GetAttendeesAsyncReturnsWhoAttendsTheShowOnce()
    {
        var harness = new EngagementHarness();
        var (first, second, both, onlyB) = Seed(harness);

        var attendeesA = await Api(harness).GetAttendeesAsync(ShowA, TestContext.Current.CancellationToken);
        var attendeesB = await Api(harness).GetAttendeesAsync(ShowB, TestContext.Current.CancellationToken);

        attendeesA.ShouldBe([first, second, both], ignoreOrder: true);
        attendeesA.Count.ShouldBe(3, $"ids repetidos: {string.Join(", ", attendeesA)}");
        attendeesB.ShouldBe([both, onlyB], ignoreOrder: true);
    }

    [Fact]
    [Trait("Spec", "SPEC-0029:UT-02")]
    public async Task GetAttendeesAsyncOfAShowWithoutAttendancesIsEmpty()
    {
        var harness = new EngagementHarness();
        Seed(harness);

        var attendees = await Api(harness).GetAttendeesAsync(ShowC, TestContext.Current.CancellationToken);

        attendees.ShouldBeEmpty();
    }
}
