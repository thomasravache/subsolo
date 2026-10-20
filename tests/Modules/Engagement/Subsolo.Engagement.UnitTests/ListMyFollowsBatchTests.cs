using Subsolo.Engagement.UnitTests.Infrastructure;
using Subsolo.Modules.Engagement.Application.Features.ListMyFollows;
using Subsolo.Modules.Engagement.Domain.Follows;

namespace Subsolo.Engagement.UnitTests;

/// <summary>
/// SPEC-0029 H27-06 (revisão da SPEC-0027) — a lista "Seguindo" busca os perfis seguidos em lote
/// (<c>IProfilesApi.GetManyAsync</c>, uma chamada só), não um <c>GetAsync</c> por perfil; perfis que não existem mais
/// ficam de fora e a cidade continua com o nome do catálogo.
/// </summary>
public sealed class ListMyFollowsBatchTests
{
    private static readonly Guid Fan = Guid.CreateVersion7();

    [Fact]
    [Trait("Spec", "SPEC-0029:H27-06")]
    public async Task TheFollowedProfilesAreReadInOneBatch()
    {
        var harness = new EngagementHarness();
        var a = harness.Profiles.AddApproved("Podridão", "podridao");
        var b = harness.Profiles.AddApproved("Cova Rasa", "cova-rasa");
        var c = harness.Profiles.AddApproved("Porão do Rock", "porao-do-rock", "venue");
        var gone = Guid.CreateVersion7();
        harness.AddFollow(Fan, FollowTarget.City("curitiba"));
        harness.AddFollow(Fan, FollowTarget.Profile(a.Id));
        harness.AddFollow(Fan, FollowTarget.Profile(b.Id));
        harness.AddFollow(Fan, FollowTarget.Profile(c.Id));
        harness.AddFollow(Fan, FollowTarget.Profile(gone));
        harness.AddFollow(Guid.CreateVersion7(), FollowTarget.Profile(Guid.CreateVersion7()));

        var result = await harness.Create<ListMyFollowsQueryHandler>()
            .Handle(new ListMyFollowsQuery(Fan), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        result.Value.Profiles.Select(profile => profile.Id).ShouldBe([a.Id, b.Id, c.Id], ignoreOrder: true);
        result.Value.Cities.ShouldHaveSingleItem().ShouldBe(new FollowedCityResponse("curitiba", "Curitiba"));
        harness.Profiles.Queries.ShouldBeEmpty(
            $"a lista consultou o IProfilesApi um perfil por vez ({harness.Profiles.Queries.Count} chamadas a GetAsync)");
        harness.Profiles.ManyQueries.ShouldHaveSingleItem("os perfis deveriam vir numa busca em lote só")
            .ShouldBe([a.Id, b.Id, c.Id, gone], ignoreOrder: true);
    }

    [Fact]
    [Trait("Spec", "SPEC-0029:H27-06")]
    public async Task WithoutFollowedProfilesNoProfileIsRead()
    {
        var harness = new EngagementHarness();
        harness.AddFollow(Fan, FollowTarget.City("curitiba"));

        var result = await harness.Create<ListMyFollowsQueryHandler>()
            .Handle(new ListMyFollowsQuery(Fan), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Profiles.ShouldBeEmpty();
        harness.Profiles.Queries.ShouldBeEmpty();
        harness.Profiles.ManyQueries.Where(ids => ids.Count > 0).ShouldBeEmpty();
    }
}
