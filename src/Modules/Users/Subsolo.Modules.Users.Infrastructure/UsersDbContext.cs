using Microsoft.EntityFrameworkCore;
using Subsolo.Common.Infrastructure.Database;
using Subsolo.Modules.Users.Application;
using Subsolo.Modules.Users.Domain.Sessions;
using Subsolo.Modules.Users.Domain.Users;

namespace Subsolo.Modules.Users.Infrastructure;

/// <summary>
/// SPEC-0014 — DbContext do módulo Users, no schema <see cref="SchemaName"/>; é também a unidade de trabalho do módulo
/// (os eventos de domínio vão para a outbox no mesmo <c>SaveChanges</c>).
/// </summary>
public sealed class UsersDbContext(DbContextOptions<UsersDbContext> options)
    : ModuleDbContext(options, SchemaName), IUsersUnitOfWork
{
    public const string SchemaName = "users";

    public DbSet<User> Users => Set<User>();

    public DbSet<Session> Sessions => Set<Session>();

    Task IUsersUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken) => SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(UsersDbContext).Assembly);
    }
}
