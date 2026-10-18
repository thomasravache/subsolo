using Microsoft.EntityFrameworkCore;
using Subsolo.Common.Infrastructure.Database;
using Subsolo.Modules.Profiles.Application;
using Subsolo.Modules.Profiles.Domain.Profiles;
using Subsolo.Modules.Profiles.Domain.Requests;

namespace Subsolo.Modules.Profiles.Infrastructure;

/// <summary>
/// SPEC-0021 — DbContext do módulo Profiles, no schema <see cref="SchemaName"/>; é também a unidade de trabalho do
/// módulo (os eventos de domínio vão para a outbox no mesmo <c>SaveChanges</c>).
/// </summary>
public sealed class ProfilesDbContext(DbContextOptions<ProfilesDbContext> options)
    : ModuleDbContext(options, SchemaName), IProfilesUnitOfWork
{
    public const string SchemaName = "profiles";

    public DbSet<Profile> Profiles => Set<Profile>();

    public DbSet<ProfileRequest> ProfileRequests => Set<ProfileRequest>();

    async Task IProfilesUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken) =>
        await SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProfilesDbContext).Assembly);
    }
}
