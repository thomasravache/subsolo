using Microsoft.EntityFrameworkCore;
using Subsolo.Modules.Events.Domain.Styles;

namespace Subsolo.Modules.Events.Infrastructure.Styles;

/// <summary>SPEC-0017 — estilos no schema <c>events</c>.</summary>
internal sealed class StyleRepository(EventsDbContext db) : IStyleRepository
{
    /// <summary>Rastreado pelo DbContext: um <c>Rename</c> é gravado no próximo <c>SaveChangesAsync</c>.</summary>
    public Task<Style?> GetBySlugAsync(string slug, CancellationToken cancellationToken) =>
        db.Styles.SingleOrDefaultAsync(style => style.Slug == slug, cancellationToken);

    public async Task<IReadOnlyList<Style>> ListAsync(CancellationToken cancellationToken) =>
        await db.Styles.AsNoTracking()
            .OrderBy(style => style.Name)
            .ThenBy(style => style.Slug)
            .ToListAsync(cancellationToken);

    public void Add(Style style) => db.Styles.Add(style);
}
