using Microsoft.EntityFrameworkCore;
using Npgsql;
using Subsolo.Common.Infrastructure.Database;
using Subsolo.Modules.Events.Application;
using Subsolo.Modules.Events.Domain.Cities;
using Subsolo.Modules.Events.Domain.Flyers;
using Subsolo.Modules.Events.Domain.Shows;
using Subsolo.Modules.Events.Domain.Styles;
using Subsolo.Modules.Events.Infrastructure.Shows;

namespace Subsolo.Modules.Events.Infrastructure;

/// <summary>SPEC-0008 — DbContext do módulo Events, no schema <see cref="SchemaName"/>.</summary>
public sealed class EventsDbContext(DbContextOptions<EventsDbContext> options)
    : ModuleDbContext(options, SchemaName), IEventsUnitOfWork
{
    public const string SchemaName = "events";

    /// <summary>Índices únicos dos slugs (nomes gerados pelo EF nas migrações).</summary>
    private const string CitySlugIndex = "IX_cities_slug";

    private const string StyleSlugIndex = "IX_styles_slug";

    /// <summary>SPEC-0022 — chave primária dos flyers (o id é o hash do arquivo).</summary>
    private const string FlyerKey = "PK_flyers";

    /// <summary>SPEC-0024 — FK do show para o flyer (nome gerado pelo EF na migração dos shows).</summary>
    private const string ShowFlyerForeignKey = "FK_shows_flyers_flyer_id";

    public DbSet<City> Cities => Set<City>();

    /// <summary>SPEC-0017 — estilos de metal (os iniciais vêm da migração de dados).</summary>
    public DbSet<Style> Styles => Set<Style>();

    /// <summary>SPEC-0022 — flyers enviados (as variantes ficam no <c>Media:Root</c>).</summary>
    public DbSet<Flyer> Flyers => Set<Flyer>();

    /// <summary>SPEC-0023 — shows da agenda.</summary>
    public DbSet<Show> Shows => Set<Show>();

    /// <summary>
    /// SPEC-0017 — unidade de trabalho do módulo: grava as mudanças rastreadas numa transação. Emenda 2: violação de
    /// unicidade (23505) num slug de cidade ou estilo (SPEC-0023: ou de show na cidade) vira <see cref="DuplicateSlugException"/>. SPEC-0022: no id de um
    /// flyer, <see cref="DuplicateFlyerException"/>. SPEC-0024 (H23-02): o flyer apagado pela limpeza depois da
    /// conferência — a FK do show (23503) ou a linha do flyer que não existe mais (concorrência) — vira
    /// <see cref="MissingFlyerException"/>.
    /// </summary>
    async Task IEventsUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception) when (exception.Entries.Any(entry => entry.Entity is Flyer))
        {
            ChangeTracker.Clear();
            throw new MissingFlyerException("O flyer foi apagado durante a gravação.", exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.ForeignKeyViolation,
            ConstraintName: ShowFlyerForeignKey,
        })
        {
            ChangeTracker.Clear();
            throw new MissingFlyerException("O flyer do show foi apagado durante a gravação.", exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: CitySlugIndex or StyleSlugIndex or ShowConfiguration.CitySlugIndex,
        })
        {
            ChangeTracker.Clear();
            throw new DuplicateSlugException("Slug já gravado por outro pedido.", exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: FlyerKey,
        })
        {
            ChangeTracker.Clear();
            throw new DuplicateFlyerException("Flyer já gravado por outro envio.", exception);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EventsDbContext).Assembly);
    }
}
