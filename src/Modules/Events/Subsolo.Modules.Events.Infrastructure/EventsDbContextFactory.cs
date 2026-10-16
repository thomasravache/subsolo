using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Subsolo.Modules.Events.Infrastructure;

/// <summary>SPEC-0008 — cria o <see cref="EventsDbContext"/> para o <c>dotnet ef migrations</c> (sem banco).</summary>
[ExcludeFromCodeCoverage(Justification = "Usado só em tempo de design pelo dotnet ef.")]
internal sealed class EventsDbContextFactory : IDesignTimeDbContextFactory<EventsDbContext>
{
    public EventsDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<EventsDbContext>().UseNpgsql("Host=localhost;Database=subsolo").Options);
}
