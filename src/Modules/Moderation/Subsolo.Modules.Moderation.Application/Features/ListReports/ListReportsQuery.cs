using System.Text.Json.Serialization;
using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Moderation.Application.Features.ListReports;

/// <summary>
/// SPEC-0033 — <c>GET /api/v1/admin/reports</c>: os shows com denúncias pendentes, mais antigos primeiro (pela primeira
/// denúncia), sem quem denunciou.
/// </summary>
public sealed record ListReportsQuery : IQuery<IReadOnlyList<ReportQueueItem>>;

/// <summary>
/// SPEC-0033 — um show na fila (§6): título, cidade, slug, início e casa (do Events), as contagens por motivo, até três
/// detalhes mais recentes (texto puro, mostrado escapado) e a primeira denúncia. Nada de quem denunciou (ADR-0008).
/// </summary>
public sealed record ReportQueueItem(
    Guid ShowId,
    string Title,
    string CitySlug,
    string Slug,
    DateTimeOffset StartsAtUtc,
    string VenueName,
    ReportCountsResponse Counts,
    IReadOnlyList<string> LatestDetails,
    DateTimeOffset FirstReportedAtUtc);

/// <summary>SPEC-0033 — as denúncias pendentes do show por motivo (as chaves são os motivos do §6).</summary>
public sealed record ReportCountsResponse(
    [property: JsonPropertyName("fake")] int Fake,
    [property: JsonPropertyName("cancelled_unannounced")] int CancelledUnannounced,
    [property: JsonPropertyName("wrong_info")] int WrongInfo,
    [property: JsonPropertyName("other")] int Other);
