using Subsolo.Common.Domain;

namespace Subsolo.Modules.Engagement.Domain.Follows;

/// <summary>
/// SPEC-0027 — o usuário <see cref="UserId"/> segue o alvo (<see cref="TargetKind"/>, <see cref="TargetKey"/>) desde
/// <see cref="CreatedAtUtc"/>. Chave: (usuário, tipo do alvo, chave do alvo) — um follow só por usuário e alvo.
/// </summary>
public sealed class Follow : Entity
{
    private Follow(Guid userId, string targetKind, string targetKey, DateTimeOffset createdAtUtc)
    {
        UserId = userId;
        TargetKind = targetKind;
        TargetKey = targetKey;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid UserId { get; private set; }

    public string TargetKind { get; private set; }

    public string TargetKey { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>O alvo do follow.</summary>
    public FollowTarget Target => new(TargetKind, TargetKey);

    /// <summary>Um follow novo de <paramref name="userId"/> para <paramref name="target"/>.</summary>
    public static Follow Create(Guid userId, FollowTarget target, DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(target);
        return new Follow(userId, target.Kind, target.Key, createdAtUtc);
    }
}
