using Microsoft.EntityFrameworkCore;
using Npgsql;
using Subsolo.Common.Infrastructure.Database;
using Subsolo.Modules.Moderation.Application;
using Subsolo.Modules.Moderation.Domain.Reports;

namespace Subsolo.Modules.Moderation.Infrastructure;

/// <summary>
/// SPEC-0033 — DbContext do módulo Moderation, no schema <see cref="SchemaName"/>; é também a unidade de trabalho do
/// módulo (o <c>ShowHiddenByModeration</c> vai para a outbox no mesmo <c>SaveChanges</c>). A denúncia pendente repetida
/// (índice único parcial) e a decisão concorrente (token <c>xmin</c>) viram <see cref="ModerationConflictException"/>,
/// com as mudanças descartadas.
/// </summary>
public sealed class ModerationDbContext(DbContextOptions<ModerationDbContext> options)
    : ModuleDbContext(options, SchemaName), IModerationUnitOfWork
{
    public const string SchemaName = "moderation";

    /// <summary>Índice único parcial: uma denúncia pendente por fã e show (§6).</summary>
    public const string PendingReportIndex = "IX_reports_pending_show_reporter";

    public DbSet<Report> Reports => Set<Report>();

    async Task IModerationUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            ChangeTracker.Clear();
            throw new ModerationConflictException(ModerationConflict.AlreadyDecided, exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: PendingReportIndex,
        })
        {
            ChangeTracker.Clear();
            throw new ModerationConflictException(ModerationConflict.AlreadyReported, exception);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ModerationDbContext).Assembly);
    }
}
