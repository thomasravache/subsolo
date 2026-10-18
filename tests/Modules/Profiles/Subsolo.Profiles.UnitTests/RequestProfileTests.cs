using Subsolo.Common.Domain;
using Subsolo.Modules.Profiles.Application;
using Subsolo.Modules.Profiles.Application.Features.RequestProfile;
using Subsolo.Modules.Profiles.Domain.Profiles;
using Subsolo.Modules.Profiles.Domain.Requests;
using Subsolo.Profiles.UnitTests.Infrastructure;

namespace Subsolo.Profiles.UnitTests;

/// <summary>
/// SPEC-0021 UT-01 — o handler de <c>RequestProfile</c> com o catálogo de teste: um fã pede "Podridão" (banda,
/// <c>curitiba</c>, <c>death</c>) e o pedido pendente é gravado com o slug <c>podridao</c>, o contato e nenhum perfil
/// ainda; com outro pedido dele em análise (novo ou reivindicação), <c>profiles.request_pending</c>; com a banda
/// <c>podridao</c> já existente, <c>profiles.name_taken</c> (o slug é único por tipo: uma casa com o mesmo nome passa).
/// </summary>
public sealed class RequestProfileTests
{
    private const string Contact = "@podridao no Instagram";

    private static readonly Guid Fan = Guid.CreateVersion7();

    private static RequestProfileCommand Podridao(Guid userId, string type = ProfileTypes.Band, string name = "Podridão") =>
        new(
            userId,
            type,
            name,
            "curitiba",
            type == ProfileTypes.Band ? ["death"] : [],
            new ProfileLinksInput("https://www.instagram.com/podridao", null, null),
            Contact);

    private static Task<Result<ProfileRequestCreated>> HandleAsync(ProfilesHarness harness, RequestProfileCommand command) =>
        harness.Create<RequestProfileCommandHandler>().Handle(command, TestContext.Current.CancellationToken);

    [Fact]
    [Trait("Spec", "SPEC-0021:UT-01")]
    public async Task ValidRequestIsSavedPendingWithTheSlugGeneratedFromTheName()
    {
        var harness = new ProfilesHarness();

        var result = await HandleAsync(harness, Podridao(Fan));

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        result.Value.Status.ShouldBe(ProfileRequestStatuses.Pending);
        var request = harness.Requests.All.ShouldHaveSingleItem("o pedido não foi gravado");
        request.Id.ShouldBe(result.Value.RequestId);
        request.Kind.ShouldBe(ProfileRequestKinds.New);
        request.Status.ShouldBe(ProfileRequestStatuses.Pending);
        request.RequesterUserId.ShouldBe(Fan);
        request.ContactOrProof.ShouldBe(Contact);
        request.CreatedAtUtc.ShouldBe(harness.Clock.GetUtcNow());
        request.DecidedAtUtc.ShouldBeNull();
        var draft = request.Draft.ShouldNotBeNull("pedido novo sem os dados do perfil");
        draft.Type.ShouldBe(ProfileTypes.Band);
        draft.Name.ShouldBe("Podridão");
        draft.Slug.ShouldBe("podridao", "slug gerado do nome, sem acento");
        draft.City.ShouldBe("curitiba");
        draft.Styles.ShouldBe(["death"]);
        draft.Links.Instagram.ShouldBe("https://www.instagram.com/podridao");
        harness.Profiles.All.ShouldBeEmpty("o perfil só nasce na aprovação");
        harness.UnitOfWork.Saves.ShouldBe(1, "o pedido é gravado numa transação");
        harness.UnitOfWork.Events.OfType<ProfileOwnerAssignedDomainEvent>().ShouldBeEmpty("pedir não dá dono a ninguém");
    }

    [Theory]
    [Trait("Spec", "SPEC-0021:UT-01")]
    [InlineData("Porão do Rock", "porao-do-rock")]
    [InlineData("Podridão", "podridao")]
    [InlineData("  Coágulo  Sônico ", "coagulo-sonico")]
    public async Task TheSlugIsGeneratedFromTheName(string name, string slug)
    {
        var harness = new ProfilesHarness();

        var result = await HandleAsync(harness, Podridao(Fan, ProfileTypes.Venue, name));

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        harness.Requests.All.ShouldHaveSingleItem().Draft.ShouldNotBeNull().Slug.ShouldBe(slug, $"nome \"{name}\"");
    }

    [Theory]
    [Trait("Spec", "SPEC-0021:UT-01")]
    [InlineData(ProfileRequestKinds.New)]
    [InlineData(ProfileRequestKinds.Claim)]
    public async Task AnotherPendingRequestOfTheUserReturnsRequestPending(string pendingKind)
    {
        var harness = new ProfilesHarness();
        if (pendingKind == ProfileRequestKinds.New)
        {
            harness.AddNewRequest(Fan, ProfilesHarness.Draft("Outra Banda", "outra-banda"));
        }
        else
        {
            var ownerless = harness.AddProfile(ProfilesHarness.Draft("Porão do Rock", "porao-do-rock", ProfileTypes.Venue));
            harness.AddClaim(Fan, ownerless.Id);
        }

        var saves = harness.UnitOfWork.Saves;

        var result = await HandleAsync(harness, Podridao(Fan));

        result.IsFailure.ShouldBeTrue("com outro pedido em análise, o segundo é recusado");
        result.Error.Code.ShouldBe(ProfilesErrors.RequestPending);
        result.Error.Type.ShouldBe(ErrorType.Conflict);
        harness.Requests.All.Count.ShouldBe(1, "nenhum pedido novo foi gravado");
        harness.UnitOfWork.Saves.ShouldBe(saves, "nada foi gravado");
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:UT-01")]
    public async Task APendingRequestOfAnotherUserDoesNotBlock()
    {
        var harness = new ProfilesHarness();
        harness.AddNewRequest(Guid.CreateVersion7(), ProfilesHarness.Draft("Outra Banda", "outra-banda"));

        var result = await HandleAsync(harness, Podridao(Fan));

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        harness.Requests.All.Count.ShouldBe(2);
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:UT-01")]
    public async Task ExistingBandWithTheSlugReturnsNameTaken()
    {
        var harness = new ProfilesHarness();
        harness.AddProfile(ProfilesHarness.Draft());
        var saves = harness.UnitOfWork.Saves;

        var result = await HandleAsync(harness, Podridao(Fan));

        result.IsFailure.ShouldBeTrue("já existe a banda podridao");
        result.Error.Code.ShouldBe(ProfilesErrors.NameTaken);
        result.Error.Type.ShouldBe(ErrorType.Conflict);
        harness.Requests.All.ShouldBeEmpty("nenhum pedido foi gravado");
        harness.UnitOfWork.Saves.ShouldBe(saves, "nada foi gravado");
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:UT-01")]
    public async Task TheSameNameInAnotherTypeIsAccepted()
    {
        var harness = new ProfilesHarness();
        harness.AddProfile(ProfilesHarness.Draft());

        var result = await HandleAsync(harness, Podridao(Fan, ProfileTypes.Venue));

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        harness.Requests.All.ShouldHaveSingleItem().Draft.ShouldNotBeNull().Slug.ShouldBe("podridao");
    }
}
