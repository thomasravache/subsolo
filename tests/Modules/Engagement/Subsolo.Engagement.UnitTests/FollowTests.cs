using Subsolo.Common.Domain;
using Subsolo.Engagement.UnitTests.Infrastructure;
using Subsolo.Modules.Engagement.Application;
using Subsolo.Modules.Engagement.Application.Features.FollowCity;
using Subsolo.Modules.Engagement.Application.Features.FollowProfile;
using Subsolo.Modules.Engagement.Domain.Follows;

namespace Subsolo.Engagement.UnitTests;

/// <summary>
/// SPEC-0027 UT-01 — os handlers de <c>FollowCity</c> e <c>FollowProfile</c> com o catálogo e os perfis de teste: o fã
/// segue a cidade <c>curitiba</c> e o perfil aprovado X e os follows são gravados (com a data do relógio); seguir de novo
/// dá sucesso sem duplicar; cidade inexistente, perfil pendente (ainda não é perfil) ou inexistente →
/// <c>engagement.target_not_found</c>, sem gravar nada.
/// </summary>
public sealed class FollowTests
{
    private static readonly Guid Fan = Guid.CreateVersion7();

    private static Task<Result> FollowCityAsync(EngagementHarness harness, string slug, Guid? userId = null) =>
        harness.Create<FollowCityCommandHandler>()
            .Handle(new FollowCityCommand(userId ?? Fan, slug), TestContext.Current.CancellationToken);

    private static Task<Result> FollowProfileAsync(EngagementHarness harness, Guid profileId, Guid? userId = null) =>
        harness.Create<FollowProfileCommandHandler>()
            .Handle(new FollowProfileCommand(userId ?? Fan, profileId), TestContext.Current.CancellationToken);

    [Fact]
    [Trait("Spec", "SPEC-0027:UT-01")]
    public async Task FollowingAnExistingCityCreatesTheFollow()
    {
        var harness = new EngagementHarness();

        var result = await FollowCityAsync(harness, "curitiba");

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        var follow = harness.Follows.All.ShouldHaveSingleItem("o follow da cidade não foi gravado");
        follow.UserId.ShouldBe(Fan);
        follow.TargetKind.ShouldBe(FollowTargetKinds.City);
        follow.TargetKey.ShouldBe("curitiba");
        follow.CreatedAtUtc.ShouldBe(harness.Clock.GetUtcNow());
        harness.Catalog.CityQueries.ShouldContain("curitiba", "a cidade não foi validada pelo IEventsCatalog");
    }

    [Fact]
    [Trait("Spec", "SPEC-0027:UT-01")]
    public async Task FollowingAnApprovedProfileCreatesTheFollow()
    {
        var harness = new EngagementHarness();
        var profile = harness.Profiles.AddApproved();

        var result = await FollowProfileAsync(harness, profile.Id);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        var follow = harness.Follows.All.ShouldHaveSingleItem("o follow do perfil não foi gravado");
        follow.UserId.ShouldBe(Fan);
        follow.TargetKind.ShouldBe(FollowTargetKinds.Profile);
        follow.TargetKey.ShouldBe(profile.Id.ToString("D"));
        follow.CreatedAtUtc.ShouldBe(harness.Clock.GetUtcNow());
        harness.Profiles.Queries.ShouldContain(profile.Id, "o perfil não foi validado pelo IProfilesApi");
    }

    [Fact]
    [Trait("Spec", "SPEC-0027:UT-01")]
    public async Task FollowingTheCityAndTheProfileCreatesTwoFollows()
    {
        var harness = new EngagementHarness();
        var profile = harness.Profiles.AddApproved();

        (await FollowCityAsync(harness, "curitiba")).IsSuccess.ShouldBeTrue();
        (await FollowProfileAsync(harness, profile.Id)).IsSuccess.ShouldBeTrue();

        harness.Follows.All.Select(follow => follow.Target).ShouldBe(
            [FollowTarget.City("curitiba"), FollowTarget.Profile(profile.Id)],
            ignoreOrder: true);
    }

    [Fact]
    [Trait("Spec", "SPEC-0027:UT-01")]
    public async Task FollowingTheCityAgainSucceedsWithoutDuplicating()
    {
        var harness = new EngagementHarness();
        (await FollowCityAsync(harness, "curitiba")).IsSuccess.ShouldBeTrue();
        var first = harness.Follows.All.ShouldHaveSingleItem();
        harness.Clock.Advance(TimeSpan.FromHours(1));

        var again = await FollowCityAsync(harness, "curitiba");

        again.IsSuccess.ShouldBeTrue(again.IsFailure ? again.Error.Code : "seguir de novo deveria dar sucesso (idempotente)");
        harness.Follows.All.ShouldHaveSingleItem("seguir de novo duplicou o follow").ShouldBeSameAs(first);
        first.CreatedAtUtc.ShouldBe(EngagementHarness.Start, "seguir de novo não muda a data do follow");
    }

    [Fact]
    [Trait("Spec", "SPEC-0027:UT-01")]
    public async Task FollowingTheProfileAgainSucceedsWithoutDuplicating()
    {
        var harness = new EngagementHarness();
        var profile = harness.Profiles.AddApproved();
        (await FollowProfileAsync(harness, profile.Id)).IsSuccess.ShouldBeTrue();

        var again = await FollowProfileAsync(harness, profile.Id);

        again.IsSuccess.ShouldBeTrue(again.IsFailure ? again.Error.Code : "seguir de novo deveria dar sucesso (idempotente)");
        harness.Follows.All.ShouldHaveSingleItem("seguir de novo duplicou o follow");
    }

    [Fact]
    [Trait("Spec", "SPEC-0027:UT-01")]
    public async Task AnotherFanFollowingTheSameCityGetsTheirOwnFollow()
    {
        var harness = new EngagementHarness();
        var other = Guid.CreateVersion7();

        (await FollowCityAsync(harness, "curitiba")).IsSuccess.ShouldBeTrue();
        (await FollowCityAsync(harness, "curitiba", other)).IsSuccess.ShouldBeTrue();

        harness.Follows.All.Select(follow => follow.UserId).ShouldBe([Fan, other], ignoreOrder: true);
    }

    [Theory]
    [Trait("Spec", "SPEC-0027:UT-01")]
    [InlineData("cidade-que-nao-existe")]
    [InlineData("porto-alegre")]
    public async Task UnknownCityIsTargetNotFound(string slug)
    {
        var harness = new EngagementHarness();

        var result = await FollowCityAsync(harness, slug);

        result.IsFailure.ShouldBeTrue($"seguir a cidade inexistente {slug} deveria falhar");
        result.Error.Code.ShouldBe(EngagementErrors.TargetNotFound);
        result.Error.Code.ShouldBe("engagement.target_not_found");
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        harness.Follows.All.ShouldBeEmpty("nada é gravado para alvo inválido");
    }

    [Fact]
    [Trait("Spec", "SPEC-0027:UT-01")]
    public async Task PendingProfileIsTargetNotFound()
    {
        var harness = new EngagementHarness();
        // Um pedido pendente ainda não é perfil (SPEC-0021): o IProfilesApi não o encontra.
        var pendingRequestId = Guid.CreateVersion7();

        var result = await FollowProfileAsync(harness, pendingRequestId);

        result.IsFailure.ShouldBeTrue("seguir um perfil pendente deveria falhar");
        result.Error.Code.ShouldBe(EngagementErrors.TargetNotFound);
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        harness.Follows.All.ShouldBeEmpty("nada é gravado para alvo inválido");
    }

    [Fact]
    [Trait("Spec", "SPEC-0027:UT-01")]
    public async Task UnknownProfileIsTargetNotFound()
    {
        var harness = new EngagementHarness();
        harness.Profiles.AddApproved();

        var result = await FollowProfileAsync(harness, Guid.CreateVersion7());

        result.IsFailure.ShouldBeTrue("seguir um perfil inexistente deveria falhar");
        result.Error.Code.ShouldBe(EngagementErrors.TargetNotFound);
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        harness.Follows.All.ShouldBeEmpty("nada é gravado para alvo inválido");
    }
}
