using Microsoft.EntityFrameworkCore;
using Npgsql;
using Subsolo.Common.Infrastructure.Database;
using Subsolo.Modules.Profiles.Application;
using Subsolo.Modules.Profiles.Domain.Profiles;
using Subsolo.Modules.Profiles.Domain.Requests;

namespace Subsolo.Modules.Profiles.Infrastructure;

/// <summary>
/// SPEC-0021 — DbContext do módulo Profiles, no schema <see cref="SchemaName"/>; é também a unidade de trabalho do
/// módulo (os eventos de domínio vão para a outbox no mesmo <c>SaveChanges</c>). Emenda 3: o token de concorrência
/// (<c>xmin</c>) de perfis e pedidos e as violações de unicidade (slug por tipo, um pedido pendente por usuário) viram
/// <see cref="ProfilesConflictException"/>, com as mudanças descartadas.
/// </summary>
public sealed class ProfilesDbContext(DbContextOptions<ProfilesDbContext> options)
    : ModuleDbContext(options, SchemaName), IProfilesUnitOfWork
{
    public const string SchemaName = "profiles";

    /// <summary>Índice único do slug por tipo (nome gerado pelo EF na migração).</summary>
    public const string ProfileSlugIndex = "IX_profiles_type_slug";

    /// <summary>Índice único parcial: um pedido pendente por usuário (Emenda 3).</summary>
    public const string PendingRequestIndex = "IX_profile_requests_pending_requester";

    public DbSet<Profile> Profiles => Set<Profile>();

    public DbSet<ProfileRequest> ProfileRequests => Set<ProfileRequest>();

    async Task IProfilesUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            var conflict = exception.Entries.Any(entry => entry.Entity is ProfileRequest)
                ? ProfilesConflict.RequestChanged
                : ProfilesConflict.ProfileChanged;
            ChangeTracker.Clear();
            throw new ProfilesConflictException(conflict, exception);
        }
        catch (DbUpdateException exception) when (UniqueConflict(exception) is { } conflict)
        {
            ChangeTracker.Clear();
            throw new ProfilesConflictException(conflict, exception);
        }
    }

    private static ProfilesConflict? UniqueConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } violation
            ? violation.ConstraintName switch
            {
                ProfileSlugIndex => ProfilesConflict.NameTaken,
                PendingRequestIndex => ProfilesConflict.RequestPending,
                _ => null,
            }
            : null;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProfilesDbContext).Assembly);
    }
}
