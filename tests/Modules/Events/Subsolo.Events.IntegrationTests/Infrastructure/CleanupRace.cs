using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Subsolo.Modules.Events.Application;
using Subsolo.Modules.Events.Domain.Flyers;

namespace Subsolo.Events.IntegrationTests.Infrastructure;

/// <summary>
/// SPEC-0024 (H23-01, H23-02) — a limpeza dos flyers chegando no pior momento: quando o flyer armado
/// (<see cref="Arm"/>) é conferido pelo cadastro (<see cref="IFlyerCatalog.ExistsAsync"/>) ou lido pelo envio
/// (<see cref="IFlyerRepository.GetAsync"/>), a linha dele é apagada direto no banco e os arquivos do <c>Media:Root</c>
/// somem — o que a limpeza faria entre a leitura e o <c>SaveChanges</c>. Dispara uma vez por armação. SPEC-0026 (H24-08):
/// armado com <c>rowOnly</c>, apaga só a linha e deixa os arquivos (a limpeza parada entre os dois passos).
/// </summary>
internal sealed class CleanupRace(string connectionString, Func<string> mediaRoot)
{
    private string? _armed;

    private bool _rowOnly;

    /// <summary>Quantas vezes a limpeza simulada rodou.</summary>
    public int Fired { get; private set; }

    public void Arm(string flyerId, bool rowOnly = false)
    {
        _armed = flyerId;
        _rowOnly = rowOnly;
    }

    /// <summary>Troca o <see cref="IFlyerCatalog"/> e o <see cref="IFlyerRepository"/> registrados pelos que disparam a corrida.</summary>
    public void Install(IServiceCollection services)
    {
        Decorate<IFlyerCatalog>(services, inner => new RacingCatalog(inner, this));
        Decorate<IFlyerRepository>(services, inner => new RacingRepository(inner, this));
    }

    private async Task FireIfArmedAsync(string flyerId, CancellationToken ct)
    {
        if (!string.Equals(_armed, flyerId, StringComparison.Ordinal))
        {
            return;
        }

        _armed = null;
        Fired++;
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync(ct);
            await using var delete = new NpgsqlCommand("delete from events.flyers where id = @id", connection);
            delete.Parameters.AddWithValue("id", flyerId);
            await delete.ExecuteNonQueryAsync(ct);
        }

        if (_rowOnly)
        {
            return;
        }

        foreach (var path in Flyer.VariantPaths(flyerId))
        {
            File.Delete(Path.Combine(mediaRoot(), path));
        }
    }

    private static void Decorate<TService>(IServiceCollection services, Func<TService, TService> decorate)
        where TService : class
    {
        var descriptor = services.Last(item => item.ServiceType == typeof(TService));
        services.Remove(descriptor);
        services.Add(new ServiceDescriptor(
            typeof(TService),
            provider => decorate(Inner<TService>(provider, descriptor)),
            descriptor.Lifetime));
    }

    private static TService Inner<TService>(IServiceProvider provider, ServiceDescriptor descriptor)
        where TService : class =>
        descriptor switch
        {
            { ImplementationFactory: { } factory } => (TService)factory(provider),
            { ImplementationInstance: { } instance } => (TService)instance,
            { ImplementationType: { } type } => (TService)ActivatorUtilities.CreateInstance(provider, type),
            _ => throw new InvalidOperationException($"registro de {typeof(TService).Name} sem implementação"),
        };

    private sealed class RacingCatalog(IFlyerCatalog inner, CleanupRace race) : IFlyerCatalog
    {
        public async Task<bool> ExistsAsync(string flyerId, CancellationToken ct)
        {
            var exists = await inner.ExistsAsync(flyerId, ct);
            await race.FireIfArmedAsync(flyerId, ct);
            return exists;
        }

        public Task AttachAsync(string flyerId, CancellationToken ct) => inner.AttachAsync(flyerId, ct);
    }

    private sealed class RacingRepository(IFlyerRepository inner, CleanupRace race) : IFlyerRepository
    {
        public async Task<Flyer?> GetAsync(string id, CancellationToken cancellationToken)
        {
            var flyer = await inner.GetAsync(id, cancellationToken);
            if (flyer is not null)
            {
                await race.FireIfArmedAsync(id, cancellationToken);
            }

            return flyer;
        }

        public Task<bool> ExistsAsync(string id, CancellationToken cancellationToken) => inner.ExistsAsync(id, cancellationToken);

        /// <summary>Morto desde a SPEC-0023 (H23-05): repassa enquanto a interface tiver o membro.</summary>
        public Task<IReadOnlyList<Flyer>> ListUnattachedCreatedBeforeAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException("sem uso desde a exclusão condicional (H22-01)");

        public Task<IReadOnlyList<string>> DeleteUnattachedCreatedBeforeAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken) =>
            inner.DeleteUnattachedCreatedBeforeAsync(cutoffUtc, cancellationToken);

        public void Add(Flyer flyer) => inner.Add(flyer);

        public void Remove(Flyer flyer) => inner.Remove(flyer);
    }
}
