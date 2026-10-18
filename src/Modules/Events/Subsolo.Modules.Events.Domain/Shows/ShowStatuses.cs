namespace Subsolo.Modules.Events.Domain.Shows;

/// <summary>SPEC-0023 — situação do show.</summary>
public static class ShowStatuses
{
    public const string Scheduled = "scheduled";

    public const string Cancelled = "cancelled";

    public const string Postponed = "postponed";

    /// <summary>As situações aceitas.</summary>
    public static readonly IReadOnlyList<string> All = [Scheduled, Cancelled, Postponed];
}
