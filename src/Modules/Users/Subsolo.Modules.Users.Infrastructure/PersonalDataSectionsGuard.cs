using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Subsolo.Modules.Users.IntegrationEvents;

namespace Subsolo.Modules.Users.Infrastructure;

/// <summary>
/// SPEC-0021 (§7.6, herança da revisão da SPEC-0019) — na inicialização, confere que as seções do export
/// (<see cref="IPersonalDataSection"/>) têm nomes distintos: um nome repetido faria uma seção sobrescrever a outra em
/// silêncio, então a API não sobe e a falha nomeia a seção.
/// </summary>
internal sealed class PersonalDataSectionsGuard(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var repeated = scope.ServiceProvider.GetServices<IPersonalDataSection>()
            .GroupBy(section => section.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Order(StringComparer.Ordinal)
            .ToList();
        if (repeated.Count > 0)
        {
            throw new InvalidOperationException(
                $"Seções do export de dados pessoais com o mesmo nome: {string.Join(", ", repeated)}.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
