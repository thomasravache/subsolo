using Subsolo.Common.Domain;
using Subsolo.Engagement.UnitTests.Infrastructure;
using Subsolo.Modules.Engagement.Application;
using Subsolo.Modules.Engagement.Application.Features.AttendShow;

namespace Subsolo.Engagement.UnitTests;

/// <summary>
/// SPEC-0029 UT-01 — o handler de <c>AttendShow</c> com o catálogo de teste (shows agendado, cancelado e passado): só o
/// agendado futuro aceita o "vou" (gravado com a data do relógio, show validado pelo
/// <c>IEventsCatalog.GetShowStateAsync</c>); repetir dá sucesso sem duplicar; cancelado, adiado (Emenda 1) e passado →
/// <c>engagement.show_not_attendable</c> (409); inexistente → <c>engagement.show_not_found</c> (404), sem gravar nada.
/// </summary>
public sealed class AttendShowTests
{
    private static readonly Guid Fan = Guid.CreateVersion7();

    private static Task<Result> AttendAsync(EngagementHarness harness, Guid showId, Guid? userId = null) =>
        harness.Create<AttendShowCommandHandler>()
            .Handle(new AttendShowCommand(userId ?? Fan, showId), TestContext.Current.CancellationToken);

    private static Guid Scheduled(EngagementHarness harness) =>
        harness.Catalog.AddShow("scheduled", EngagementHarness.Start.AddDays(3));

    [Fact]
    [Trait("Spec", "SPEC-0029:UT-01")]
    public async Task AttendingAScheduledFutureShowCreatesTheAttendance()
    {
        var harness = new EngagementHarness();
        var show = Scheduled(harness);

        var result = await AttendAsync(harness, show);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        var attendance = harness.Attendances.All.ShouldHaveSingleItem("o vou não foi gravado");
        attendance.UserId.ShouldBe(Fan);
        attendance.ShowId.ShouldBe(show);
        attendance.CreatedAtUtc.ShouldBe(harness.Clock.GetUtcNow());
        harness.Catalog.ShowQueries.ShouldContain(show, "o show não foi validado pelo IEventsCatalog");
    }

    [Fact]
    [Trait("Spec", "SPEC-0029:UT-01")]
    public async Task AttendingAgainSucceedsWithoutDuplicating()
    {
        var harness = new EngagementHarness();
        var show = Scheduled(harness);
        (await AttendAsync(harness, show)).IsSuccess.ShouldBeTrue();
        var first = harness.Attendances.All.ShouldHaveSingleItem();
        harness.Clock.Advance(TimeSpan.FromHours(1));

        var again = await AttendAsync(harness, show);

        again.IsSuccess.ShouldBeTrue(again.IsFailure ? again.Error.Code : "marcar de novo deveria dar sucesso (idempotente)");
        harness.Attendances.All.ShouldHaveSingleItem("marcar de novo duplicou o vou").ShouldBeSameAs(first);
        first.CreatedAtUtc.ShouldBe(EngagementHarness.Start, "marcar de novo não muda a data do vou");
    }

    [Fact]
    [Trait("Spec", "SPEC-0029:UT-01")]
    public async Task AnotherFanAttendingTheSameShowGetsTheirOwnAttendance()
    {
        var harness = new EngagementHarness();
        var show = Scheduled(harness);
        var other = Guid.CreateVersion7();

        (await AttendAsync(harness, show)).IsSuccess.ShouldBeTrue();
        (await AttendAsync(harness, show, other)).IsSuccess.ShouldBeTrue();

        harness.Attendances.All.Select(attendance => attendance.UserId).ShouldBe([Fan, other], ignoreOrder: true);
    }

    [Fact]
    [Trait("Spec", "SPEC-0029:UT-01")]
    public async Task CancelledShowIsNotAttendable()
    {
        var harness = new EngagementHarness();
        var show = harness.Catalog.AddShow("cancelled", EngagementHarness.Start.AddDays(3));

        var result = await AttendAsync(harness, show);

        ShouldBeNotAttendable(result, "cancelado");
        harness.Attendances.All.ShouldBeEmpty("nada é gravado para show cancelado");
    }

    [Fact]
    [Trait("Spec", "SPEC-0029:UT-01")]
    public async Task PostponedShowIsNotAttendable()
    {
        var harness = new EngagementHarness();
        var show = harness.Catalog.AddShow("postponed", EngagementHarness.Start.AddDays(3));

        var result = await AttendAsync(harness, show);

        ShouldBeNotAttendable(result, "adiado");
        harness.Attendances.All.ShouldBeEmpty("nada é gravado para show adiado");
    }

    [Theory]
    [Trait("Spec", "SPEC-0029:UT-01")]
    [InlineData(-1)]
    [InlineData(-24 * 30)]
    public async Task PastShowIsNotAttendable(int hoursFromNow)
    {
        var harness = new EngagementHarness();
        var show = harness.Catalog.AddShow("scheduled", EngagementHarness.Start.AddHours(hoursFromNow));

        var result = await AttendAsync(harness, show);

        ShouldBeNotAttendable(result, "passado");
        harness.Attendances.All.ShouldBeEmpty("nada é gravado para show passado");
    }

    [Fact]
    [Trait("Spec", "SPEC-0029:UT-01")]
    public async Task ShowThatBecamePastWithTheClockIsNotAttendable()
    {
        var harness = new EngagementHarness();
        var show = harness.Catalog.AddShow("scheduled", EngagementHarness.Start.AddHours(2));
        harness.Clock.Advance(TimeSpan.FromHours(3));

        var result = await AttendAsync(harness, show);

        ShouldBeNotAttendable(result, "que já começou pelo relógio");
        harness.Attendances.All.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Spec", "SPEC-0029:UT-01")]
    public async Task UnknownShowIsShowNotFound()
    {
        var harness = new EngagementHarness();
        Scheduled(harness);

        var result = await AttendAsync(harness, Guid.CreateVersion7());

        result.IsFailure.ShouldBeTrue("marcar vou num show inexistente deveria falhar");
        result.Error.Code.ShouldBe(EngagementErrors.ShowNotFound);
        result.Error.Code.ShouldBe("engagement.show_not_found");
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        harness.Attendances.All.ShouldBeEmpty("nada é gravado para show inexistente");
    }

    private static void ShouldBeNotAttendable(Result result, string what)
    {
        result.IsFailure.ShouldBeTrue($"marcar vou num show {what} deveria falhar");
        result.Error.Code.ShouldBe(EngagementErrors.ShowNotAttendable);
        result.Error.Code.ShouldBe("engagement.show_not_attendable");
        result.Error.Type.ShouldBe(ErrorType.Conflict);
    }
}
