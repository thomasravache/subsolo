using Subsolo.Engagement.UnitTests.Infrastructure;
using Subsolo.Modules.Engagement.Application.Features.GetFollowers;
using Subsolo.Modules.Engagement.Domain.Follows;

namespace Subsolo.Engagement.UnitTests;

/// <summary>
/// SPEC-0029 H27-09 (revisão da SPEC-0027) — follows de perfil que deixou de ser aprovado ou foi excluído (o
/// <c>IProfilesApi</c> não o encontra mais) não contam em <c>GetFollowersAsync</c>: quem só segue esse perfil não recebe
/// aviso; quem segue a cidade ou um perfil aprovado continua.
/// </summary>
public sealed class FollowersOfRemovedProfilesTests
{
    private static Task<IReadOnlyCollection<Guid>> GetFollowersAsync(
        EngagementHarness harness, string city, IReadOnlyCollection<Guid> profiles) =>
        harness.Create<EngagementApi>().GetFollowersAsync(city, profiles, TestContext.Current.CancellationToken);

    [Fact]
    [Trait("Spec", "SPEC-0029:H27-09")]
    public async Task FollowersOfAProfileThatIsNoLongerApprovedAreLeftOut()
    {
        var harness = new EngagementHarness();
        var approved = harness.Profiles.AddApproved();
        var removed = Guid.CreateVersion7(); // seguido quando aprovado; hoje o IProfilesApi não o encontra
        var approvedFan = Guid.CreateVersion7();
        var removedFan = Guid.CreateVersion7();
        harness.AddFollow(approvedFan, FollowTarget.Profile(approved.Id));
        harness.AddFollow(removedFan, FollowTarget.Profile(removed));

        var followers = await GetFollowersAsync(harness, "sao-paulo", [approved.Id, removed]);

        followers.ShouldBe([approvedFan], ignoreOrder: true);
    }

    [Fact]
    [Trait("Spec", "SPEC-0029:H27-09")]
    public async Task AFanOfTheRemovedProfileWhoFollowsTheCityStillCounts()
    {
        var harness = new EngagementHarness();
        var removed = Guid.CreateVersion7();
        var fan = Guid.CreateVersion7();
        harness.AddFollow(fan, FollowTarget.Profile(removed));
        harness.AddFollow(fan, FollowTarget.City("curitiba"));

        var followers = await GetFollowersAsync(harness, "curitiba", [removed]);

        followers.ShouldHaveSingleItem().ShouldBe(fan);
    }

    [Fact]
    [Trait("Spec", "SPEC-0029:H27-09")]
    public async Task OnlyRemovedProfilesGiveNoFollowers()
    {
        var harness = new EngagementHarness();
        var removed = Guid.CreateVersion7();
        harness.AddFollow(Guid.CreateVersion7(), FollowTarget.Profile(removed));

        var followers = await GetFollowersAsync(harness, "sao-paulo", [removed]);

        followers.ShouldBeEmpty();
    }
}
