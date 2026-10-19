using Subsolo.Engagement.UnitTests.Infrastructure;
using Subsolo.Modules.Engagement.Application.Features.GetFollowers;
using Subsolo.Modules.Engagement.Domain.Follows;
using Subsolo.Modules.Engagement.IntegrationEvents;

namespace Subsolo.Engagement.UnitTests;

/// <summary>
/// SPEC-0027 UT-02 — <see cref="IEngagementApi.GetFollowersAsync"/> sobre os follows: dados três usuários (um segue a
/// cidade <c>curitiba</c>, outro o perfil A, outro os dois), <c>GetFollowersAsync("curitiba", [A, B])</c> devolve os três
/// ids sem repetição; quem segue outra cidade ou outro perfil fica de fora.
/// </summary>
public sealed class GetFollowersTests
{
    private static readonly Guid ProfileA = Guid.CreateVersion7();
    private static readonly Guid ProfileB = Guid.CreateVersion7();
    private static readonly Guid ProfileC = Guid.CreateVersion7();

    private static Task<IReadOnlyCollection<Guid>> GetFollowersAsync(
        EngagementHarness harness, string city, IReadOnlyCollection<Guid> profiles) =>
        harness.Create<EngagementApi>().GetFollowersAsync(city, profiles, TestContext.Current.CancellationToken);

    [Fact]
    [Trait("Spec", "SPEC-0027:UT-02")]
    public async Task ReturnsEveryoneWhoFollowsTheCityOrAnyProfileOnce()
    {
        var harness = new EngagementHarness();
        var cityFan = Guid.CreateVersion7();
        var profileFan = Guid.CreateVersion7();
        var bothFan = Guid.CreateVersion7();
        harness.AddFollow(cityFan, FollowTarget.City("curitiba"));
        harness.AddFollow(profileFan, FollowTarget.Profile(ProfileA));
        harness.AddFollow(bothFan, FollowTarget.City("curitiba"));
        harness.AddFollow(bothFan, FollowTarget.Profile(ProfileA));

        var followers = await GetFollowersAsync(harness, "curitiba", [ProfileA, ProfileB]);

        followers.ShouldBe([cityFan, profileFan, bothFan], ignoreOrder: true);
        followers.Count.ShouldBe(3, $"ids repetidos: {string.Join(", ", followers)}");
    }

    [Fact]
    [Trait("Spec", "SPEC-0027:UT-02")]
    public async Task FollowersOfOtherCitiesAndProfilesAreLeftOut()
    {
        var harness = new EngagementHarness();
        var cityFan = Guid.CreateVersion7();
        var otherCityFan = Guid.CreateVersion7();
        var otherProfileFan = Guid.CreateVersion7();
        var bFan = Guid.CreateVersion7();
        harness.AddFollow(cityFan, FollowTarget.City("curitiba"));
        harness.AddFollow(otherCityFan, FollowTarget.City("sao-paulo"));
        harness.AddFollow(otherProfileFan, FollowTarget.Profile(ProfileC));
        harness.AddFollow(bFan, FollowTarget.Profile(ProfileB));

        var followers = await GetFollowersAsync(harness, "curitiba", [ProfileA, ProfileB]);

        followers.ShouldBe([cityFan, bFan], ignoreOrder: true);
    }

    [Fact]
    [Trait("Spec", "SPEC-0027:UT-02")]
    public async Task OneFanFollowingTheCityAndTwoProfilesAppearsOnce()
    {
        var harness = new EngagementHarness();
        var fan = Guid.CreateVersion7();
        harness.AddFollow(fan, FollowTarget.City("curitiba"));
        harness.AddFollow(fan, FollowTarget.Profile(ProfileA));
        harness.AddFollow(fan, FollowTarget.Profile(ProfileB));

        var followers = await GetFollowersAsync(harness, "curitiba", [ProfileA, ProfileB]);

        followers.ShouldHaveSingleItem().ShouldBe(fan);
    }

    [Fact]
    [Trait("Spec", "SPEC-0027:UT-02")]
    public async Task WithoutProfilesOnlyTheCityFollowersAreReturned()
    {
        var harness = new EngagementHarness();
        var cityFan = Guid.CreateVersion7();
        harness.AddFollow(cityFan, FollowTarget.City("curitiba"));
        harness.AddFollow(Guid.CreateVersion7(), FollowTarget.Profile(ProfileA));

        var followers = await GetFollowersAsync(harness, "curitiba", []);

        followers.ShouldHaveSingleItem().ShouldBe(cityFan);
    }

    [Fact]
    [Trait("Spec", "SPEC-0027:UT-02")]
    public async Task NobodyFollowingGivesAnEmptyCollection()
    {
        var harness = new EngagementHarness();
        harness.AddFollow(Guid.CreateVersion7(), FollowTarget.City("sao-paulo"));

        var followers = await GetFollowersAsync(harness, "curitiba", [ProfileA]);

        followers.ShouldBeEmpty();
    }
}
