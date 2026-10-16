using Microsoft.EntityFrameworkCore;

namespace Subsolo.Common.Infrastructure.Database;

/// <summary>
/// SPEC-0007 — base do DbContext de cada módulo (ADR-0002): <c>HasDefaultSchema(schema)</c>, as tabelas
/// <c>outbox_messages</c> e <c>inbox_messages</c> no schema do módulo, e a gravação dos eventos de domínio das
/// entidades na outbox na mesma transação do <c>SaveChanges</c>. O DbContext derivado chama
/// <c>base.OnModelCreating</c>.
/// </summary>
public abstract class ModuleDbContext : DbContext
{
    protected ModuleDbContext(DbContextOptions options, string schema)
        : base(options) => throw new NotImplementedException();

    /// <summary>Schema do módulo no PostgreSQL (ex.: <c>events</c>).</summary>
    public string Schema => throw new NotImplementedException();

    protected override void OnModelCreating(ModelBuilder modelBuilder) => throw new NotImplementedException();
}
