using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Subsolo.Modules.Engagement.Infrastructure;

/// <summary>SPEC-0027 — cria o <see cref="EngagementDbContext"/> para o <c>dotnet ef migrations</c> (sem banco).</summary>
[ExcludeFromCodeCoverage(Justification = "Usado só em tempo de design pelo dotnet ef.")]
internal sealed class EngagementDbContextFactory : IDesignTimeDbContextFactory<EngagementDbContext>
{
    public EngagementDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<EngagementDbContext>().UseNpgsql("Host=localhost;Database=subsolo").Options);
}
