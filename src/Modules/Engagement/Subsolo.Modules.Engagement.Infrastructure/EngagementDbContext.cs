using Microsoft.EntityFrameworkCore;
using Npgsql;
using Subsolo.Common.Infrastructure.Database;
using Subsolo.Modules.Engagement.Application;
using Subsolo.Modules.Engagement.Domain.Attendances;
using Subsolo.Modules.Engagement.Domain.Follows;
using Subsolo.Modules.Engagement.Infrastructure.Notices;

namespace Subsolo.Modules.Engagement.Infrastructure;

/// <summary>
/// SPEC-0027 — DbContext do módulo Engagement, no schema <see cref="SchemaName"/>; é também a unidade de trabalho do
/// módulo. Um follow igual gravado por outra requisição no meio (violação da chave primária, 23505) vira
/// <see cref="FollowAlreadyExistsException"/>, com as mudanças descartadas; SPEC-0029: o mesmo para um "vou" igual
/// (<see cref="AttendanceAlreadyExistsException"/>).
/// </summary>
public sealed class EngagementDbContext(DbContextOptions<EngagementDbContext> options)
    : ModuleDbContext(options, SchemaName), IEngagementUnitOfWork
{
    public const string SchemaName = "engagement";

    /// <summary>Chave primária dos follows (usuário, tipo do alvo, chave do alvo).</summary>
    public const string FollowsPrimaryKey = "PK_follows";

    /// <summary>SPEC-0029 — chave primária dos "vou" (usuário, show).</summary>
    public const string AttendancesPrimaryKey = "PK_attendances";

    public DbSet<Follow> Follows => Set<Follow>();

    public DbSet<Attendance> Attendances => Set<Attendance>();

    /// <summary>SPEC-0031 — os shows publicados, base do resumo diário.</summary>
    internal DbSet<PublishedShowRow> PublishedShows => Set<PublishedShowRow>();

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
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: AttendancesPrimaryKey,
        })
        {
            ChangeTracker.Clear();
            throw new AttendanceAlreadyExistsException("O vou já existe.", exception);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EngagementDbContext).Assembly);
    }
}
