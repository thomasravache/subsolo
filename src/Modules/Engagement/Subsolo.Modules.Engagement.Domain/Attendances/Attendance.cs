using Subsolo.Common.Domain;

namespace Subsolo.Modules.Engagement.Domain.Attendances;

/// <summary>
/// SPEC-0029 — o usuário <see cref="UserId"/> marcou "vou" no show <see cref="ShowId"/> em <see cref="CreatedAtUtc"/>.
/// Chave: (usuário, show) — um "vou" só por usuário e show.
/// </summary>
public sealed class Attendance : Entity
{
    private Attendance(Guid userId, Guid showId, DateTimeOffset createdAtUtc)
    {
        UserId = userId;
        ShowId = showId;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid UserId { get; private set; }

    public Guid ShowId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>Um "vou" novo de <paramref name="userId"/> no show <paramref name="showId"/>.</summary>
    public static Attendance Create(Guid userId, Guid showId, DateTimeOffset createdAtUtc) =>
        new(userId, showId, createdAtUtc);
}
