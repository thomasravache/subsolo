using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.IntegrationEvents;
using Subsolo.Modules.Profiles.Application;
using Subsolo.Modules.Profiles.Domain.Profiles;
using Subsolo.Modules.Profiles.Domain.Requests;

namespace Subsolo.Profiles.UnitTests.Infrastructure;

/// <summary>
/// SPEC-0021 — os handlers e validadores do módulo Profiles sobre dublês em memória: perfis, pedidos, unidade de
/// trabalho que conta as gravações e recolhe os eventos de domínio (como o interceptor da outbox), o catálogo de
/// cidades e estilos de teste (<see cref="TestEventsCatalog"/>) e o <see cref="FakeTimeProvider"/>. Os handlers são
/// montados pelo contêiner (<see cref="Create{T}"/>), com essas dependências e logs nulos.
/// </summary>
internal sealed class ProfilesHarness
{
    public static readonly DateTimeOffset Start = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly ServiceProvider _services;

    public ProfilesHarness()
    {
        UnitOfWork = new InMemoryProfilesUnitOfWork(Profiles, Requests);
        _services = new ServiceCollection()
            .AddSingleton<IProfileRepository>(Profiles)
            .AddSingleton<IProfileRequestRepository>(Requests)
            .AddSingleton<IProfilesUnitOfWork>(UnitOfWork)
            .AddSingleton<IEventsCatalog>(Catalog)
            .AddSingleton<TimeProvider>(Clock)
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .BuildServiceProvider();
    }

    public FakeTimeProvider Clock { get; } = new(Start);

    public InMemoryProfiles Profiles { get; } = new();

    public InMemoryProfileRequests Requests { get; } = new();

    public InMemoryProfilesUnitOfWork UnitOfWork { get; }

    public TestEventsCatalog Catalog { get; } = new();

    /// <summary>Um handler ou validador do módulo com as dependências do harness.</summary>
    public T Create<T>() => ActivatorUtilities.CreateInstance<T>(_services);

    /// <summary>Os dados de um perfil já validados (slug informado).</summary>
    public static ProfileDraft Draft(
        string name = "Podridão",
        string slug = "podridao",
        string type = ProfileTypes.Band,
        string city = "curitiba",
        IReadOnlyList<string>? styles = null) =>
        new(type, name, slug, city, styles ?? (type == ProfileTypes.Band ? ["death"] : []), ProfileLinks.None);

    /// <summary>Um perfil aprovado já gravado (sem dono, a menos que <paramref name="ownerUserId"/>).</summary>
    public Profile AddProfile(ProfileDraft draft, Guid? ownerUserId = null)
    {
        var profile = Profile.Create(draft, ownerUserId, Clock.GetUtcNow());
        profile.ClearDomainEvents();
        Profiles.Add(profile);
        return profile;
    }

    /// <summary>Um pedido de perfil novo pendente já gravado.</summary>
    public ProfileRequest AddNewRequest(Guid userId, ProfileDraft draft, string contact = "@podridao no Instagram")
    {
        var request = ProfileRequest.SubmitNew(userId, draft, contact, Clock.GetUtcNow());
        request.ClearDomainEvents();
        Requests.Add(request);
        return request;
    }

    /// <summary>Uma reivindicação pendente já gravada.</summary>
    public ProfileRequest AddClaim(Guid userId, Guid profileId, string proof = "Sou o baterista; o Instagram da banda confirma.")
    {
        var request = ProfileRequest.SubmitClaim(userId, profileId, proof, Clock.GetUtcNow());
        request.ClearDomainEvents();
        Requests.Add(request);
        return request;
    }
}

internal sealed class InMemoryProfiles : IProfileRepository
{
    private readonly List<Profile> _profiles = [];

    public IReadOnlyList<Profile> All => _profiles;

    public Task<Profile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_profiles.FirstOrDefault(profile => profile.Id == id));

    public Task<bool> ExistsAsync(string type, string slug, CancellationToken cancellationToken) =>
        Task.FromResult(_profiles.Any(profile => profile.Type == type && profile.Slug == slug));

    public void Add(Profile profile) => _profiles.Add(profile);
}

internal sealed class InMemoryProfileRequests : IProfileRequestRepository
{
    private readonly List<ProfileRequest> _requests = [];

    public IReadOnlyList<ProfileRequest> All => _requests;

    public Task<ProfileRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_requests.FirstOrDefault(request => request.Id == id));

    public Task<bool> HasPendingAsync(Guid requesterUserId, CancellationToken cancellationToken) =>
        Task.FromResult(_requests.Any(request =>
            request.RequesterUserId == requesterUserId && request.Status == ProfileRequestStatuses.Pending));

    public void Add(ProfileRequest request) => _requests.Add(request);
}

/// <summary>Conta as gravações e, como o interceptor da outbox, recolhe (e limpa) os eventos de domínio pendentes.</summary>
internal sealed class InMemoryProfilesUnitOfWork(InMemoryProfiles profiles, InMemoryProfileRequests requests) : IProfilesUnitOfWork
{
    private readonly List<IDomainEvent> _events = [];

    public int Saves { get; private set; }

    public IReadOnlyList<IDomainEvent> Events => _events;

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        Saves++;
        foreach (var entity in profiles.All.Cast<Entity>().Concat(requests.All))
        {
            _events.AddRange(entity.DomainEvents);
            entity.ClearDomainEvents();
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Dublê do <see cref="IEventsCatalog"/>: as cidades <c>curitiba</c> e <c>sao-paulo</c> e os estilos iniciais do
/// Events (<c>death</c>, <c>black</c>, <c>grind</c>, <c>doom</c>, <c>thrash</c>) mais <c>crust</c>; guarda cada consulta.
/// </summary>
internal sealed class TestEventsCatalog : IEventsCatalog
{
    private readonly HashSet<string> _cities = new(["curitiba", "sao-paulo"], StringComparer.Ordinal);
    private readonly HashSet<string> _styles = new(["death", "black", "grind", "doom", "thrash", "crust"], StringComparer.Ordinal);

    public List<string> CityQueries { get; } = [];

    public List<IReadOnlyCollection<string>> StyleQueries { get; } = [];

    /// <summary>SPEC-0029 — o Profiles não lê shows: nenhum existe aqui.</summary>
    public Task<ShowState?> GetShowStateAsync(Guid showId, CancellationToken ct) => Task.FromResult<ShowState?>(null);

    /// <summary>SPEC-0031 — o Profiles não lê resumo de show: nenhum.</summary>
    public Task<ShowSummary?> GetShowSummaryAsync(Guid showId, CancellationToken ct) => Task.FromResult<ShowSummary?>(null);

    /// <summary>SPEC-0032 — o Profiles não lê shows: nenhum.</summary>
    public Task<IReadOnlyList<ShowSummary>> ListScheduledShowsStartingAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ShowSummary>>([]);

    /// <summary>SPEC-0032 (H29-01) — o Profiles não lê shows: nenhum.</summary>
    public Task<IReadOnlyDictionary<Guid, ShowState>> GetShowStatesAsync(IReadOnlyCollection<Guid> showIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<Guid, ShowState>>(new Dictionary<Guid, ShowState>());

    public Task<bool> CityExistsAsync(string slug, CancellationToken ct)
    {
        CityQueries.Add(slug);
        return Task.FromResult(_cities.Contains(slug));
    }

    public Task<bool> StylesExistAsync(IReadOnlyCollection<string> slugs, CancellationToken ct)
    {
        StyleQueries.Add(slugs);
        return Task.FromResult(slugs.All(_styles.Contains));
    }
}
