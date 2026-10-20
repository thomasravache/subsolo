using Subsolo.Modules.Engagement.Application;
using Subsolo.Modules.Users.IntegrationEvents;

namespace Subsolo.Modules.Engagement.Infrastructure.Attendances;

/// <summary>
/// SPEC-0029 — seção <see cref="SectionName"/> do export de dados pessoais (SPEC-0019, ADR-0008): os shows em que o
/// titular marcou "vou" (id do show e data); sem nada, a seção é omitida.
/// </summary>
internal sealed class AttendancesPersonalDataSection(IAttendancesReadModel attendances) : IPersonalDataSection
{
    public const string SectionName = "attendances";

    public string Name => SectionName;

    public async Task<object?> ExportAsync(Guid userId, CancellationToken ct)
    {
        var mine = await attendances.ListOfAsync(userId, ct);
        return mine.Count == 0
            ? null
            : new AttendancesExport([.. mine.Select(attendance => new ExportedAttendance(attendance.ShowId, attendance.CreatedAtUtc))]);
    }

    private sealed record AttendancesExport(IReadOnlyList<ExportedAttendance> Shows);

    private sealed record ExportedAttendance(Guid ShowId, DateTimeOffset MarkedAtUtc);
}
