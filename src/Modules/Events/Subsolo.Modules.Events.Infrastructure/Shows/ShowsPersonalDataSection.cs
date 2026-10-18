using Microsoft.EntityFrameworkCore;
using Subsolo.Modules.Users.IntegrationEvents;

namespace Subsolo.Modules.Events.Infrastructure.Shows;

/// <summary>
/// SPEC-0023 — seção <see cref="SectionName"/> do export de dados pessoais (SPEC-0019): os shows que o titular cadastrou;
/// sem nenhum, a seção é omitida. Na exclusão da conta os shows ficam (são da cena; só o organizador, público, aparece
/// neles).
/// </summary>
internal sealed class ShowsPersonalDataSection(EventsDbContext db) : IPersonalDataSection
{
    public const string SectionName = "shows";

    public string Name => SectionName;

    public async Task<object?> ExportAsync(Guid userId, CancellationToken ct)
    {
        var shows = await db.Shows.AsNoTracking()
            .Where(show => show.CreatedBy == userId)
            .OrderBy(show => show.CreatedAtUtc)
            .ToListAsync(ct);
        if (shows.Count == 0)
        {
            return null;
        }

        return new ShowsExport(
        [
            .. shows.Select(show => new ExportedShow(
                show.Id,
                show.Slug,
                show.City,
                show.Title,
                show.StartsAtUtc,
                show.OrganizerProfileId,
                show.Venue.Name,
                [.. show.Lineup.Select(item => item.Name)],
                show.Styles,
                show.Price,
                show.TicketUrl,
                show.Status,
                show.CreatedAtUtc)),
        ]);
    }

    private sealed record ShowsExport(IReadOnlyList<ExportedShow> Shows);

    private sealed record ExportedShow(
        Guid Id,
        string Slug,
        string City,
        string Title,
        DateTimeOffset StartsAtUtc,
        Guid OrganizerProfileId,
        string VenueName,
        IReadOnlyList<string> Lineup,
        IReadOnlyList<string> Styles,
        string Price,
        string? TicketUrl,
        string Status,
        DateTimeOffset CreatedAtUtc);
}
