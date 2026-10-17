using System.Diagnostics;
using System.Diagnostics.Metrics;
using Subsolo.Modules.Users.Domain.Sessions;

namespace Subsolo.Modules.Users.Application.Diagnostics;

/// <summary>
/// SPEC-0014 — métricas do módulo Users (Plano de Rollout): <c>subsolo.users.sign_ins</c> (por método) e
/// <c>subsolo.users.sessions_revoked</c>. Sem dados pessoais nas tags.
/// </summary>
public static class UsersTelemetry
{
    /// <summary>Nome do <see cref="Meter"/> do módulo (assinado pelo host nas métricas).</summary>
    public const string MeterName = "Subsolo.Users";

    /// <summary>Tag com o método de entrada (<c>magic_link</c> | <c>google</c>).</summary>
    public const string MethodTag = "subsolo.users.method";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> SignIns = Meter.CreateCounter<long>(
        "subsolo.users.sign_ins", unit: "{sign_in}", description: "Entradas concluídas, por método.");

    private static readonly Counter<long> SessionsRevoked = Meter.CreateCounter<long>(
        "subsolo.users.sessions_revoked", unit: "{session}", description: "Sessões revogadas no servidor (logout).");

    internal static void SignedIn(SignInMethod method) =>
        SignIns.Add(1, new TagList { { MethodTag, SignInMethods.ToCode(method) } });

    internal static void SessionRevoked() => SessionsRevoked.Add(1);
}
