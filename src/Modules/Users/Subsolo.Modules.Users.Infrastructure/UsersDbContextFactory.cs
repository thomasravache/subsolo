using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Subsolo.Modules.Users.Infrastructure;

/// <summary>SPEC-0014 — cria o <see cref="UsersDbContext"/> para o <c>dotnet ef migrations</c> (sem banco).</summary>
[ExcludeFromCodeCoverage(Justification = "Usado só em tempo de design pelo dotnet ef.")]
internal sealed class UsersDbContextFactory : IDesignTimeDbContextFactory<UsersDbContext>
{
    public UsersDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<UsersDbContext>().UseNpgsql("Host=localhost;Database=subsolo").Options);
}
