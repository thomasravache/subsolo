using Microsoft.EntityFrameworkCore;
using Npgsql;
using Subsolo.Common.Infrastructure.Database;
using Subsolo.Modules.Events.Application;
using Subsolo.Modules.Events.Domain.Cities;
using Subsolo.Modules.Events.Domain.Styles;

namespace Subsolo.Modules.Events.Infrastructure;

/// <summary>SPEC-0008 — DbContext do módulo Events, no schema <see cref="SchemaName"/>.</summary>
public sealed class EventsDbContext(DbContextOptions<EventsDbContext> options)
    : ModuleDbContext(options, SchemaName), IEventsUnitOfWork
{
    public const string SchemaName = "events";

    /// <summary>Índices únicos dos slugs (nomes gerados pelo EF nas migrações).</summary>
    private const string CitySlugIndex = "IX_cities_slug";

    private const string StyleSlugIndex = "IX_styles_slug";

    public DbSet<City> Cities => Set<City>();

    /// <summary>SPEC-0017 — estilos de metal (os iniciais vêm da migração de dados).</summary>
    public DbSet<Style> Styles => Set<Style>();

    /// <summary>
    /// SPEC-0017 — unidade de trabalho do módulo: grava as mudanças rastreadas numa transação. Emenda 2: violação de
    /// unicidade (23505) num slug de cidade ou estilo vira <see cref="DuplicateSlugException"/>.
    /// </summary>
    async Task IEventsUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: CitySlugIndex or StyleSlugIndex,
        })
        {
            ChangeTracker.Clear();
            throw new DuplicateSlugException("Slug já gravado por outro pedido.", exception);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EventsDbContext).Assembly);
    }
}
