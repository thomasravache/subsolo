using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Subsolo.Modules.Profiles.Infrastructure;

/// <summary>SPEC-0021 — cria o <see cref="ProfilesDbContext"/> para o <c>dotnet ef migrations</c> (sem banco).</summary>
[ExcludeFromCodeCoverage(Justification = "Usado só em tempo de design pelo dotnet ef.")]
internal sealed class ProfilesDbContextFactory : IDesignTimeDbContextFactory<ProfilesDbContext>
{
    public ProfilesDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ProfilesDbContext>().UseNpgsql("Host=localhost;Database=subsolo").Options);
}
