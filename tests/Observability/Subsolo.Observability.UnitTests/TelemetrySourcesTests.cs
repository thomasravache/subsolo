using System.Diagnostics;
using Subsolo.Common.Application.Diagnostics;
using Subsolo.Common.Infrastructure.Messaging;

namespace Subsolo.Observability.UnitTests;

/// <summary>
/// SPEC-0009 (item herdado da revisão da base do monólito) — o tracing do ServiceDefaults assina as fontes próprias do
/// Subsolo: comandos e consultas (<see cref="ApplicationTelemetry.SourceName"/>) e outbox/inbox
/// (<see cref="MessagingTelemetry.SourceName"/>). Uma atividade de cada fonte chega ao exportador.
/// </summary>
public sealed class TelemetrySourcesTests
{
    [Theory]
    [Trait("Spec", "SPEC-0009:IT-01")]
    [InlineData(ApplicationTelemetry.SourceName)]
    [InlineData(MessagingTelemetry.SourceName)]
    public void ServiceDefaultsExportsActivitiesFromSubsoloSources(string sourceName)
    {
        using var telemetry = new ServiceDefaultsTelemetry();
        using var source = new ActivitySource(sourceName);
        string spanId;

        using (var activity = source.StartActivity($"SPEC-0009 fonte {sourceName}"))
        {
            activity.ShouldNotBeNull($"o ServiceDefaults não assina a fonte {sourceName}");
            spanId = activity.SpanId.ToHexString();
        }

        telemetry.Spans.ShouldContain(
            span => span.SpanId.ToHexString() == spanId && span.Source.Name == sourceName,
            $"a atividade da fonte {sourceName} não chegou ao exportador do ServiceDefaults");
    }
}
