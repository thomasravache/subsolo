using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Subsolo.Modules.Moderation.Infrastructure;

/// <summary>SPEC-0033 — cria o <see cref="ModerationDbContext"/> para o <c>dotnet ef migrations</c> (sem banco).</summary>
[ExcludeFromCodeCoverage(Justification = "Usado só em tempo de design pelo dotnet ef.")]
internal sealed class ModerationDbContextFactory : IDesignTimeDbContextFactory<ModerationDbContext>
{
    public ModerationDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ModerationDbContext>().UseNpgsql("Host=localhost;Database=subsolo").Options);
}
