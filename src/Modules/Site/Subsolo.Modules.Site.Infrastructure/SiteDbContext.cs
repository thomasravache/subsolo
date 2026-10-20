using Microsoft.EntityFrameworkCore;
using Subsolo.Common.Infrastructure.Database;
using Subsolo.Modules.Site.Application;
using Subsolo.Modules.Site.Domain.Supporters;

namespace Subsolo.Modules.Site.Infrastructure;

/// <summary>SPEC-0028 — DbContext do módulo Site, no schema <see cref="SchemaName"/>; é também a unidade de trabalho do módulo.</summary>
public sealed class SiteDbContext(DbContextOptions<SiteDbContext> options)
    : ModuleDbContext(options, SchemaName), ISiteUnitOfWork
{
    public const string SchemaName = "site";

    public DbSet<Supporter> Supporters => Set<Supporter>();

    Task ISiteUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken) => SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SiteDbContext).Assembly);
    }
}
