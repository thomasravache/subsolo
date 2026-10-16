using Microsoft.EntityFrameworkCore;
using Subsolo.Common.Infrastructure.Database;

namespace Subsolo.Modules.TemplateB.Infrastructure;

/// <summary>Dublê de teste: DbContext do módulo TemplateB (só outbox e inbox), no schema <see cref="SchemaName"/>.</summary>
public sealed class TemplateBDbContext(DbContextOptions<TemplateBDbContext> options)
    : ModuleDbContext(options, SchemaName)
{
    public const string SchemaName = "template_b";
}
