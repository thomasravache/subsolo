using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Subsolo.Modules.Engagement.Application;
using Subsolo.Modules.Engagement.Domain.Attendances;
using Subsolo.Modules.Engagement.Domain.Follows;
using Subsolo.Modules.Events.IntegrationEvents;
using Subsolo.Modules.Profiles.IntegrationEvents;

namespace Subsolo.Engagement.UnitTests.Infrastructure;

/// <summary>
/// SPEC-0027 — os handlers e a interface pública do módulo Engagement sobre dublês em memória (copiado do
/// <c>ProfilesHarness</c> da SPEC-0021): follows, unidade de trabalho que conta as gravações, o catálogo de cidades de
/// teste (<see cref="TestEventsCatalog"/>: <c>curitiba</c> e <c>sao-paulo</c>), os perfis de teste
/// (<see cref="TestProfilesApi"/>: só os aprovados existem) e o <see cref="FakeTimeProvider"/>. Os tipos são montados
/// pelo contêiner (<see cref="Create{T}"/>), com essas dependências e logs nulos. SPEC-0029: os "vou" em memória
/// (<see cref="Attendances"/>) e os shows de teste do catálogo (<see cref="TestEventsCatalog.AddShow"/>); os follows
/// também servem de read model (<see cref="IFollowsReadModel"/>) e o catálogo dá os nomes das cidades
/// (<see cref="IEventsCatalogNames"/>).
/// </summary>
internal sealed class EngagementHarness
{
    public static readonly DateTimeOffset Start = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly ServiceProvider _services;

    public EngagementHarness()
    {
        UnitOfWork = new InMemoryEngagementUnitOfWork(Follows, Attendances);
        _services = new ServiceCollection()
            .AddSingleton<IFollowRepository>(Follows)
            .AddSingleton<IFollowsReadModel>(Follows)
            .AddSingleton<IAttendanceRepository>(Attendances)
            .AddSingleton<IEventsCatalogNames>(Catalog)
            .AddSingleton<IEngagementUnitOfWork>(UnitOfWork)
            .AddSingleton<IEventsCatalog>(Catalog)
            .AddSingleton<IProfilesApi>(Profiles)
            .AddSingleton<TimeProvider>(Clock)
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .BuildServiceProvider();
    }

    public FakeTimeProvider Clock { get; } = new(Start);

    public InMemoryFollows Follows { get; } = new();

    public InMemoryAttendances Attendances { get; } = new();

    public InMemoryEngagementUnitOfWork UnitOfWork { get; }

    public TestEventsCatalog Catalog { get; } = new();

    public TestProfilesApi Profiles { get; } = new();

    /// <summary>Um handler (ou a implementação do <c>IEngagementApi</c>) do módulo com as dependências do harness.</summary>
    public T Create<T>() => ActivatorUtilities.CreateInstance<T>(_services);

    /// <summary>Um follow já gravado.</summary>
    public Follow AddFollow(Guid userId, FollowTarget target)
    {
        var follow = Follow.Create(userId, target, Clock.GetUtcNow());
        Follows.AddSaved(follow);
        return follow;
    }

    /// <summary>SPEC-0029 — um "vou" já gravado.</summary>
    public Attendance AddAttendance(Guid userId, Guid showId)
    {
        var attendance = Attendance.Create(userId, showId, Clock.GetUtcNow());
        Attendances.AddSaved(attendance);
        return attendance;
    }
}

/// <summary>
/// Follows em memória: <see cref="Add"/> só entra em <see cref="All"/> depois do <c>SaveChangesAsync</c> (como o EF);
/// <see cref="GetFollowerIdsAsync"/> devolve um id por follow que bate com algum alvo (com repetição, como a consulta).
/// </summary>
internal sealed class InMemoryFollows : IFollowRepository, IFollowsReadModel
{
    private readonly List<Follow> _saved = [];
    private readonly List<Follow> _pending = [];

    public IReadOnlyList<Follow> All => _saved;

    public Task<bool> ExistsAsync(Guid userId, FollowTarget target, CancellationToken cancellationToken) =>
        Task.FromResult(_saved.Any(follow => follow.UserId == userId && follow.Target == target));

    public void Add(Follow follow) => _pending.Add(follow);

    public Task<IReadOnlyList<Guid>> GetFollowerIdsAsync(IReadOnlyCollection<FollowTarget> targets, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>>(
            [.. _saved.Where(follow => targets.Contains(follow.Target)).Select(follow => follow.UserId)]);

    public void AddSaved(Follow follow) => _saved.Add(follow);

    public Task<IReadOnlyList<Follow>> ListOfAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Follow>>([.. _saved.Where(follow => follow.UserId == userId)]);

    public Task<int> CountAsync(FollowTarget target, CancellationToken cancellationToken) =>
        Task.FromResult(_saved.Count(follow => follow.Target == target));

    /// <summary>Grava os pendentes; um segundo follow do mesmo usuário e alvo violaria a chave primária.</summary>
    public void Commit()
    {
        foreach (var follow in _pending)
        {
            if (_saved.Any(saved => saved.UserId == follow.UserId && saved.Target == follow.Target))
            {
                throw new InvalidOperationException(
                    $"chave primária violada: {follow.UserId} já segue {follow.TargetKind}:{follow.TargetKey}");
            }

            _saved.Add(follow);
        }

        _pending.Clear();
    }
}

/// <summary>
/// SPEC-0029 — "vou" em memória: <see cref="Add"/> só entra em <see cref="All"/> depois do <c>SaveChangesAsync</c>
/// (como o EF); um segundo "vou" do mesmo usuário no mesmo show violaria a chave primária.
/// </summary>
internal sealed class InMemoryAttendances : IAttendanceRepository
{
    private readonly List<Attendance> _saved = [];
    private readonly List<Attendance> _pending = [];

    public IReadOnlyList<Attendance> All => _saved;

    public Task<bool> ExistsAsync(Guid userId, Guid showId, CancellationToken cancellationToken) =>
        Task.FromResult(_saved.Any(attendance => attendance.UserId == userId && attendance.ShowId == showId));

    public void Add(Attendance attendance) => _pending.Add(attendance);

    public Task<IReadOnlyDictionary<Guid, int>> CountAsync(IReadOnlyCollection<Guid> showIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, int>>(
            _saved.Where(attendance => showIds.Contains(attendance.ShowId))
                .GroupBy(attendance => attendance.ShowId)
                .ToDictionary(group => group.Key, group => group.Count()));

    public Task<IReadOnlyList<Guid>> GetAttendeeIdsAsync(Guid showId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>>([.. _saved.Where(attendance => attendance.ShowId == showId).Select(attendance => attendance.UserId)]);

    public void AddSaved(Attendance attendance) => _saved.Add(attendance);

    /// <summary>Grava os pendentes; um segundo "vou" do mesmo usuário e show violaria a chave primária.</summary>
    public void Commit()
    {
        foreach (var attendance in _pending)
        {
            if (_saved.Any(saved => saved.UserId == attendance.UserId && saved.ShowId == attendance.ShowId))
            {
                throw new InvalidOperationException(
                    $"chave primária violada: {attendance.UserId} já marcou vou em {attendance.ShowId}");
            }

            _saved.Add(attendance);
        }

        _pending.Clear();
    }
}

/// <summary>Conta as gravações e grava os follows e os "vou" pendentes.</summary>
internal sealed class InMemoryEngagementUnitOfWork(InMemoryFollows follows, InMemoryAttendances attendances) : IEngagementUnitOfWork
{
    public int Saves { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        Saves++;
        follows.Commit();
        attendances.Commit();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Dublê do <see cref="IEventsCatalog"/>: as cidades <c>curitiba</c> e <c>sao-paulo</c>; guarda cada consulta. SPEC-0029:
/// os shows de teste (<see cref="AddShow"/>, situação e início) e os nomes das cidades (<see cref="IEventsCatalogNames"/>).
/// </summary>
internal sealed class TestEventsCatalog : IEventsCatalog, IEventsCatalogNames
{
    private readonly HashSet<string> _cities = new(["curitiba", "sao-paulo"], StringComparer.Ordinal);
    private readonly Dictionary<string, string> _names = new(StringComparer.Ordinal) { ["curitiba"] = "Curitiba", ["sao-paulo"] = "São Paulo" };
    private readonly Dictionary<Guid, ShowState> _shows = [];

    public List<Guid> ShowQueries { get; } = [];

    /// <summary>Um show de teste com a situação <paramref name="status"/> e o início <paramref name="startsAtUtc"/>; devolve o id.</summary>
    public Guid AddShow(string status, DateTimeOffset startsAtUtc)
    {
        var id = Guid.CreateVersion7();
        _shows[id] = new ShowState(status, startsAtUtc);
        return id;
    }

    public Task<CatalogNames> GetNamesAsync(
        IReadOnlyCollection<string> citySlugs, IReadOnlyCollection<string> styleSlugs, CancellationToken ct) =>
        Task.FromResult(new CatalogNames(
            citySlugs.Where(_names.ContainsKey).Distinct(StringComparer.Ordinal).ToDictionary(slug => slug, slug => _names[slug], StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.Ordinal)));

    public List<string> CityQueries { get; } = [];

    public Task<bool> CityExistsAsync(string slug, CancellationToken ct)
    {
        CityQueries.Add(slug);
        return Task.FromResult(_cities.Contains(slug));
    }

    public Task<bool> StylesExistAsync(IReadOnlyCollection<string> slugs, CancellationToken ct) =>
        Task.FromResult(false);

    public Task<ShowState?> GetShowStateAsync(Guid showId, CancellationToken ct)
    {
        ShowQueries.Add(showId);
        return Task.FromResult(_shows.GetValueOrDefault(showId));
    }
}

/// <summary>
/// Dublê do <see cref="IProfilesApi"/>: só os perfis aprovados existem (um pedido pendente ainda não é perfil, SPEC-0021);
/// guarda cada consulta. SPEC-0029 (H27-06): a busca em lote <see cref="GetManyAsync"/> (assinatura do
/// <c>IShowProfiles.GetManyAsync</c>), com as consultas em <see cref="ManyQueries"/>.
/// </summary>
internal sealed class TestProfilesApi : IProfilesApi
{
    private readonly Dictionary<Guid, ProfileSummary> _approved = [];

    public List<Guid> Queries { get; } = [];

    public List<IReadOnlyCollection<Guid>> ManyQueries { get; } = [];

    /// <summary>Um perfil aprovado de banda (sem dono).</summary>
    public ProfileSummary AddApproved(string name = "Podridão", string slug = "podridao", string type = "band") =>
        AddApproved(Guid.CreateVersion7(), name, slug, type);

    /// <summary>SPEC-0029 — um perfil aprovado com o id <paramref name="id"/>.</summary>
    public ProfileSummary AddApproved(Guid id, string name = "Podridão", string slug = "podridao", string type = "band")
    {
        var profile = new ProfileSummary(id, type, name, slug, "curitiba", null);
        _approved[profile.Id] = profile;
        return profile;
    }

    /// <summary>SPEC-0029 (H27-06) — os perfis aprovados entre <paramref name="profileIds"/>, por id.</summary>
    public Task<IReadOnlyDictionary<Guid, ProfileSummary>> GetManyAsync(IReadOnlyCollection<Guid> profileIds, CancellationToken ct)
    {
        ManyQueries.Add([.. profileIds]);
        return Task.FromResult<IReadOnlyDictionary<Guid, ProfileSummary>>(
            profileIds.Distinct().Where(_approved.ContainsKey).ToDictionary(id => id, id => _approved[id]));
    }

    public Task<IReadOnlyList<ProfileSummary>> GetOwnedProfilesAsync(Guid userId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ProfileSummary>>([]);

    public Task<ProfileSummary?> GetAsync(Guid profileId, CancellationToken ct)
    {
        Queries.Add(profileId);
        return Task.FromResult(_approved.GetValueOrDefault(profileId));
    }
}
