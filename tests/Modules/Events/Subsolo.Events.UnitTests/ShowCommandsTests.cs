using Subsolo.Common.Domain;
using Subsolo.Events.UnitTests.Infrastructure;
using Subsolo.Modules.Events.Application.Features.ChangeShowStatus;
using Subsolo.Modules.Events.Application.Features.CreateShow;
using Subsolo.Modules.Events.Application.Features.UpdateShow;
using Subsolo.Modules.Events.Domain.Shows;

namespace Subsolo.Events.UnitTests;

/// <summary>
/// SPEC-0023 — os handlers do cadastro de show com perfis e catálogo de teste (<see cref="ShowsHarness"/>):
/// <list type="bullet">
/// <item>UT-01 — "Noite da Peste II" em 14/11/2026 22:00 em Curitiba gera o slug <c>noite-da-peste-ii-2026-11-14</c>,
/// guarda o início em UTC (01:00 de 15/11), liga o flyer e gera <c>ShowPublished</c> com o organizador, a casa com perfil
/// e as bandas com perfil (o nome livre fica de fora); editando, o slug não muda e sai <c>ShowChanged</c>;</item>
/// <item>UT-03 — o artista dono do perfil A não cria nem edita show do perfil B (<c>auth.forbidden</c>, nada gravado); o
/// admin pode em qualquer perfil, inclusive sem dono.</item>
/// </list>
/// </summary>
public sealed class ShowCommandsTests
{
    private static readonly DateTimeOffset NoiteDaPesteUtc = new(2026, 11, 15, 1, 0, 0, TimeSpan.Zero);

    private readonly ShowsHarness _harness = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<Result<ShowSaved>> CreateAsync(CreateShowCommand command) =>
        _harness.Create<CreateShowCommandHandler>().Handle(command, Ct);

    private Task<Result<ShowSaved>> UpdateAsync(UpdateShowCommand command) =>
        _harness.Create<UpdateShowCommandHandler>().Handle(command, Ct);

    /// <summary>A edição com os mesmos dados de <paramref name="created"/>, trocando o que for informado.</summary>
    private static UpdateShowCommand Edit(
        Guid showId,
        CreateShowCommand created,
        Guid? userId = null,
        bool? isAdmin = null,
        string? startsLocal = null,
        IReadOnlyList<ShowLineupItemInput>? lineup = null) =>
        new(
            showId,
            userId ?? created.UserId,
            isAdmin ?? created.IsAdmin,
            created.OrganizerProfileId,
            created.Title,
            created.City,
            startsLocal ?? created.StartsLocal,
            created.Venue,
            lineup ?? created.Lineup,
            created.Styles,
            created.Price,
            created.TicketUrl,
            created.FlyerId);

    [Fact]
    [Trait("Spec", "SPEC-0023:UT-01")]
    public async Task CreatingGeneratesTheSlugStoresUtcAttachesTheFlyerAndRaisesShowPublished()
    {
        var command = ShowsHarness.NoiteDaPeste(TestProfiles.OwnerA, TestProfiles.ProducerA.Id);

        var result = await CreateAsync(command);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        result.Value.Slug.ShouldBe("noite-da-peste-ii-2026-11-14");
        var show = _harness.Shows.All.ShouldHaveSingleItem("o show não foi gravado");
        show.Id.ShouldBe(result.Value.Id);
        show.Slug.ShouldBe("noite-da-peste-ii-2026-11-14");
        show.City.ShouldBe("curitiba");
        show.Title.ShouldBe("Noite da Peste II");
        show.StartsAtUtc.ShouldBe(NoiteDaPesteUtc, "22:00 em America/Sao_Paulo (UTC−3) é 01:00 UTC do dia seguinte");
        show.OrganizerProfileId.ShouldBe(TestProfiles.ProducerA.Id);
        show.Venue.ProfileId.ShouldBe(TestProfiles.Venue.Id);
        show.Lineup.Select(item => item.ProfileId).ShouldBe([TestProfiles.Band.Id, null, TestProfiles.OtherBand.Id]);
        show.Lineup[1].Name.ShouldBe("Cova Rasa");
        show.Styles.ShouldBe(["death", "grind"]);
        show.FlyerId.ShouldBe(ShowsHarness.FlyerId);
        show.Status.ShouldBe(ShowStatuses.Scheduled);
        show.CreatedBy.ShouldBe(TestProfiles.OwnerA);
        _harness.Flyers.Attached.ShouldBe([ShowsHarness.FlyerId], "o flyer não foi ligado ao show");
        _harness.UnitOfWork.Saves.ShouldBe(1, "show, flyer ligado e evento vão numa gravação só");

        var published = _harness.UnitOfWork.Events.OfType<ShowPublishedDomainEvent>()
            .ShouldHaveSingleItem("ShowPublished não foi levantado (ou foi mais de uma vez)");
        published.ShowId.ShouldBe(show.Id);
        published.City.ShouldBe("curitiba");
        published.StartsAtUtc.ShouldBe(NoiteDaPesteUtc);
        published.ProfileIds.ShouldBe(
            [TestProfiles.ProducerA.Id, TestProfiles.Venue.Id, TestProfiles.Band.Id, TestProfiles.OtherBand.Id],
            ignoreOrder: true,
            "organizador + casa com perfil + bandas com perfil (o nome livre não tem id)");
    }

    [Fact]
    [Trait("Spec", "SPEC-0023:UT-01")]
    public async Task EditingKeepsTheSlugAndRaisesShowChanged()
    {
        var command = ShowsHarness.NoiteDaPeste(TestProfiles.OwnerA, TestProfiles.ProducerA.Id);
        var created = await CreateAsync(command);
        created.IsSuccess.ShouldBeTrue(created.IsFailure ? created.Error.Code : null);

        var result = await UpdateAsync(Edit(
            created.Value.Id,
            command,
            startsLocal: "2026-11-21T21:00",
            lineup: [new(null, "Vômito Ácido"), new(TestProfiles.Band.Id, null)]));

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        result.Value.ShouldBe(created.Value, "a edição devolve o mesmo id e o mesmo slug");
        var show = _harness.Shows.All.ShouldHaveSingleItem();
        show.Slug.ShouldBe("noite-da-peste-ii-2026-11-14", "o slug é imutável, mesmo com a data nova");
        show.StartsAtUtc.ShouldBe(new DateTimeOffset(2026, 11, 22, 0, 0, 0, TimeSpan.Zero));
        show.Lineup.Select(item => item.Name).ShouldContain("Vômito Ácido");
        show.Lineup.Count.ShouldBe(2);

        var changed = _harness.UnitOfWork.Events.OfType<ShowChangedDomainEvent>()
            .ShouldHaveSingleItem("a edição não levantou ShowChanged");
        changed.ShowId.ShouldBe(show.Id);
        changed.Status.ShouldBe(ShowStatuses.Scheduled);
        changed.StartsAtUtc.ShouldBe(new DateTimeOffset(2026, 11, 22, 0, 0, 0, TimeSpan.Zero));
        _harness.UnitOfWork.Events.OfType<ShowPublishedDomainEvent>().Count().ShouldBe(1, "editar não publica o show de novo");
    }

    [Fact]
    [Trait("Spec", "SPEC-0023:UT-03")]
    public async Task AnArtistCannotCreateAShowForAProfileOwnedBySomeoneElse()
    {
        var result = await CreateAsync(ShowsHarness.NoiteDaPeste(TestProfiles.OwnerA, TestProfiles.ProducerB.Id));

        result.IsFailure.ShouldBeTrue("o dono do perfil A criou show do perfil B");
        result.Error.Code.ShouldBe("auth.forbidden");
        result.Error.Type.ShouldBe(ErrorType.Forbidden);
        _harness.Shows.All.ShouldBeEmpty("nada é gravado");
        _harness.Flyers.Attached.ShouldBeEmpty("o flyer não é ligado");
        _harness.UnitOfWork.Events.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Spec", "SPEC-0023:UT-03")]
    public async Task AnArtistCannotCreateAShowForAnOwnerlessProfile()
    {
        var result = await CreateAsync(ShowsHarness.NoiteDaPeste(TestProfiles.OwnerA, TestProfiles.Ownerless.Id));

        result.IsFailure.ShouldBeTrue("um artista criou show de um perfil sem dono");
        result.Error.Code.ShouldBe("auth.forbidden");
        _harness.Shows.All.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Spec", "SPEC-0023:UT-03")]
    public async Task AnArtistCannotEditOrChangeTheStatusOfAShowOfAnotherProfile()
    {
        var command = ShowsHarness.NoiteDaPeste(TestProfiles.OwnerB, TestProfiles.ProducerB.Id);
        var created = await CreateAsync(command);
        created.IsSuccess.ShouldBeTrue(created.IsFailure ? created.Error.Code : null);
        var before = _harness.Shows.All.ShouldHaveSingleItem().StartsAtUtc;
        var events = _harness.UnitOfWork.Events.Count;

        var edit = await UpdateAsync(Edit(created.Value.Id, command, userId: TestProfiles.OwnerA, startsLocal: "2026-11-21T21:00"));
        var status = await _harness.Create<ChangeShowStatusCommandHandler>().Handle(
            new ChangeShowStatusCommand(created.Value.Id, TestProfiles.OwnerA, IsAdmin: false, ShowStatuses.Cancelled), Ct);

        edit.IsFailure.ShouldBeTrue("o dono do perfil A editou show do perfil B");
        edit.Error.Code.ShouldBe("auth.forbidden");
        status.IsFailure.ShouldBeTrue("o dono do perfil A cancelou show do perfil B");
        status.Error.Code.ShouldBe("auth.forbidden");
        var show = _harness.Shows.All.ShouldHaveSingleItem();
        show.StartsAtUtc.ShouldBe(before, "a edição negada mudou o show");
        show.Status.ShouldBe(ShowStatuses.Scheduled, "a mudança de situação negada mudou o show");
        _harness.UnitOfWork.Events.Count.ShouldBe(events, "a mudança negada levantou evento");
    }

    [Fact]
    [Trait("Spec", "SPEC-0023:UT-03")]
    public async Task TheAdminCreatesForAnOwnerlessProfileAndEditsAShowOfAnyProfile()
    {
        var ownerless = await CreateAsync(ShowsHarness.NoiteDaPeste(TestProfiles.Admin, TestProfiles.Ownerless.Id, isAdmin: true));
        var other = ShowsHarness.NoiteDaPeste(TestProfiles.OwnerB, TestProfiles.ProducerB.Id, title: "Caos Absoluto Fest", flyerId: ShowsHarness.OtherFlyerId);
        var created = await CreateAsync(other);
        created.IsSuccess.ShouldBeTrue(created.IsFailure ? created.Error.Code : null);

        var edit = await UpdateAsync(Edit(created.Value.Id, other, userId: TestProfiles.Admin, isAdmin: true, startsLocal: "2026-11-28T20:00"));

        ownerless.IsSuccess.ShouldBeTrue(ownerless.IsFailure ? ownerless.Error.Code : "o admin não criou show de perfil sem dono");
        _harness.Shows.All.Single(show => show.Id == ownerless.Value.Id).OrganizerProfileId.ShouldBe(TestProfiles.Ownerless.Id);
        _harness.Shows.All.Single(show => show.Id == ownerless.Value.Id).CreatedBy.ShouldBe(TestProfiles.Admin);
        edit.IsSuccess.ShouldBeTrue(edit.IsFailure ? edit.Error.Code : "o admin não editou show de outro perfil");
        _harness.Shows.All.Single(show => show.Id == created.Value.Id).StartsAtUtc
            .ShouldBe(new DateTimeOffset(2026, 11, 28, 23, 0, 0, TimeSpan.Zero));
    }
}
