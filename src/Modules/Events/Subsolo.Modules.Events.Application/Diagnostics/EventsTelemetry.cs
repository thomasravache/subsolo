using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Subsolo.Modules.Events.Application.Diagnostics;

/// <summary>
/// SPEC-0022 — métricas do módulo Events (Plano de Rollout): <c>subsolo.flyers.uploads</c> (por resultado) e
/// <c>subsolo.flyers.processing_duration</c> (segundos de processamento de cada flyer novo). Sem nome de arquivo nem
/// dado do usuário nas tags.
/// </summary>
public static class EventsTelemetry
{
    /// <summary>Nome do <see cref="Meter"/> do módulo (assinado pelo host nas métricas).</summary>
    public const string MeterName = "Subsolo.Events";

    /// <summary>Tag com o resultado do envio (<c>created</c>, <c>existing</c> ou o código do erro).</summary>
    public const string ResultTag = "subsolo.flyers.result";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> Uploads = Meter.CreateCounter<long>(
        "subsolo.flyers.uploads", unit: "{upload}", description: "Envios de flyer, por resultado.");

    private static readonly Histogram<double> ProcessingDuration = Meter.CreateHistogram<double>(
        "subsolo.flyers.processing_duration", unit: "s", description: "Tempo de processamento de um flyer novo.");

    internal static void Uploaded(string result) => Uploads.Add(1, new TagList { { ResultTag, result } });

    internal static void Processed(TimeSpan duration) => ProcessingDuration.Record(duration.TotalSeconds);
}
