using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Subsolo.Modules.Site.Infrastructure;

/// <summary>SPEC-0028 — cria o <see cref="SiteDbContext"/> para o <c>dotnet ef migrations</c> (sem banco).</summary>
[ExcludeFromCodeCoverage(Justification = "Usado só em tempo de design pelo dotnet ef.")]
internal sealed class SiteDbContextFactory : IDesignTimeDbContextFactory<SiteDbContext>
{
    public SiteDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<SiteDbContext>().UseNpgsql("Host=localhost;Database=subsolo").Options);
}
