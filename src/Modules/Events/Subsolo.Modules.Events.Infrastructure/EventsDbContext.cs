using Microsoft.EntityFrameworkCore;
using Subsolo.Common.Infrastructure.Database;
using Subsolo.Modules.Events.Application;
using Subsolo.Modules.Events.Domain.Cities;

namespace Subsolo.Modules.Events.Infrastructure;

/// <summary>SPEC-0008 — DbContext do módulo Events, no schema <see cref="SchemaName"/>.</summary>
public sealed class EventsDbContext(DbContextOptions<EventsDbContext> options)
    : ModuleDbContext(options, SchemaName), IEventsUnitOfWork
{
    public const string SchemaName = "events";

    public DbSet<City> Cities => Set<City>();

    /// <summary>SPEC-0017 — unidade de trabalho do módulo (andaime do contrato).</summary>
    Task IEventsUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken) =>
        throw new NotImplementedException("SPEC-0017: IEventsUnitOfWork ainda não implementado.");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EventsDbContext).Assembly);
    }
}
