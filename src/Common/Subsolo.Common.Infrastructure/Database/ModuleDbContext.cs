using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Subsolo.Common.Infrastructure.Messaging;

namespace Subsolo.Common.Infrastructure.Database;

/// <summary>
/// SPEC-0007 — base do DbContext de cada módulo (ADR-0002): <c>HasDefaultSchema(schema)</c>, as tabelas
/// <c>outbox_messages</c> e <c>inbox_messages</c> (e as de consumidores) no schema do módulo, e o histórico de
/// migrações no mesmo schema. Os eventos de domínio das entidades vão para a outbox na mesma transação do
/// <c>SaveChanges</c> (interceptor registrado pelo <c>AddModule</c>). O DbContext derivado chama
/// <c>base.OnModelCreating</c>.
/// </summary>
public abstract class ModuleDbContext : DbContext
{
    protected ModuleDbContext(DbContextOptions options, string schema)
        : base(options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        Schema = schema;
    }

    /// <summary>Schema do módulo no PostgreSQL (ex.: <c>events</c>).</summary>
    public string Schema { get; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        // Mantém o provider e a connection string já configurados; só põe o histórico no schema do módulo.
        optionsBuilder.UseNpgsql(npgsql => npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schema));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyMessaging();
    }
}
