using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.Common.Application.Media;
using Subsolo.Modules.Events.Domain.Flyers;

namespace Subsolo.Events.IntegrationTests.Infrastructure;

/// <summary>
/// SPEC-0026 (H24-02) — um envio do mesmo flyer chegando na janela da limpeza: quando a limpeza (que já apagou a linha e
/// conferiu que ela não voltou) vai apagar o primeiro arquivo do flyer armado (<see cref="Arm"/>), o envio começa e a
/// limpeza espera até ele terminar ou <see cref="Wait"/> passar — o que daria tempo de o envio gravar arquivos e linha
/// antes da exclusão dos arquivos, se nada os serializasse. Com a checagem e a exclusão da limpeza e a gravação e a
/// inserção do envio sob a mesma trava por id, o envio espera a limpeza terminar. Dispara uma vez por armação.
/// </summary>
internal sealed class CleanupLockRace
{
    /// <summary>Quanto a limpeza espera o envio concorrente antes de seguir.</summary>
    public static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    private readonly Lock _gate = new();

    private string? _armed;

    private Func<Task<HttpStatusCode>>? _upload;

    private int _deleted;

    /// <summary>O envio concorrente, depois de disparado.</summary>
    public Task<HttpStatusCode>? Upload { get; private set; }

    /// <summary>Quantos arquivos do flyer armado a limpeza apagou.</summary>
    public int Deleted => Volatile.Read(ref _deleted);

    public void Arm(string flyerId, Func<Task<HttpStatusCode>> upload)
    {
        lock (_gate)
        {
            _armed = flyerId;
            _upload = upload;
            _deleted = 0;
            Upload = null;
        }
    }

    /// <summary>Troca o <see cref="IFileStorage"/> registrado pelo que dispara a corrida.</summary>
    public void Install(IServiceCollection services)
    {
        var descriptor = services.Last(item => item.ServiceType == typeof(IFileStorage));
        services.Remove(descriptor);
        services.Add(new ServiceDescriptor(
            typeof(IFileStorage),
            provider => new RacingStorage(Inner(provider, descriptor), this),
            descriptor.Lifetime));
    }

    private async Task BeforeDeleteAsync(string path)
    {
        Task<HttpStatusCode>? started = null;
        lock (_gate)
        {
            if (_armed is null || !Flyer.VariantPaths(_armed).Contains(path, StringComparer.Ordinal))
            {
                return;
            }

            _deleted++;
            if (Upload is null && _upload is not null)
            {
                Upload = started = Task.Run(_upload);
            }
        }

        if (started is not null)
        {
            await Task.WhenAny(started, Task.Delay(Wait));
        }
    }

    private static IFileStorage Inner(IServiceProvider provider, ServiceDescriptor descriptor) =>
        descriptor switch
        {
            { ImplementationFactory: { } factory } => (IFileStorage)factory(provider),
            { ImplementationInstance: { } instance } => (IFileStorage)instance,
            { ImplementationType: { } type } => (IFileStorage)ActivatorUtilities.CreateInstance(provider, type),
            _ => throw new InvalidOperationException("registro de IFileStorage sem implementação"),
        };

    private sealed class RacingStorage(IFileStorage inner, CleanupLockRace race) : IFileStorage
    {
        public Task SaveAsync(string path, Stream content, CancellationToken ct) => inner.SaveAsync(path, content, ct);

        public Task<Stream?> OpenAsync(string path, CancellationToken ct) => inner.OpenAsync(path, ct);

        public async Task DeleteAsync(string path, CancellationToken ct)
        {
            await race.BeforeDeleteAsync(path);
            await inner.DeleteAsync(path, ct);
        }
    }
}
