using Microsoft.EntityFrameworkCore;
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

    public DbSet<City> Cities => Set<City>();

    /// <summary>SPEC-0017 — estilos de metal (os iniciais vêm da migração de dados).</summary>
    public DbSet<Style> Styles => Set<Style>();

    /// <summary>SPEC-0017 — unidade de trabalho do módulo: grava as mudanças rastreadas numa transação.</summary>
    Task IEventsUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken) => SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EventsDbContext).Assembly);
    }
}
