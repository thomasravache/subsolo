using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Subsolo.Modules.Engagement.Application.Diagnostics;

/// <summary>
/// SPEC-0027 — métricas do módulo Engagement (Plano de Rollout): <c>subsolo.engagement.follows</c> e
/// <c>subsolo.engagement.unfollows</c>, por tipo de alvo (<c>city</c> | <c>profile</c>). Sem dados pessoais nas tags.
/// </summary>
public static class EngagementTelemetry
{
    /// <summary>Nome do <see cref="Meter"/> do módulo (assinado pelo host nas métricas).</summary>
    public const string MeterName = "Subsolo.Engagement";

    /// <summary>Tag com o tipo do alvo (<c>city</c> | <c>profile</c>).</summary>
    public const string TargetKindTag = "subsolo.engagement.target_kind";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> Follows = Meter.CreateCounter<long>(
        "subsolo.engagement.follows", unit: "{follow}", description: "Follows novos, por tipo de alvo.");

    private static readonly Counter<long> Unfollows = Meter.CreateCounter<long>(
        "subsolo.engagement.unfollows", unit: "{follow}", description: "Follows desfeitos, por tipo de alvo.");

    internal static void Followed(string targetKind) =>
        Follows.Add(1, new TagList { { TargetKindTag, targetKind } });

    internal static void Unfollowed(string targetKind) =>
        Unfollows.Add(1, new TagList { { TargetKindTag, targetKind } });
}
