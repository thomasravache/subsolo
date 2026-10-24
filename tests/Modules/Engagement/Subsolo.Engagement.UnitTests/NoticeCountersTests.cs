using System.Diagnostics.Metrics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Subsolo.Modules.Engagement.Application.Diagnostics;
using Subsolo.Modules.Engagement.Application.Features.DailyDigest;

namespace Subsolo.Engagement.UnitTests;

/// <summary>
/// SPEC-0032 H31-05 (item herdado da revisão da SPEC-0031; contrato v2) — os contadores
/// <c>subsolo.engagement.digest_recipients</c> e <c>subsolo.engagement.new_show_notices</c> contam seguidores
/// considerados, inclusive em reexecuções: a descrição deles diz "seguidores considerados (inclui reexecuções)"; e o log
/// <c>DailyDigestCompleted</c> diz "fã(s) considerado(s)".
/// </summary>
public sealed class NoticeCountersTests
{
    private const string Considered = "seguidores considerados (inclui reexecuções)";

    [Theory]
    [Trait("Spec", "SPEC-0032:H31-05")]
    [InlineData("subsolo.engagement.digest_recipients")]
    [InlineData("subsolo.engagement.new_show_notices")]
    public void TheCounterDescriptionSaysFollowersConsideredIncludingReruns(string name)
    {
        var description = Description(name).ShouldNotBeNull($"o contador {name} não foi publicado pelo meter {EngagementTelemetry.MeterName}");

        description.ShouldContain(Considered, Case.Insensitive, $"descrição de {name}: \"{description}\"");
    }

    [Fact]
    [Trait("Spec", "SPEC-0032:H31-05")]
    public void TheDailyDigestCompletedLogSaysFansConsidered()
    {
        var message = typeof(SendDailyDigestCommandHandler)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Select(method => method.GetCustomAttribute<LoggerMessageAttribute>())
            .Where(attribute => attribute?.EventName == "DailyDigestCompleted")
            .ShouldHaveSingleItem("o handler do resumo não tem o log DailyDigestCompleted")!
            .Message;

        message.ShouldContain("fã(s) considerado(s)", Case.Sensitive, $"mensagem do DailyDigestCompleted: \"{message}\"");
    }

    /// <summary>A descrição do instrumento <paramref name="name"/> do meter do Engagement (ou nula, se não existe).</summary>
    private static string? Description(string name)
    {
        RuntimeHelpers.RunClassConstructor(typeof(EngagementTelemetry).TypeHandle);
        string? description = null;
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, _) =>
            {
                if (instrument.Meter.Name == EngagementTelemetry.MeterName && instrument.Name == name)
                {
                    description = instrument.Description;
                }
            },
        };
        listener.Start();
        return description;
    }
}
