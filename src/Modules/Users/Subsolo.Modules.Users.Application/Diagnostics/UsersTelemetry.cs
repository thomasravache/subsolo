using System.Diagnostics;
using System.Diagnostics.Metrics;
using Subsolo.Modules.Users.Domain.Sessions;

namespace Subsolo.Modules.Users.Application.Diagnostics;

/// <summary>
/// SPEC-0014 — métricas do módulo Users (Plano de Rollout): <c>subsolo.users.sign_ins</c> (por método) e
/// <c>subsolo.users.sessions_revoked</c>. SPEC-0016 — <c>subsolo.users.magic_links_requested</c> (resultado:
/// <c>sent</c>, <c>limited</c>, <c>failed</c>) e <c>subsolo.users.magic_links_consumed</c> (resultado: <c>ok</c>,
/// <c>invalid</c>). Sem dados pessoais nas tags.
/// </summary>
public static class UsersTelemetry
{
    /// <summary>Nome do <see cref="Meter"/> do módulo (assinado pelo host nas métricas).</summary>
    public const string MeterName = "Subsolo.Users";

    /// <summary>Tag com o método de entrada (<c>magic_link</c> | <c>google</c>).</summary>
    public const string MethodTag = "subsolo.users.method";

    /// <summary>SPEC-0016 — tag com o resultado do pedido ou do consumo do link mágico.</summary>
    public const string ResultTag = "subsolo.users.result";

    internal const string RequestSent = "sent";

    internal const string RequestLimited = "limited";

    internal const string RequestFailed = "failed";

    internal const string ConsumeOk = "ok";

    internal const string ConsumeInvalid = "invalid";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> SignIns = Meter.CreateCounter<long>(
        "subsolo.users.sign_ins", unit: "{sign_in}", description: "Entradas concluídas, por método.");

    private static readonly Counter<long> SessionsRevoked = Meter.CreateCounter<long>(
        "subsolo.users.sessions_revoked", unit: "{session}", description: "Sessões revogadas no servidor (logout).");

    private static readonly Counter<long> MagicLinksRequested = Meter.CreateCounter<long>(
        "subsolo.users.magic_links_requested", unit: "{magic_link}", description: "Pedidos de link mágico, por resultado.");

    private static readonly Counter<long> MagicLinksConsumed = Meter.CreateCounter<long>(
        "subsolo.users.magic_links_consumed", unit: "{magic_link}", description: "Consumos de link mágico, por resultado.");

    internal static void MagicLinkRequested(string result) =>
        MagicLinksRequested.Add(1, new TagList { { ResultTag, result } });

    internal static void MagicLinkConsumed(string result) =>
        MagicLinksConsumed.Add(1, new TagList { { ResultTag, result } });

    internal static void SignedIn(SignInMethod method) =>
        SignIns.Add(1, new TagList { { MethodTag, SignInMethods.ToCode(method) } });

    internal static void SessionRevoked() => SessionsRevoked.Add(1);
}
