using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Subsolo.Modules.Template.Infrastructure;

/// <summary>Dublê de teste: cria o <see cref="TemplateDbContext"/> para o <c>dotnet ef migrations</c> (sem banco).</summary>
[ExcludeFromCodeCoverage(Justification = "Usado só em tempo de design pelo dotnet ef.")]
internal sealed class TemplateDbContextFactory : IDesignTimeDbContextFactory<TemplateDbContext>
{
    public TemplateDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<TemplateDbContext>().UseNpgsql("Host=localhost;Database=subsolo").Options);
}
