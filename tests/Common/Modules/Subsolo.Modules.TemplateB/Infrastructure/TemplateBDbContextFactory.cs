using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Subsolo.Modules.TemplateB.Infrastructure;

/// <summary>Dublê de teste: cria o <see cref="TemplateBDbContext"/> para o <c>dotnet ef migrations</c> (sem banco).</summary>
[ExcludeFromCodeCoverage(Justification = "Usado só em tempo de design pelo dotnet ef.")]
internal sealed class TemplateBDbContextFactory : IDesignTimeDbContextFactory<TemplateBDbContext>
{
    public TemplateBDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<TemplateBDbContext>().UseNpgsql("Host=localhost;Database=subsolo").Options);
}
