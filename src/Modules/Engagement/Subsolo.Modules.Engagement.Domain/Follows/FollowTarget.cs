using System.Globalization;

namespace Subsolo.Modules.Engagement.Domain.Follows;

/// <summary>
/// SPEC-0027 — o alvo de um follow: <see cref="FollowTargetKinds.City"/> com o slug da cidade ou
/// <see cref="FollowTargetKinds.Profile"/> com o id do perfil (formato <c>D</c>, minúsculo).
/// </summary>
public sealed record FollowTarget(string Kind, string Key)
{
    /// <summary>A cidade <paramref name="slug"/>.</summary>
    public static FollowTarget City(string slug) => new(FollowTargetKinds.City, slug);

    /// <summary>O perfil <paramref name="profileId"/>.</summary>
    public static FollowTarget Profile(Guid profileId) =>
        new(FollowTargetKinds.Profile, profileId.ToString("D", CultureInfo.InvariantCulture));
}
