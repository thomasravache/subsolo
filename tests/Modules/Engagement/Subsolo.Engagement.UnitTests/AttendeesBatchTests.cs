using Subsolo.Engagement.UnitTests.Infrastructure;
using Subsolo.Modules.Engagement.Application.Features.GetAttendees;
using Subsolo.Modules.Engagement.IntegrationEvents;

namespace Subsolo.Engagement.UnitTests;

/// <summary>
/// SPEC-0038 UT-02 (H36-02) — <see cref="IAttendanceApi.GetAttendeesAsync(IReadOnlyCollection{Guid}, CancellationToken)"/>
/// (a leitura em lote da SPEC-0036) com "vou" em dois shows: lista vazia → dicionário vazio sem consultar o repositório;
/// ids repetidos e um show sem "vou" → cada show pedido aparece uma vez (o repositório recebe cada id uma vez), o sem
/// "vou" com conjunto vazio, e os ids de cada show sem repetição.
/// </summary>
public sealed class AttendeesBatchTests
{
    private static readonly Guid ShowA = Guid.CreateVersion7();
    private static readonly Guid ShowB = Guid.CreateVersion7();
    private static readonly Guid Empty = Guid.CreateVersion7();

    // Pela interface pública (como o lembrete a usa).
#pragma warning disable CA1859
    private static IAttendanceApi Api(EngagementHarness harness) => harness.Create<AttendanceApi>();
#pragma warning restore CA1859

    /// <summary>Dois fãs em A (um deles também em B) e um só em B.</summary>
    private static (Guid First, Guid Both, Guid OnlyB) Seed(EngagementHarness harness)
    {
        var first = Guid.CreateVersion7();
        var both = Guid.CreateVersion7();
        var onlyB = Guid.CreateVersion7();
        harness.AddAttendance(first, ShowA);
        harness.AddAttendance(both, ShowA);
        harness.AddAttendance(both, ShowB);
        harness.AddAttendance(onlyB, ShowB);
        return (first, both, onlyB);
    }

    [Fact]
    [Trait("Spec", "SPEC-0038:UT-02")]
    public async Task AnEmptyListReturnsAnEmptyDictionaryWithoutQuerying()
    {
        var harness = new EngagementHarness();
        Seed(harness);

        var attendees = await Api(harness).GetAttendeesAsync([], TestContext.Current.CancellationToken);

        attendees.ShouldBeEmpty();
        harness.Attendances.AttendeesQueries.ShouldBeEmpty("lista vazia não deveria consultar o repositório");
    }

    [Fact]
    [Trait("Spec", "SPEC-0038:UT-02")]
    public async Task RepeatedIdsAndAShowWithoutAttendancesReturnEachShowOnce()
    {
        var harness = new EngagementHarness();
        var (first, both, onlyB) = Seed(harness);

        var attendees = await Api(harness).GetAttendeesAsync(
            [ShowA, ShowB, ShowA, Empty, ShowB, Empty], TestContext.Current.CancellationToken);

        attendees.Keys.ShouldBe([ShowA, ShowB, Empty], ignoreOrder: true);
        attendees[ShowA].ShouldBe([first, both], ignoreOrder: true);
        attendees[ShowB].ShouldBe([both, onlyB], ignoreOrder: true);
        attendees[Empty].ShouldBeEmpty("show sem \"vou\" aparece com conjunto vazio");
        var query = harness.Attendances.AttendeesQueries.ShouldHaveSingleItem("uma leitura só para todos os shows");
        query.ShouldBe([ShowA, ShowB, Empty], ignoreOrder: true, "o repositório recebeu id repetido");
    }
}
