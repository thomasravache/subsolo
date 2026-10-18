using Microsoft.EntityFrameworkCore;
using Npgsql;
using Subsolo.Common.Infrastructure.Database;
using Subsolo.Modules.Users.Application;
using Subsolo.Modules.Users.Application.Features.SignIn;
using Subsolo.Modules.Users.Domain.MagicLinks;
using Subsolo.Modules.Users.Domain.Sessions;
using Subsolo.Modules.Users.Domain.Users;

namespace Subsolo.Modules.Users.Infrastructure;

/// <summary>
/// SPEC-0014 — DbContext do módulo Users, no schema <see cref="SchemaName"/>; é também a unidade de trabalho do módulo
/// (os eventos de domínio vão para a outbox no mesmo <c>SaveChanges</c>). SPEC-0016 — links mágicos
/// (<see cref="MagicLinkTokens"/>); a unidade de trabalho traduz a violação do índice único do e-mail em
/// <see cref="UserEmailTakenException"/>, depois de descartar as mudanças pendentes.
/// </summary>
public sealed class UsersDbContext(DbContextOptions<UsersDbContext> options)
    : ModuleDbContext(options, SchemaName), IUsersUnitOfWork
{
    public const string SchemaName = "users";

    /// <summary>Índice único do e-mail normalizado em <c>users.users</c> (SPEC-0014).</summary>
    private const string UserEmailIndex = "IX_users_email";

    public DbSet<User> Users => Set<User>();

    public DbSet<Session> Sessions => Set<Session>();

    public DbSet<MagicLinkToken> MagicLinkTokens => Set<MagicLinkToken>();

    async Task IUsersUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUserEmailTaken(exception))
        {
            DiscardPendingChanges();
            throw new UserEmailTakenException(exception);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(UsersDbContext).Assembly);
    }

    private static bool IsUserEmailTaken(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } violation
        && string.Equals(violation.ConstraintName, UserEmailIndex, StringComparison.Ordinal);

    /// <summary>Desanexa as mudanças não gravadas (o usuário recusado, a sessão e as linhas da outbox dele).</summary>
    private void DiscardPendingChanges()
    {
        foreach (var entry in ChangeTracker.Entries()
                     .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
}
