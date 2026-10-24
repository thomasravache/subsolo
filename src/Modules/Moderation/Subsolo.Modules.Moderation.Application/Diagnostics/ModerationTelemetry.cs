using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Subsolo.Modules.Moderation.Application.Diagnostics;

/// <summary>
/// SPEC-0033 — métricas do módulo Moderation (Plano de Rollout): <c>subsolo.moderation.reports</c> (por motivo) e
/// <c>subsolo.moderation.decisions</c> (manter ou ocultar). Sem dados pessoais nas tags.
/// </summary>
public static class ModerationTelemetry
{
    /// <summary>Nome do <see cref="Meter"/> do módulo (assinado pelo host nas métricas).</summary>
    public const string MeterName = "Subsolo.Moderation";

    /// <summary>Tag com o motivo (<c>fake</c> | <c>cancelled_unannounced</c> | <c>wrong_info</c> | <c>other</c>).</summary>
    public const string ReasonTag = "subsolo.moderation.reason";

    /// <summary>Tag com a decisão (<c>kept</c> | <c>hidden</c>).</summary>
    public const string DecisionTag = "subsolo.moderation.decision";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> Reports = Meter.CreateCounter<long>(
        "subsolo.moderation.reports", unit: "{report}", description: "Denúncias de show enviadas, por motivo.");

    private static readonly Counter<long> Decisions = Meter.CreateCounter<long>(
        "subsolo.moderation.decisions", unit: "{decision}", description: "Decisões do admin sobre shows denunciados, por decisão.");

    internal static void Reported(string reason) =>
        Reports.Add(1, new TagList { { ReasonTag, reason } });

    internal static void Decided(string decision) =>
        Decisions.Add(1, new TagList { { DecisionTag, decision } });
}
