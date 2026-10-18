using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Subsolo.Modules.Events.Application.Diagnostics;

/// <summary>
/// SPEC-0022 — métricas do módulo Events (Plano de Rollout): <c>subsolo.flyers.uploads</c> (por resultado) e
/// <c>subsolo.flyers.processing_duration</c> (segundos de processamento de cada flyer novo). SPEC-0023:
/// <c>subsolo.shows.created</c>, <c>subsolo.shows.status_changed</c> (por situação) e <c>subsolo.shows.create_duration</c>.
/// Sem nome de arquivo nem dado do usuário nas tags.
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

    /// <summary>SPEC-0023 — tag com a situação nova do show.</summary>
    public const string StatusTag = "subsolo.shows.status";

    private static readonly Counter<long> ShowsCreated = Meter.CreateCounter<long>(
        "subsolo.shows.created", unit: "{show}", description: "Shows cadastrados.");

    private static readonly Counter<long> ShowsStatusChanged = Meter.CreateCounter<long>(
        "subsolo.shows.status_changed", unit: "{show}", description: "Mudanças de situação de show, por situação nova.");

    private static readonly Histogram<double> ShowCreateDuration = Meter.CreateHistogram<double>(
        "subsolo.shows.create_duration", unit: "s", description: "Tempo do cadastro de um show (sem o envio do flyer).");

    internal static void ShowCreated(TimeSpan duration)
    {
        ShowsCreated.Add(1);
        ShowCreateDuration.Record(duration.TotalSeconds);
    }

    internal static void ShowStatusChanged(string status) => ShowsStatusChanged.Add(1, new TagList { { StatusTag, status } });

    internal static void Uploaded(string result) => Uploads.Add(1, new TagList { { ResultTag, result } });

    internal static void Processed(TimeSpan duration) => ProcessingDuration.Record(duration.TotalSeconds);
}
