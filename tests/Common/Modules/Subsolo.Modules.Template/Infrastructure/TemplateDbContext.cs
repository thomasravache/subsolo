using Microsoft.EntityFrameworkCore;
using Subsolo.Common.Infrastructure.Database;
using Subsolo.Modules.Template.Domain;

namespace Subsolo.Modules.Template.Infrastructure;

/// <summary>Dublê de teste: DbContext do módulo Template, no schema <see cref="SchemaName"/>.</summary>
public sealed class TemplateDbContext(DbContextOptions<TemplateDbContext> options)
    : ModuleDbContext(options, SchemaName)
{
    public const string SchemaName = "template";

    public DbSet<TemplateItem> Items => Set<TemplateItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TemplateItem>(item =>
        {
            item.ToTable("items");
            item.HasKey(i => i.Id);
            item.Property(i => i.Id).HasColumnName("id");
            item.Property(i => i.Name).HasColumnName("name").HasMaxLength(200);
        });
    }
}
