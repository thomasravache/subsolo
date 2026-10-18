using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Subsolo.Modules.Profiles.Application.Diagnostics;

/// <summary>
/// SPEC-0021 — métricas do módulo Profiles (Plano de Rollout): <c>subsolo.profiles.requests</c> (por tipo de perfil e
/// tipo de pedido) e <c>subsolo.profiles.decisions</c> (aprovado ou recusado). Sem dados pessoais nas tags.
/// </summary>
public static class ProfilesTelemetry
{
    /// <summary>Nome do <see cref="Meter"/> do módulo (assinado pelo host nas métricas).</summary>
    public const string MeterName = "Subsolo.Profiles";

    /// <summary>Tag com o tipo do perfil (<c>band</c> | <c>venue</c> | <c>producer</c>).</summary>
    public const string TypeTag = "subsolo.profiles.type";

    /// <summary>Tag com o tipo do pedido (<c>new</c> | <c>claim</c>).</summary>
    public const string KindTag = "subsolo.profiles.kind";

    /// <summary>Tag com a decisão (<c>approved</c> | <c>rejected</c>).</summary>
    public const string DecisionTag = "subsolo.profiles.decision";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> Requests = Meter.CreateCounter<long>(
        "subsolo.profiles.requests", unit: "{request}", description: "Pedidos de perfil e reivindicações, por tipo.");

    private static readonly Counter<long> Decisions = Meter.CreateCounter<long>(
        "subsolo.profiles.decisions", unit: "{decision}", description: "Decisões do admin sobre os pedidos, por decisão.");

    internal static void Requested(string type, string kind) =>
        Requests.Add(1, new TagList { { TypeTag, type }, { KindTag, kind } });

    internal static void Decided(string decision) =>
        Decisions.Add(1, new TagList { { DecisionTag, decision } });
}
