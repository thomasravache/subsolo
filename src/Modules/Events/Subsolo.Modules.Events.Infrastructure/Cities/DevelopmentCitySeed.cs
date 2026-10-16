using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Subsolo.Modules.Events.Infrastructure.Cities;

/// <summary>
/// SPEC-0008 — seed de desenvolvimento: a cidade <see cref="Slug"/> ("Cidade Exemplo", SP, America/Sao_Paulo).
/// Idempotente e seguro com hosts subindo em paralelo (<c>on conflict do nothing</c> no slug único).
/// </summary>
public static class DevelopmentCitySeed
{
    public const string Slug = "cidade-exemplo";
    public const string Name = "Cidade Exemplo";
    public const string State = "SP";
    public const string TimeZone = "America/Sao_Paulo";

    /// <summary>Id fixo, para a cidade de exemplo ser a mesma em todo ambiente de desenvolvimento.</summary>
    public static readonly Guid Id = new("5b1f8d1e-8f0c-4c4e-9a59-3c1d7a0e0001");

    /// <summary>Grava a cidade de exemplo se ela ainda não existe (o host chama só em Development).</summary>
    public static async Task SeedEventsDevelopmentDataAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"""
            insert into events.cities (id, slug, name, state, time_zone)
            values ({Id}, {Slug}, {Name}, {State}, {TimeZone})
            on conflict (slug) do nothing
            """,
            cancellationToken);
    }
}
