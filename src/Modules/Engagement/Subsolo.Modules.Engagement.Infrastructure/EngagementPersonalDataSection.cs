using Subsolo.Modules.Engagement.Application;
using Subsolo.Modules.Engagement.Application.Features.ListMyFollows;
using Subsolo.Modules.Engagement.Domain.Follows;
using Subsolo.Modules.Events.IntegrationEvents;
using Subsolo.Modules.Profiles.IntegrationEvents;
using Subsolo.Modules.Users.IntegrationEvents;

namespace Subsolo.Modules.Engagement.Infrastructure;

/// <summary>
/// SPEC-0027 — seção <see cref="SectionName"/> do export de dados pessoais (SPEC-0019, ADR-0008): as cidades (slug e
/// nome) e os perfis (id, tipo, nome e slug) que o titular segue, com a data de cada follow; sem nada, a seção é omitida.
/// </summary>
internal sealed class EngagementPersonalDataSection(
    IFollowsReadModel follows,
    IEventsCatalogNames catalog,
    IProfilesApi profiles) : IPersonalDataSection
{
    public const string SectionName = "follows";

    public string Name => SectionName;

    public async Task<object?> ExportAsync(Guid userId, CancellationToken ct)
    {
        var mine = await follows.ListOfAsync(userId, ct);
        if (mine.Count == 0)
        {
            return null;
        }

        var described = await new FollowedTargets(catalog, profiles).DescribeAsync(mine, ct);
        var since = mine.ToDictionary(follow => follow.Target, follow => follow.CreatedAtUtc);
        return new FollowsExport(
            [
                .. described.Cities.Select(city => new ExportedCity(
                    city.Slug, city.Name, since[FollowTarget.City(city.Slug)])),
            ],
            [
                .. described.Profiles.Select(profile => new ExportedProfile(
                    profile.Id, profile.Type, profile.Name, profile.Slug, since[FollowTarget.Profile(profile.Id)])),
            ]);
    }

    private sealed record FollowsExport(IReadOnlyList<ExportedCity> Cities, IReadOnlyList<ExportedProfile> Profiles);

    private sealed record ExportedCity(string Slug, string Name, DateTimeOffset FollowedAtUtc);

    private sealed record ExportedProfile(Guid Id, string Type, string Name, string Slug, DateTimeOffset FollowedAtUtc);
}
