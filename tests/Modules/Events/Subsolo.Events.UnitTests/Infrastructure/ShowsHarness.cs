using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.Application;
using Subsolo.Modules.Events.Application.Features.CreateShow;
using Subsolo.Modules.Events.Domain.Cities;
using Subsolo.Modules.Events.Domain.Shows;
using Subsolo.Modules.Events.Domain.Styles;
using Subsolo.Modules.Profiles.IntegrationEvents;

namespace Subsolo.Events.UnitTests.Infrastructure;

/// <summary>
/// SPEC-0023 — os handlers e o validador do cadastro de show sobre dublês em memória (copiado do <c>ProfilesHarness</c>
/// da SPEC-0021): shows, unidade de trabalho que conta as gravações e recolhe os eventos de domínio (como o interceptor
/// da outbox), cidades (<c>curitiba</c> e <c>sao-paulo</c>, ambas em <c>America/Sao_Paulo</c>), os estilos iniciais, o
/// catálogo de flyers de teste, os perfis de teste (<see cref="TestProfiles"/>) e o <see cref="FakeTimeProvider"/>. Os
/// handlers são montados pelo contêiner (<see cref="Create{T}"/>), com essas dependências e logs nulos.
/// </summary>
internal sealed class ShowsHarness
{
    /// <summary>"Agora": 18/10/2026 12:00 UTC (09:00 em Curitiba).</summary>
    public static readonly DateTimeOffset Start = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Flyer enviado (SPEC-0022) e ainda não ligado.</summary>
    public static readonly string FlyerId = new('a', 64);

    /// <summary>Outro flyer enviado.</summary>
    public static readonly string OtherFlyerId = new('b', 64);

    private readonly ServiceProvider _services;

    public ShowsHarness()
    {
        UnitOfWork = new InMemoryEventsUnitOfWork(Shows);
        _services = new ServiceCollection()
            .AddSingleton<IShowRepository>(Shows)
            .AddSingleton<IEventsUnitOfWork>(UnitOfWork)
            .AddSingleton<ICityRepository>(Cities)
            .AddSingleton<IStyleRepository>(Styles)
            .AddSingleton<IFlyerCatalog>(Flyers)
            .AddSingleton<IProfilesApi>(Profiles)
            .AddSingleton<TimeProvider>(Clock)
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .BuildServiceProvider();
    }

    public FakeTimeProvider Clock { get; } = new(Start);

    public InMemoryShows Shows { get; } = new();

    public InMemoryEventsUnitOfWork UnitOfWork { get; }

    public TestCities Cities { get; } = new();

    public TestStyles Styles { get; } = new();

    public TestFlyerCatalog Flyers { get; } = new(FlyerId, OtherFlyerId);

    public TestProfiles Profiles { get; } = new();

    /// <summary>Um handler ou validador do módulo com as dependências do harness.</summary>
    public T Create<T>() => ActivatorUtilities.CreateInstance<T>(_services);

    /// <summary>"Noite da Peste II" em Curitiba, 14/11/2026 22:00 locais, válido para o organizador <paramref name="organizer"/>.</summary>
    public static CreateShowCommand NoiteDaPeste(
        Guid userId,
        Guid organizer,
        bool isAdmin = false,
        string? title = "Noite da Peste II",
        string? city = "curitiba",
        string? startsLocal = "2026-11-14T22:00",
        ShowVenueInput? venue = null,
        IReadOnlyList<ShowLineupItemInput>? lineup = null,
        IReadOnlyList<string>? styles = null,
        string? price = "R$ 30 / R$ 40 na hora",
        string? ticketUrl = "https://ingressos.example/noite-da-peste-ii",
        string? flyerId = null) =>
        new(
            userId,
            isAdmin,
            organizer,
            title,
            city,
            startsLocal,
            venue ?? new ShowVenueInput(TestProfiles.Venue.Id, null),
            lineup ?? [new(TestProfiles.Band.Id, null), new(null, "Cova Rasa"), new(TestProfiles.OtherBand.Id, null)],
            styles ?? ["death", "grind"],
            price,
            ticketUrl,
            flyerId ?? FlyerId);
}

internal sealed class InMemoryShows : IShowRepository
{
    private readonly List<Show> _shows = [];

    public IReadOnlyList<Show> All => _shows;

    public Task<Show?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_shows.FirstOrDefault(show => show.Id == id));

    public Task<bool> SlugExistsAsync(string city, string slug, CancellationToken cancellationToken) =>
        Task.FromResult(_shows.Any(show => show.City == city && show.Slug == slug));

    public void Add(Show show) => _shows.Add(show);
}

/// <summary>Conta as gravações e, como o interceptor da outbox, recolhe (e limpa) os eventos de domínio pendentes.</summary>
internal sealed class InMemoryEventsUnitOfWork(InMemoryShows shows) : IEventsUnitOfWork
{
    private readonly List<IDomainEvent> _events = [];

    public int Saves { get; private set; }

    public IReadOnlyList<IDomainEvent> Events => _events;

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        Saves++;
        foreach (var show in shows.All)
        {
            _events.AddRange(show.DomainEvents);
            show.ClearDomainEvents();
        }

        return Task.CompletedTask;
    }
}

/// <summary><c>curitiba</c> e <c>sao-paulo</c>, ambas no fuso <c>America/Sao_Paulo</c>.</summary>
internal sealed class TestCities : ICityRepository
{
    private readonly List<City> _cities =
    [
        City.Create("curitiba", "Curitiba", "PR", "America/Sao_Paulo"),
        City.Create("sao-paulo", "São Paulo", "SP", "America/Sao_Paulo"),
    ];

    public Task<City?> GetBySlugAsync(string slug, CancellationToken cancellationToken) =>
        Task.FromResult(_cities.FirstOrDefault(city => city.Slug == slug));

    public Task<IReadOnlyList<City>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<City>>(_cities);
}

/// <summary>Os estilos iniciais do Events: <c>death</c>, <c>black</c>, <c>grind</c>, <c>doom</c>, <c>thrash</c> e mais <c>crust</c>.</summary>
internal sealed class TestStyles : IStyleRepository
{
    private readonly List<Style> _styles =
        [.. new[] { "death", "black", "grind", "doom", "thrash", "crust" }.Select(slug => Style.Create(slug, slug))];

    public Task<Style?> GetBySlugAsync(string slug, CancellationToken cancellationToken) =>
        Task.FromResult(_styles.FirstOrDefault(style => style.Slug == slug));

    public Task<IReadOnlyList<Style>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Style>>(_styles);

    public void Add(Style style) => _styles.Add(style);
}

/// <summary>Dublê do <see cref="IFlyerCatalog"/>: os flyers enviados e os ligados (na ordem).</summary>
internal sealed class TestFlyerCatalog(params string[] uploaded) : IFlyerCatalog
{
    private readonly HashSet<string> _uploaded = new(uploaded, StringComparer.Ordinal);

    public List<string> Attached { get; } = [];

    public Task<bool> ExistsAsync(string flyerId, CancellationToken ct) => Task.FromResult(_uploaded.Contains(flyerId));

    public Task AttachAsync(string flyerId, CancellationToken ct)
    {
        Attached.Add(flyerId);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Dublê do <see cref="IProfilesApi"/>: o produtor <see cref="ProducerA"/> (dono <see cref="OwnerA"/>), o produtor
/// <see cref="ProducerB"/> (dono <see cref="OwnerB"/>), o produtor sem dono <see cref="Ownerless"/>, a casa
/// <see cref="Venue"/> e as bandas <see cref="Band"/> e <see cref="OtherBand"/>, todos em Curitiba.
/// </summary>
internal sealed class TestProfiles : IProfilesApi
{
    public static readonly Guid OwnerA = new("0199f000-0000-7000-8000-00000000000a");

    public static readonly Guid OwnerB = new("0199f000-0000-7000-8000-00000000000b");

    public static readonly Guid Admin = new("0199f000-0000-7000-8000-0000000000ad");

    public static readonly ProfileSummary ProducerA = new(
        new Guid("0199f001-0000-7000-8000-00000000000a"), "producer", "Peste Produções", "peste-producoes", "curitiba", OwnerA);

    public static readonly ProfileSummary ProducerB = new(
        new Guid("0199f001-0000-7000-8000-00000000000b"), "producer", "Caos Produções", "caos-producoes", "curitiba", OwnerB);

    public static readonly ProfileSummary Ownerless = new(
        new Guid("0199f001-0000-7000-8000-00000000000c"), "producer", "Porão Coletivo", "porao-coletivo", "curitiba", null);

    public static readonly ProfileSummary Venue = new(
        new Guid("0199f001-0000-7000-8000-0000000000c1"), "venue", "Porão do Rock", "porao-do-rock", "curitiba", null);

    public static readonly ProfileSummary Band = new(
        new Guid("0199f001-0000-7000-8000-0000000000b1"), "band", "Podridão", "podridao", "curitiba", null);

    public static readonly ProfileSummary OtherBand = new(
        new Guid("0199f001-0000-7000-8000-0000000000b2"), "band", "Necrocorpo", "necrocorpo", "curitiba", OwnerB);

    private static readonly ProfileSummary[] All = [ProducerA, ProducerB, Ownerless, Venue, Band, OtherBand];

    public Task<IReadOnlyList<ProfileSummary>> GetOwnedProfilesAsync(Guid userId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ProfileSummary>>([.. All.Where(profile => profile.OwnerUserId == userId)]);

    public Task<ProfileSummary?> GetAsync(Guid profileId, CancellationToken ct) =>
        Task.FromResult(All.FirstOrDefault(profile => profile.Id == profileId));
}
