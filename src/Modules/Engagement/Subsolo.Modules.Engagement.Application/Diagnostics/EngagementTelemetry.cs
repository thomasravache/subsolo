using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Subsolo.Modules.Engagement.Application.Diagnostics;

/// <summary>
/// SPEC-0027 — métricas do módulo Engagement (Plano de Rollout): <c>subsolo.engagement.follows</c> e
/// <c>subsolo.engagement.unfollows</c>, por tipo de alvo (<c>city</c> | <c>profile</c>). SPEC-0029:
/// <c>subsolo.engagement.attendances</c>, por ação (<c>attend</c> | <c>unattend</c>). Sem dados pessoais nas tags.
/// </summary>
public static class EngagementTelemetry
{
    /// <summary>Nome do <see cref="Meter"/> do módulo (assinado pelo host nas métricas).</summary>
    public const string MeterName = "Subsolo.Engagement";

    /// <summary>Tag com o tipo do alvo (<c>city</c> | <c>profile</c>).</summary>
    public const string TargetKindTag = "subsolo.engagement.target_kind";

    /// <summary>SPEC-0029 — tag com a ação do "vou" (<c>attend</c> | <c>unattend</c>).</summary>
    public const string ActionTag = "subsolo.engagement.action";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> Follows = Meter.CreateCounter<long>(
        "subsolo.engagement.follows", unit: "{follow}", description: "Follows novos, por tipo de alvo.");

    private static readonly Counter<long> Unfollows = Meter.CreateCounter<long>(
        "subsolo.engagement.unfollows", unit: "{follow}", description: "Follows desfeitos, por tipo de alvo.");

    private static readonly Counter<long> Attendances = Meter.CreateCounter<long>(
        "subsolo.engagement.attendances", unit: "{attendance}", description: "\"Vou\" marcados e desmarcados, por ação.");

    internal static void Followed(string targetKind) =>
        Follows.Add(1, new TagList { { TargetKindTag, targetKind } });

    internal static void Unfollowed(string targetKind) =>
        Unfollows.Add(1, new TagList { { TargetKindTag, targetKind } });

    internal static void Attended() =>
        Attendances.Add(1, new TagList { { ActionTag, "attend" } });

    internal static void Unattended() =>
        Attendances.Add(1, new TagList { { ActionTag, "unattend" } });
}
