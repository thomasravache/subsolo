using Microsoft.EntityFrameworkCore;
using Npgsql;
using Subsolo.Common.Infrastructure.Database;
using Subsolo.Modules.Engagement.Application;
using Subsolo.Modules.Engagement.Domain.Follows;

namespace Subsolo.Modules.Engagement.Infrastructure;

/// <summary>
/// SPEC-0027 — DbContext do módulo Engagement, no schema <see cref="SchemaName"/>; é também a unidade de trabalho do
/// módulo. Um follow igual gravado por outra requisição no meio (violação da chave primária, 23505) vira
/// <see cref="FollowAlreadyExistsException"/>, com as mudanças descartadas.
/// </summary>
public sealed class EngagementDbContext(DbContextOptions<EngagementDbContext> options)
    : ModuleDbContext(options, SchemaName), IEngagementUnitOfWork
{
    public const string SchemaName = "engagement";

    /// <summary>Chave primária dos follows (usuário, tipo do alvo, chave do alvo).</summary>
    public const string FollowsPrimaryKey = "PK_follows";

    public DbSet<Follow> Follows => Set<Follow>();

    async Task IEngagementUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: FollowsPrimaryKey,
        })
        {
            ChangeTracker.Clear();
            throw new FollowAlreadyExistsException("O follow já existe.", exception);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EngagementDbContext).Assembly);
    }
}
