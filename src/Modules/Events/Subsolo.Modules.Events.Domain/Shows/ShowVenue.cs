namespace Subsolo.Modules.Events.Domain.Shows;

/// <summary>SPEC-0023 — a casa do show: um perfil de casa (<paramref name="ProfileId"/>) ou só o nome livre (2–80).</summary>
public sealed record ShowVenue(Guid? ProfileId, string Name);

/// <summary>SPEC-0023 — uma banda do line-up: um perfil de banda (<paramref name="ProfileId"/>) ou só o nome livre (1–80).</summary>
public sealed record ShowLineupItem(Guid? ProfileId, string Name);
